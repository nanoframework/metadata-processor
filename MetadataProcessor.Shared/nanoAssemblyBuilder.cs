// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Original work from Oleg Rakhmatulin.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Main metadata transformation class - builds .NET nanoFramework assembly
    /// from full .NET Framework assembly metadata represented in Mono.Cecil format.
    /// </summary>
    public sealed class nanoAssemblyBuilder
    {
        private readonly nanoTablesContext _tablesContext;

        private readonly bool _verbose;
        private readonly bool _isCoreLibrary;

        public nanoTablesContext TablesContext => _tablesContext;

        /// <summary>
        /// Creates new instance of <see cref="nanoAssemblyBuilder"/> object.
        /// </summary>
        /// <param name="assemblyDefinition">Original assembly metadata in Mono.Cecil format.</param>
        /// <param name="explicitTypesOrder">List of full type names with explicit ordering.</param>
        /// <param name="stringSorter">Custom string literals sorter for UTs using only.</param>
        /// <param name="applyAttributesCompression">
        /// If contains <c>true</c> each type/method/field should contains one attribute of each type.
        /// </param>
        public nanoAssemblyBuilder(
            AssemblyDefinition assemblyDefinition,
            List<string> classNamesToExclude,
            bool verbose,
            bool isCoreLibrary = false,
            List<string> explicitTypesOrder = null,
            ICustomStringSorter stringSorter = null,
            bool applyAttributesCompression = false)
        {
            _tablesContext = new nanoTablesContext(
                assemblyDefinition,
                explicitTypesOrder,
                classNamesToExclude,
                stringSorter,
                applyAttributesCompression,
                verbose,
                isCoreLibrary);

            _verbose = verbose;
            _isCoreLibrary = isCoreLibrary;
        }

        /// <summary>
        /// Writes all .NET nanoFramework metadata into output stream.
        /// </summary>
        /// <param name="binaryWriter">Binary writer with correct endianness.</param>
        public void Write(
            nanoBinaryWriter binaryWriter)
        {
            var header = new nanoAssemblyDefinition(
                _tablesContext,
                _isCoreLibrary);
            header.Write(binaryWriter, true);

            foreach (InanoTable table in GetTables(_tablesContext))
            {
                long tableBegin = (binaryWriter.BaseStream.Position + 3) & 0xFFFFFFFC;
                table.Write(binaryWriter);

                long padding = (4 - ((binaryWriter.BaseStream.Position - tableBegin) % 4)) % 4;
                binaryWriter.WriteBytes(new byte[padding]);

                header.UpdateTableOffset(binaryWriter, tableBegin, padding);
            }

            binaryWriter.BaseStream.Seek(0, SeekOrigin.Begin);
            header.Write(binaryWriter, false);
        }

        /// <summary>
        /// Minimizes the assembly, removing unwanted and unused elements.
        /// </summary>
        /// <remarks>
        /// In order to minimize an assembly it has to have been previously compiled.
        /// </remarks>
        public void Minimize()
        {
            // remove unused types

            // build collection with all types except the ones to exclude
            HashSet<MetadataToken> setNew = new HashSet<MetadataToken>();
            HashSet<MetadataToken> set = new HashSet<MetadataToken>();

            foreach (TypeDefinition t in _tablesContext.TypeDefinitionTable.Items)
            {
                if (!t.IsToExclude())
                {
                    setNew.Add(t.MetadataToken);
                }
                else
                {
                    if (_verbose)
                    {
                        Console.WriteLine($"Excluding {t.FullName}");
                    }
                }
            }

            while (setNew.Count > 0)
            {
                HashSet<MetadataToken> setAdd = new HashSet<MetadataToken>();

                foreach (MetadataToken t in setNew.OrderBy(t => t.ToInt32()))
                {
                    set.Add(t);

                    if (_verbose)
                    {
                        string typeDescription = TokenToString(t);
                        Console.WriteLine($"Including {typeDescription}");
                    }

                    HashSet<MetadataToken> setTmp;

                    try
                    {
                        setTmp = BuildDependencyList(t);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine($"Exception processing token {t.ToInt32().ToString("x8")} {TokenToString(t)}");
                        throw;
                    }

                    // show dependencies
                    if (_verbose)
                    {
                        ShowDependencies(t, set, setTmp);
                    }

                    // copy type def
                    foreach (MetadataToken td in setTmp.OrderBy(mt => mt.ToInt32()))
                    {
                        setAdd.Add(td);
                    }
                }

                // remove type
                setNew = new HashSet<MetadataToken>();

                foreach (MetadataToken t in setAdd.OrderBy(mt => mt.ToInt32()))
                {
                    if (!set.Contains(t))
                    {
                        setNew.Add(t);
                    }
                }
            }

            // need to reset several tables so they are recreated only with the used items
            // order matters on several cases because the list recreation populates others
            _tablesContext.ResetByteCodeTable();
            _tablesContext.ResetSignaturesTable();
            _tablesContext.ResetResourcesTables();
            _tablesContext.AssemblyReferenceTable.RemoveUnusedItems(set);
            _tablesContext.TypeReferencesTable.RemoveUnusedItems(set);
            _tablesContext.FieldsTable.RemoveUnusedItems(set);
            _tablesContext.GenericParamsTable.RemoveUnusedItems(set);
            _tablesContext.MethodSpecificationTable.RemoveUnusedItems(set);
            _tablesContext.FieldReferencesTable.RemoveUnusedItems(set);
            _tablesContext.MethodDefinitionTable.RemoveUnusedItems(set);
            _tablesContext.MethodReferencesTable.RemoveUnusedItems(set);
            _tablesContext.TypeDefinitionTable.RemoveUnusedItems(set);
            _tablesContext.TypeDefinitionTable.ResetByteCodeOffsets();
            _tablesContext.ResetTypeSpecificationsTable();
            _tablesContext.AttributesTable.RemoveUnusedItems(set);
            _tablesContext.StringTable.RemoveUnusedItems(set);

            // renormalise type definitions look-up tables
            // by removing items that are not used

            foreach (TypeDefinition c in _tablesContext.TypeDefinitionTable.Items)
            {
                // collect fields to remove
                List<FieldDefinition> fieldsToRemove = new List<FieldDefinition>();

                foreach (FieldDefinition f in c.Fields)
                {
                    if (_tablesContext.FieldsTable.Items.FirstOrDefault(i => i.MetadataToken == f.MetadataToken) == null)
                    {
                        fieldsToRemove.Add(f);
                    }
                }

                // remove unused fields
                fieldsToRemove.Select(i => c.Fields.Remove(i)).ToList();

                // collect methods to remove
                List<MethodDefinition> methodsToRemove = new List<MethodDefinition>();

                foreach (MethodDefinition m in c.Methods)
                {
                    if (_tablesContext.MethodDefinitionTable.Items.FirstOrDefault(i => i.MetadataToken == m.MetadataToken) == null)
                    {
                        methodsToRemove.Add(m);
                    }
                }

                // remove unused methods
                methodsToRemove.Select(i => c.Methods.Remove(i)).ToList();

                // collect interfaces to remove
                List<InterfaceImplementation> interfacesToRemove = new List<InterfaceImplementation>();

                foreach (InterfaceImplementation i in c.Interfaces)
                {
                    // remove unused interfaces
                    bool used = false;

                    // because we don't have an interface definition table
                    // have to do it the hard way: search the type definition that contains the interface type
                    foreach (TypeDefinition t in _tablesContext.TypeDefinitionTable.Items)
                    {
                        InterfaceImplementation ii1 = t.Interfaces.FirstOrDefault(ii => ii.MetadataToken == i.MetadataToken);
                        if (ii1 != null)
                        {
                            used = true;

                            break;
                        }
                    }

                    if (!used)
                    {
                        interfacesToRemove.Add(i);
                    }
                }

                interfacesToRemove.Select(i => c.Interfaces.Remove(i)).ToList();
            }

            // flag minimize completed
            _tablesContext.MinimizeComplete = true;
        }

        private void ShowDependencies(MetadataToken token, HashSet<MetadataToken> set, HashSet<MetadataToken> setTmp)
        {
            string tokenFrom = TokenToString(token);

            foreach (MetadataToken m in setTmp.OrderBy(mt => mt.ToInt32()))
            {
                if (!set.Contains(m))
                {
                    try
                    {
                        Console.WriteLine($"{tokenFrom} -> {TokenToString(m)}");
                    }
                    catch (Exception)
                    {
                        throw new ArgumentException($"Exception listing token dependencies. Problematic token is 0x{m.ToInt32().ToString("x8")}.");
                    }
                }
            }
        }

        /// <summary>
        /// Returns the native checksum of the assembly.
        /// </summary>
        /// <returns>Native checksum of the assembly.</returns>
        /// <remarks>
        /// Need to call <see cref="Minimize()"/> before calling this method otherwise the checksum is not available.
        /// </remarks>
        public string GetNativeChecksum()
        {
            if (_tablesContext.MinimizeComplete)
            {
                return $"0x{_tablesContext.NativeMethodsCrc.CurrentCrc.ToString("X8")}";
            }
            else
            {
                throw new InvalidOperationException("Error: can't provide checksum without compiling the assembly first.");
            }
        }

        private void ShowDependencies(int token, HashSet<int> set, HashSet<int> setTmp)
        {
            string tokenFrom = TokenToString(token);

            foreach (int m in setTmp.OrderBy(mt => mt))
            {
                if (!set.Contains(m))
                {
                    System.Console.WriteLine($"{tokenFrom} -> {TokenToString(m)}");
                }
            }
        }

        private HashSet<int> BuildDependencyList(int token)
        {
            HashSet<MetadataToken> tokens = BuildDependencyList(_tablesContext.AssemblyDefinition.MainModule.LookupToken(token).MetadataToken);

            var output = new HashSet<int>();

            var dummy = tokens.Select(t => output.Add(t.ToInt32())).ToList();

            return output;
        }

        private HashSet<MetadataToken> BuildDependencyList(MetadataToken token)
        {
            HashSet<MetadataToken> set = new HashSet<MetadataToken>();

            switch (token.TokenType)
            {
                case TokenType.TypeRef:
                    TypeReference tr = _tablesContext.TypeReferencesTable.Items.FirstOrDefault(i => i.MetadataToken == token);

                    if (tr.IsNested)
                    {
                        set.Add(tr.DeclaringType.MetadataToken);
                    }
                    else
                    {
                        switch (tr.Scope.MetadataToken.TokenType)
                        {
                            case TokenType.AssemblyRef:
                            case TokenType.TypeRef:
                                set.Add(tr.Scope.MetadataToken);
                                break;
                        }
                    }
                    break;

                case TokenType.MemberRef:

                    FieldReference fr = null;

                    // try to find a method reference
                    MethodReference mr = _tablesContext.MethodReferencesTable.Items.FirstOrDefault(i => i.MetadataToken == token);

                    if (mr != null &&
                        mr.ReturnType != null)
                    {
                        if (mr.DeclaringType != null)
                        {
                            if (mr.DeclaringType is TypeSpecification)
                            {
                                // Cecil.Mono has a bug providing TypeSpecs Metadata tokens generic parameters variables, so we need to check against our internal table and build one from it
                                if (_tablesContext.TypeSpecificationsTable.TryGetTypeReferenceId(mr.DeclaringType, out ushort referenceId))
                                {
                                    // add "fabricated" token for TypeSpec using the referenceId as RID
                                    set.Add(new MetadataToken(
                                        TokenType.TypeSpec,
                                        referenceId));

                                    // add the metadata token for the element type
                                    set.Add(mr.DeclaringType.GetElementType().MetadataToken);
                                }
                                else
                                {
                                    Debug.Fail($"Couldn't find a TypeSpec entry for {mr.DeclaringType}");
                                }
                            }
                            else
                            {
                                set.Add(mr.DeclaringType.MetadataToken);
                            }
                        }

                        if (mr.MethodReturnType.ReturnType.IsValueType &&
                            !mr.MethodReturnType.ReturnType.IsPrimitive)
                        {
                            set.Add(mr.MethodReturnType.ReturnType.MetadataToken);
                        }
                        else if (mr.ReturnType.IsArray)
                        {
                            if (mr.ReturnType.DeclaringType != null)
                            {
                                set.Add(mr.ReturnType.DeclaringType.MetadataToken);
                            }
                            else
                            {
                                if (mr.ReturnType.GetElementType().FullName != "System.Void" &&
                                     mr.ReturnType.GetElementType().FullName != "System.String" &&
                                     mr.ReturnType.GetElementType().FullName != "System.Object" &&
                                    !mr.ReturnType.GetElementType().IsPrimitive)
                                {
                                    set.Add(mr.ReturnType.GetElementType().MetadataToken);
                                }
                            }
                        }
                        else
                        {
                            if (mr.ReturnType.MetadataType == MetadataType.ValueType)
                            {
                                if (mr.ReturnType.FullName != "System.Void" &&
                                     mr.ReturnType.FullName != "System.String" &&
                                     mr.ReturnType.FullName != "System.Object" &&
                                    !mr.ReturnType.IsPrimitive)
                                {
                                    set.Add(mr.ReturnType.MetadataToken);
                                }
                            }
                            if (mr.ReturnType.MetadataType == MetadataType.Class)
                            {
                                set.Add(mr.ReturnType.MetadataToken);
                            }
                        }

                        // parameters
                        foreach (ParameterDefinition p in mr.Parameters)
                        {
                            if (p.ParameterType.DeclaringType != null)
                            {
                                TypeDefinition resolvedType = p.ParameterType.Resolve();
                                if (resolvedType != null && resolvedType.IsEnum)
                                {
                                    set.Add(p.ParameterType.MetadataToken);
                                }
                                else
                                {
                                    set.Add(p.ParameterType.DeclaringType.MetadataToken);
                                }
                            }
                            if (p.ParameterType.MetadataType == MetadataType.Class)
                            {
                                set.Add(p.ParameterType.MetadataToken);
                            }
                            if (p.ParameterType.IsValueType &&
                                !p.ParameterType.IsPrimitive)
                            {
                                set.Add(p.ParameterType.MetadataToken);
                            }
                            if (p.ParameterType is GenericInstanceType)
                            {
                                set.Add(p.ParameterType.MetadataToken);
                                set.Add(p.ParameterType.GetElementType().MetadataToken);
                            }
                        }
                    }

                    if (mr == null)
                    {
                        // try now with field references
                        fr = _tablesContext.FieldReferencesTable.Items.FirstOrDefault(i => i.MetadataToken == token);

                        if (fr != null)
                        {
                            if (fr.DeclaringType != null)
                            {
                                if (fr.DeclaringType is TypeSpecification)
                                {
                                    // Cecil.Mono has a bug providing TypeSpecs Metadata tokens generic parameters variables, so we need to check against our internal table and build one from it
                                    if (_tablesContext.TypeSpecificationsTable.TryGetTypeReferenceId(fr.DeclaringType, out ushort referenceId))
                                    {
                                        // add "fabricated" token for TypeSpec using the referenceId as RID
                                        set.Add(new MetadataToken(
                                            TokenType.TypeSpec,
                                            referenceId));

                                        // add the metadata token for the element type
                                        set.Add(fr.DeclaringType.GetElementType().MetadataToken);
                                    }
                                    else
                                    {
                                        Debug.Fail($"Couldn't find a TypeSpec entry for {fr.DeclaringType}");
                                    }
                                }
                                else
                                {
                                    set.Add(fr.DeclaringType.MetadataToken);
                                }
                            }

                            if (fr.FieldType.MetadataType == MetadataType.Class)
                            {
                                set.Add(fr.FieldType.MetadataToken);
                            }
                            else if (!fr.FieldType.IsPrimitive &&
                                      fr.FieldType.IsValueType &&
                                      fr.FieldType.FullName != "System.Void" &&
                                      fr.FieldType.FullName != "System.String" &&
                                      fr.FieldType.FullName != "System.Object")
                            {
                                set.Add(fr.FieldType.MetadataToken);
                            }
                            else if (fr.FieldType.IsArray)
                            {
                                if (fr.FieldType.DeclaringType != null)
                                {
                                    set.Add(fr.FieldType.MetadataToken);
                                }
                                else
                                {
                                    TypeReference elementType = fr.FieldType.GetElementType();

                                    if (elementType.FullName != "System.Void" &&
                                         elementType.FullName != "System.String" &&
                                         elementType.FullName != "System.Object" &&
                                        !elementType.IsPrimitive)
                                    {
                                        set.Add(elementType.MetadataToken);
                                    }
                                }
                            }
                            else if (fr.FieldType.DeclaringType != null)
                            {
                                set.Add(fr.FieldType.DeclaringType.MetadataToken);
                            }
                        }
                    }

                    Debug.Assert(mr != null || fr != null);

                    break;

                case TokenType.TypeSpec:
                    // Developer notes:
                    // Because the issue with Mono.Cecil not providing the correct metadata token for TypeSpec,
                    // need to search the TypeSpec token in the two "formats".
                    // 1. The original token as provided by Mono.Cecil
                    // 2. The "fabricated" token, which is the one that is used in the TypeSpec table
                    // It's OK to add both to the set because they end up referencing to the same type.
                    // Anyways, as this a minimize operation, it's preferable to have both rather than none.

                    // start searching for metadata token
                    TypeReference ts1 = _tablesContext.TypeSpecificationsTable.TryGetTypeSpecification(token);

                    if (ts1 != null)
                    {
                        // found it, let's add it
                        set.Add(token);
                    }

                    // now try to find the TypeSpec from the "fabricated" token, using the RID 
                    TypeReference ts2 = _tablesContext.TypeSpecificationsTable.TryGetTypeReferenceByIndex((ushort)token.RID);

                    if (ts2 != null)
                    {
                        set.Add(ts2.MetadataToken);
                    }

                    // sanity check
                    Debug.Assert(ts1 != null || ts2 != null);

                    break;

                case TokenType.TypeDef:
                    TypeDefinition td = _tablesContext.TypeDefinitionTable.Items.FirstOrDefault(i => i.MetadataToken == token);

                    if (td.BaseType != null)
                    {
                        set.Add(td.BaseType.MetadataToken);
                    }

                    if (td.DeclaringType != null)
                    {
                        set.Add(td.DeclaringType.MetadataToken);
                    }

                    // include attributes
                    foreach (CustomAttribute c in td.CustomAttributes)
                    {
                        if (!nanoTablesContext.ClassNamesToExclude.Contains(c.AttributeType.FullName) &&
                            c.AttributeType.IsToInclude())
                        {
                            set.Add(c.Constructor.MetadataToken);
                        }
                    }

                    // fields
                    IEnumerable<FieldDefinition> tdFields = td.Fields.Where(f => !f.IsLiteral);

                    foreach (FieldDefinition f in tdFields)
                    {
                        if (!nanoTablesContext.ClassNamesToExclude.Contains(f.DeclaringType.FullName))
                        {
                            set.Add(f.MetadataToken);
                        }
                    }

                    // generic parameters
                    foreach (GenericParameter g in td.GenericParameters)
                    {
                        if (!nanoTablesContext.ClassNamesToExclude.Contains(g.DeclaringType.FullName))
                        {
                            set.Add(g.MetadataToken);
                        }
                    }

                    // methods
                    foreach (MethodDefinition m in td.Methods)
                    {
                        if (!nanoTablesContext.ClassNamesToExclude.Contains(m.DeclaringType.FullName))
                        {
                            set.Add(m.MetadataToken);
                        }
                    }

                    // interfaces
                    foreach (InterfaceImplementation i in td.Interfaces)
                    {
                        if (!nanoTablesContext.ClassNamesToExclude.Contains(i.InterfaceType.FullName))
                        {
                            set.Add(i.MetadataToken);
                        }
                    }

                    break;

                case TokenType.Field:
                    FieldDefinition fd = _tablesContext.FieldsTable.Items.FirstOrDefault(i => i.MetadataToken == token);

                    if (fd is TypeReference)
                    {
                        set.Add(fd.MetadataToken);
                    }
                    else if (fd.FieldType.IsValueType &&
                             !fd.FieldType.IsPrimitive)
                    {
                        set.Add(fd.FieldType.MetadataToken);
                    }
                    else if (fd.FieldType is GenericInstanceType)
                    {
                        set.Add(fd.FieldType.MetadataToken);
                        set.Add(fd.FieldType.GetElementType().MetadataToken);
                    }
                    else if (fd.FieldType.IsArray)
                    {
                        if (fd.FieldType.DeclaringType != null)
                        {
                            set.Add(fd.FieldType.DeclaringType.MetadataToken);
                        }
                        else
                        {
                            if (fd.FieldType.GetElementType().FullName != "System.Void" &&
                                fd.FieldType.GetElementType().FullName != "System.String" &&
                                fd.FieldType.GetElementType().FullName != "System.Object" &&
                                !fd.FieldType.GetElementType().IsPrimitive)
                            {
                                set.Add(fd.FieldType.GetElementType().MetadataToken);
                            }
                        }
                    }
                    else if (!fd.FieldType.IsValueType &&
                             !fd.FieldType.IsPrimitive &&
                              fd.FieldType.FullName != "System.Void" &&
                              fd.FieldType.FullName != "System.String" &&
                              fd.FieldType.FullName != "System.Object")
                    {
                        set.Add(fd.FieldType.MetadataToken);
                    }

                    // attributes
                    foreach (CustomAttribute c in fd.CustomAttributes)
                    {
                        if (!nanoTablesContext.ClassNamesToExclude.Contains(c.AttributeType.FullName) &&
                            c.AttributeType.IsToInclude())
                        {
                            set.Add(c.Constructor.MetadataToken);
                        }
                    }

                    break;

                case TokenType.Method:
                    MethodDefinition md = _tablesContext.MethodDefinitionTable.Items.FirstOrDefault(i => i.MetadataToken == token);

                    // return value
                    if (md.ReturnType.IsValueType &&
                        !md.ReturnType.IsPrimitive)
                    {
                        set.Add(md.ReturnType.MetadataToken);
                    }
                    else if (md.ReturnType.IsArray)
                    {
                        if (md.ReturnType.DeclaringType != null)
                        {
                            set.Add(md.ReturnType.DeclaringType.MetadataToken);
                        }
                        else
                        {
                            if (md.ReturnType.GetElementType().FullName != "System.Void" &&
                                md.ReturnType.GetElementType().FullName != "System.String" &&
                                md.ReturnType.GetElementType().FullName != "System.Object" &&
                                !md.ReturnType.GetElementType().IsPrimitive)
                            {
                                set.Add(md.ReturnType.GetElementType().MetadataToken);
                            }
                        }
                    }
                    else if (!md.ReturnType.IsValueType &&
                             !md.ReturnType.IsPrimitive &&
                             !md.ReturnType.IsByReference &&
                              md.ReturnType.FullName != "System.Void" &&
                              md.ReturnType.FullName != "System.String" &&
                              md.ReturnType.FullName != "System.Object")
                    {
                        set.Add(md.ReturnType.MetadataToken);
                    }

                    // generic parameters
                    if (md.HasGenericParameters)
                    {
                        foreach (GenericParameter gp in md.GenericParameters)
                        {
                            set.Add(gp.MetadataToken);
                        }
                    }

                    // parameters
                    foreach (ParameterDefinition p in md.Parameters)
                    {
                        TypeReference parameterType = null;

                        if (p.ParameterType is ByReferenceType byReference)
                        {
                            parameterType = byReference.ElementType;
                        }
                        else
                        {
                            parameterType = p.ParameterType;
                        }

                        if (parameterType.IsArray)
                        {
                            if (parameterType.DeclaringType != null)
                            {
                                set.Add(parameterType.DeclaringType.MetadataToken);
                            }
                            else
                            {
                                if (parameterType.GetElementType().FullName != "System.Void" &&
                                    parameterType.GetElementType().FullName != "System.String" &&
                                    parameterType.GetElementType().FullName != "System.Object" &&
                                    !parameterType.GetElementType().IsPrimitive)
                                {
                                    set.Add(parameterType.GetElementType().MetadataToken);
                                }
                            }
                        }
                        else if (parameterType.MetadataType == MetadataType.Class)
                        {
                            set.Add(parameterType.MetadataToken);
                        }
                        else if (parameterType.IsValueType &&
                                 !parameterType.IsPrimitive)
                        {
                            set.Add(parameterType.MetadataToken);
                        }
                        else if (parameterType is GenericInstanceType)
                        {
                            set.Add(parameterType.MetadataToken);
                            set.Add(parameterType.GetElementType().MetadataToken);
                        }
                        else if (parameterType is GenericParameter)
                        {
                            set.Add(parameterType.MetadataToken);

                            foreach (GenericParameter gp in parameterType.GenericParameters)
                            {
                                set.Add(gp.MetadataToken);
                                if (parameterType.DeclaringType != null)
                                {
                                    set.Add(parameterType.DeclaringType.MetadataToken);
                                }
                            }

                            if (parameterType.DeclaringType != null)
                            {
                                set.Add(parameterType.DeclaringType.MetadataToken);
                            }
                        }
                        else if (!parameterType.IsValueType &&
                                 !parameterType.IsPrimitive &&
                                  parameterType.FullName != "System.Void" &&
                                  parameterType.FullName != "System.String" &&
                                  parameterType.FullName != "System.Object")
                        {
                            set.Add(parameterType.MetadataToken);
                        }
                    }

                    if (md.HasBody)
                    {
                        // variables
                        foreach (VariableDefinition v in md.Body.Variables)
                        {
                            if (v.VariableType.DeclaringType != null)
                            {
                                TypeDefinition resolvedType = v.VariableType.Resolve();

                                if (resolvedType != null && resolvedType.IsEnum)
                                {
                                    set.Add(v.VariableType.MetadataToken);
                                }
                                else
                                {
                                    set.Add(v.VariableType.DeclaringType.MetadataToken);
                                }
                            }
                            else if (v.VariableType.MetadataType == MetadataType.Class)
                            {
                                set.Add(v.VariableType.MetadataToken);
                            }
                            else if (v.VariableType.MetadataType == MetadataType.Array)
                            {
                                if (v.VariableType.GetElementType().FullName != "System.Void" &&
                                    v.VariableType.GetElementType().FullName != "System.String" &&
                                    v.VariableType.GetElementType().FullName != "System.Object" &&
                                    !v.VariableType.GetElementType().IsPrimitive)
                                {
                                    set.Add(v.VariableType.GetElementType().MetadataToken);
                                }
                            }
                            else if (v.VariableType.IsValueType &&
                                    !v.VariableType.IsPrimitive)
                            {
                                set.Add(v.VariableType.MetadataToken);
                            }
                            else if (v.VariableType is GenericInstanceType)
                            {
                                // Cecil.Mono has a bug providing TypeSpecs Metadata tokens generic parameters variables, so we need to check against our internal table and build one from it
                                if (_tablesContext.TypeSpecificationsTable.TryGetTypeReferenceId(v.VariableType, out ushort referenceId))
                                {
                                    // add "fabricated" token for TypeSpec using the referenceId as RID
                                    set.Add(new MetadataToken(
                                        TokenType.TypeSpec,
                                        referenceId));

                                    // add the metadata token for the element type
                                    set.Add(v.VariableType.GetElementType().MetadataToken);
                                }
                                else
                                {
                                    Debug.Fail($"Couldn't find a TypeSpec entry for {v.VariableType}");
                                }
                            }
                            else if (v.VariableType.IsPointer)
                            {
                                string message = $"Pointer types in unsafe code aren't supported. Can't use {v.VariableType} variable in \"{md.FullName}\".";

                                Console.WriteLine(message);
                                throw new Exception(message);
                            }
                        }

                        // op codes
                        foreach (Instruction i in md.Body.Instructions)
                        {
                            if (i.Operand is MethodReference)
                            {
                                var methodReferenceType = i.Operand as MethodReference;

                                set.Add(methodReferenceType.MetadataToken);

                                if (_tablesContext.MethodReferencesTable.TryGetMethodReferenceId(methodReferenceType, out ushort referenceId))
                                {
                                    if (methodReferenceType.DeclaringType != null &&
                                       methodReferenceType.DeclaringType.IsGenericInstance)
                                    {
                                        // Cecil.Mono has a bug providing TypeSpecs Metadata tokens generic parameters variables, so we need to check against our internal table and build one from it
                                        if (_tablesContext.TypeSpecificationsTable.TryGetTypeReferenceId(methodReferenceType.DeclaringType, out referenceId))
                                        {
                                            // add "fabricated" token for TypeSpec using the referenceId as RID
                                            set.Add(new MetadataToken(
                                                TokenType.TypeSpec,
                                                referenceId));

                                            // add the metadata token for the element type
                                            set.Add(methodReferenceType.DeclaringType.GetElementType().MetadataToken);
                                        }
                                        else
                                        {
                                            Debug.Fail($"Couldn't find a TypeSpec entry for {methodReferenceType.DeclaringType}");
                                        }
                                    }
                                }
                            }
                            else if (i.Operand is FieldReference ||
                                     i.Operand is TypeDefinition ||
                                     i.Operand is MethodSpecification ||
                                     i.Operand is TypeReference)
                            {
                                set.Add(((IMetadataTokenProvider)i.Operand).MetadataToken);
                            }
                            else if (
                                i.OpCode.OperandType is OperandType.InlineType ||
                                i.Operand is GenericInstanceType ||
                                i.Operand is GenericInstanceMethod ||
                                i.Operand is GenericParameter)
                            {
                                var opType = (TypeReference)i.Operand;

                                MetadataToken opToken = ((IMetadataTokenProvider)i.Operand).MetadataToken;

                                if (opToken.TokenType == TokenType.TypeSpec)
                                {
                                    // Cecil.Mono has a bug providing TypeSpecs Metadata tokens generic parameters variables, so we need to check against our internal table and build one from it
                                    if (_tablesContext.TypeSpecificationsTable.TryGetTypeReferenceId(opType, out ushort referenceId))
                                    {
                                        // add "fabricated" token for TypeSpec using the referenceId as RID
                                        set.Add(new MetadataToken(
                                            TokenType.TypeSpec,
                                            referenceId));

                                        // add the metadata token for the element type
                                        set.Add(opType.GetElementType().MetadataToken);
                                    }
                                    else
                                    {
                                        Debug.Fail($"Couldn't find a TypeSpec entry for {opType}");
                                    }
                                }
                                else
                                {
                                    set.Add(opToken);
                                }
                            }
                            else if (i.Operand is string)
                            {
                                ushort stringId = _tablesContext.StringTable.GetOrCreateStringId((string)i.Operand);

                                var newToken = new MetadataToken(TokenType.String, stringId);

                                set.Add(newToken);
                            }
                        }

                        // exceptions
                        foreach (ExceptionHandler e in md.Body.ExceptionHandlers)
                        {
                            if (e.HandlerType != Mono.Cecil.Cil.ExceptionHandlerType.Filter)
                            {
                                if (e.CatchType != null)
                                {
                                    set.Add(((IMetadataTokenProvider)e.CatchType).MetadataToken);
                                }
                            }
                        }
                    }

                    // attributes
                    foreach (CustomAttribute c in md.CustomAttributes)
                    {
                        if (!nanoTablesContext.ClassNamesToExclude.Contains(c.AttributeType.FullName) &&
                            c.AttributeType.IsToInclude())
                        {
                            set.Add(c.Constructor.MetadataToken);
                        }
                    }

                    break;

                case TokenType.InterfaceImpl:
                    // because we don't have an interface definition table
                    // have to do it the hard way: search the type definition that contains the interface
                    foreach (TypeDefinition t in _tablesContext.TypeDefinitionTable.Items)
                    {
                        InterfaceImplementation ii1 = t.Interfaces.FirstOrDefault(i => i.MetadataToken == token);
                        if (ii1 != null)
                        {
                            set.Add(ii1.InterfaceType.MetadataToken);
                            set.Add(t.MetadataToken);

                            break;
                        }
                    }

                    break;

                case TokenType.MethodSpec:
                    MethodSpecification ms = _tablesContext.MethodSpecificationTable.Items.FirstOrDefault(i => i.MetadataToken == token);

                    if (ms != null)
                    {
                        set.Add(token);
                    }
                    break;

                case TokenType.GenericParam:
                    GenericParameter gpar = _tablesContext.GenericParamsTable.Items.FirstOrDefault(i => i.MetadataToken == token);

                    if (gpar != null)
                    {
                        // need to add their constraints if, any
                        foreach (GenericParameterConstraint c in gpar.Constraints)
                        {
                            set.Add(c.MetadataToken);
                        }
                    }
                    break;

                case TokenType.AssemblyRef:
                case TokenType.String:
                case TokenType.GenericParamConstraint:
                    // we are good with these, nothing to do here
                    break;

                default:
                    Debug.Fail($"Unable to process token {token}.");
                    break;
            }

            return set;
        }

        private string TokenToString(int token)
        {
            return TokenToString(_tablesContext.AssemblyDefinition.MainModule.LookupToken(token).MetadataToken);
        }

        private string TokenToString(MetadataToken token)
        {
            StringBuilder output = new StringBuilder();

            switch (token.TokenType)
            {
                case TokenType.TypeRef:
                    TypeReference tr = _tablesContext.TypeReferencesTable.Items.FirstOrDefault(i => i.MetadataToken == token);

                    if (tr.Scope != null)
                    {
                        output.Append(TokenToString(tr.Scope.MetadataToken));
                        if (tr.Scope.MetadataToken.TokenType is TokenType.TypeRef)
                        {
                            output.Append(".");
                        }
                    }

                    output.Append(tr.FullName);
                    break;

                case TokenType.TypeDef:
                    TypeDefinition td = _tablesContext.TypeDefinitionTable.Items.FirstOrDefault(i => i.MetadataToken == token);

                    if (td.DeclaringType != null)
                    {
                        output.Append(TokenToString(td.DeclaringType.MetadataToken));
                        output.Append("+");

                        output.Append(td.Name);
                    }
                    else
                    {
                        output.Append(td.FullName);
                    }

                    break;

                case TokenType.Field:
                    FieldDefinition fd = _tablesContext.FieldsTable.Items.FirstOrDefault(i => i.MetadataToken == token);

                    if (fd.DeclaringType != null)
                    {
                        output.Append(TokenToString(fd.DeclaringType.MetadataToken));
                        output.Append("::");
                    }

                    output.Append(fd.Name);
                    break;

                case TokenType.GenericParam:
                    GenericParameter gp = _tablesContext.GenericParamsTable.Items.FirstOrDefault(g => g.MetadataToken == token);

                    output.Append($"[GenericParam 0x{token.ToUInt32().ToString("X8")}]");

                    if (gp.DeclaringType != null)
                    {
                        output.Append(TokenToString(gp.DeclaringType.MetadataToken));
                        output.Append("::");
                    }
                    else if (gp.DeclaringMethod != null)
                    {
                        output.Append(TokenToString(gp.DeclaringMethod.MetadataToken));
                        output.Append("::");
                    }

                    output.Append(gp.Name);

                    break;

                case TokenType.Method:
                    MethodDefinition md = _tablesContext.MethodDefinitionTable.Items.FirstOrDefault(i => i.MetadataToken == token);

                    if (md.DeclaringType != null)
                    {
                        output.Append(TokenToString(md.DeclaringType.MetadataToken));
                        output.Append("::");
                    }

                    output.Append(md.Name);

                    break;

                case TokenType.InterfaceImpl:

                    // because we don't have an interface definition table
                    // have to do it the hard way: search the type definition that contains the interface
                    foreach (TypeDefinition t in _tablesContext.TypeDefinitionTable.Items)
                    {
                        InterfaceImplementation ii = t.Interfaces.FirstOrDefault(i => i.MetadataToken == token);
                        if (ii != null)
                        {
                            string classToken = TokenToString(t.MetadataToken);
                            string interfaceToken = TokenToString(ii.InterfaceType.MetadataToken);

                            output.Append($"[{classToken} implements {interfaceToken}]");

                            break;
                        }
                    }

                    break;

                case TokenType.MemberRef:

                    TypeReference typeRef = null;
                    string typeName = string.Empty;

                    // try to find a method reference
                    MethodReference mr = _tablesContext.MethodReferencesTable.Items.FirstOrDefault(i => i.MetadataToken == token);

                    if (mr != null)
                    {
                        typeRef = mr.DeclaringType;
                        typeName = mr.Name;
                    }
                    else
                    {
                        // try now with field references
                        FieldReference fr = _tablesContext.FieldReferencesTable.Items.FirstOrDefault(i => i.MetadataToken == token);

                        if (fr != null)
                        {
                            typeRef = fr.DeclaringType;
                            typeName = fr.Name;
                        }
                        else
                        {
                            // try now with generic parameters
                            GenericParameter gr = _tablesContext.GenericParamsTable.Items.FirstOrDefault(g => g.MetadataToken == token);

                            if (gr != null)
                            {
                                typeRef = gr.DeclaringType;
                                typeName = gr.Name;
                            }
                        }
                    }

                    Debug.Assert(typeRef != null);
                    Debug.Assert(typeName != string.Empty);

                    if (typeRef != null)
                    {
                        output.Append(TokenToString(typeRef.MetadataToken));
                        output.Append("::");
                    }

                    output.Append(typeName);
                    break;

                case TokenType.ModuleRef:
                    // TODO
                    break;


                case TokenType.TypeSpec:
                    output.Append($"[TypeSpec 0x{token.ToUInt32().ToString("X8")}]");
                    break;

                case TokenType.AssemblyRef:
                    AssemblyNameReference ar = _tablesContext.AssemblyReferenceTable.Items.FirstOrDefault(i => i.MetadataToken == token);

                    output.Append($"[{ar.Name}]");
                    break;

                case TokenType.String:
                    string sr = _tablesContext.StringTable.TryGetString((ushort)token.RID);

                    if (sr != null)
                    {
                        output.Append($"'{sr}'");
                    }
                    break;

                case TokenType.MethodSpec:
                    output.Append($"[MethodSpec 0x{token.ToUInt32().ToString("X8")}]");
                    break;

                case TokenType.GenericParamConstraint:
                    output.Append($"[GenericParamConstraint 0x{token.ToUInt32().ToString("X8")}]");
                    break;

                default:
                    Debug.Fail($"Unable to process token {token}.");
                    break;
            }

            // output token ID if empty
            if (output.Length == 0)
            {
                output.Append($"[0x{token.ToUInt32().ToString("X8")}]");
            }

            return output.ToString();
        }

        public void Write(string fileName)
        {
            var pdbxWriter = new nanoPdbxFileWriter(_tablesContext);
            pdbxWriter.Write(fileName);
        }

        /// <summary>
        /// Count of tables in the assembly
        /// </summary>
        static public int TablesCount => 0x12;

        internal static IEnumerable<InanoTable> GetTables(
            nanoTablesContext context)
        {
            //////////////////////////////////////////////////
            // order matters and must follow CLR_TABLESENUM //
            //////////////////////////////////////////////////

            //////////////////////////////////////////////////////////////
            // update count property above whenever changing the tables //
            //////////////////////////////////////////////////////////////

            yield return context.AssemblyReferenceTable;

            yield return context.TypeReferencesTable;

            yield return context.FieldReferencesTable;

            yield return context.MethodReferencesTable;

            yield return context.TypeDefinitionTable;

            yield return context.FieldsTable;

            yield return context.MethodDefinitionTable;

            yield return context.GenericParamsTable;

            yield return context.MethodSpecificationTable;

            yield return context.TypeSpecificationsTable;

            yield return context.AttributesTable;

            yield return context.ResourcesTable;

            yield return context.ResourceDataTable;

            context.ByteCodeTable.UpdateStringTable();
            context.StringTable.GetOrCreateStringId(
                context.AssemblyDefinition.Name.Name);

            yield return context.StringTable;

            yield return context.SignaturesTable;

            yield return context.ByteCodeTable;

            yield return context.ResourceFileTable;

            yield return nanoEmptyTable.Instance;
        }
    }
}
