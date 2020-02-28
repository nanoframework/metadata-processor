//
// Copyright (c) 2019 The nanoFramework project contributors
// Original work from Oleg Rakhmatulin.
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using Mono.Collections.Generic;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

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

            foreach (var table in GetTables(_tablesContext))
            {
                var tableBegin = (binaryWriter.BaseStream.Position + 3) & 0xFFFFFFFC;
                table.Write(binaryWriter);

                var padding = (4 - ((binaryWriter.BaseStream.Position - tableBegin) % 4)) % 4;
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
            var setNew = new HashSet<MetadataToken>();
            var set = new HashSet<MetadataToken>();

            foreach (var t in _tablesContext.TypeDefinitionTable.TypeDefinitions)
            {
                if (!t.IsClassToExclude())
                {
                    setNew.Add(t.MetadataToken);
                }
                else
                {
                    if (_verbose) System.Console.WriteLine($"Excluding {t.FullName}");
                }
            }

            while(setNew.Count > 0)
            {
                var setAdd = new HashSet<MetadataToken>();

                foreach (var t in setNew.OrderBy(t => t.ToInt32()))
                {
                    set.Add(t);

                    if(_verbose)
                    {
                        var typeDescription = TokenToString(t);
                        System.Console.WriteLine($"Including {typeDescription}");
                    }

                    HashSet<MetadataToken> setTmp = BuildDependencyList(t);

                    // show dependencies
                    if (_verbose)
                    {
                        ShowDependencies(t, set, setTmp);
                    }

                    // copy type def
                    foreach (var td in setTmp.OrderBy(mt => mt.ToInt32()))
                    {
                        setAdd.Add(td);
                    }
                }

                // remove type
                setNew = new HashSet<MetadataToken>();

                foreach(var t in setAdd.OrderBy(mt => mt.ToInt32()))
                {
                    if(!set.Contains(t))
                    {
                        setNew.Add(t);
                    }
                }
            }

            // need to reset several tables so they are recreated only with the used items
            _tablesContext.AssemblyReferenceTable.RemoveUnusedItems(set);
            _tablesContext.TypeReferencesTable.RemoveUnusedItems(set);
            _tablesContext.FieldsTable.RemoveUnusedItems(set);
            _tablesContext.FieldReferencesTable.RemoveUnusedItems(set);
            _tablesContext.MethodDefinitionTable.RemoveUnusedItems(set);
            _tablesContext.MethodReferencesTable.RemoveUnusedItems(set);
            _tablesContext.TypeDefinitionTable.RemoveUnusedItems(set);
            _tablesContext.TypeDefinitionTable.ResetByteCodeOffsets();
            _tablesContext.AttributesTable.RemoveUnusedItems(set);
            _tablesContext.ResetByteCodeTable();
            _tablesContext.ResetSignaturesTable();
            _tablesContext.ResetResourcesTables();
            _tablesContext.StringTable.RemoveUnusedItems(set);
            
            // renormalise type definitions look-up tables
            // by removing items that are not used

            foreach (var c in _tablesContext.TypeDefinitionTable.Items)
            {
                // collect fields to remove
                List<FieldDefinition> fieldsToRemove = new List<FieldDefinition>();

                foreach (var f in c.Fields)
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

                foreach (var m in c.Methods)
                {
                    if(_tablesContext.MethodDefinitionTable.Items.FirstOrDefault(i => i.MetadataToken == m.MetadataToken) == null)
                    {
                        methodsToRemove.Add(m);
                    }
                }

                // remove unused methods
                methodsToRemove.Select(i => c.Methods.Remove(i)).ToList();

                // collect interfaces to remove
                List<InterfaceImplementation> interfacesToRemove = new List<InterfaceImplementation>();

                foreach (var i in c.Interfaces)
                {
                    // remove unused interfaces
                    // because we don't have an interface definition table
                    // have to do it the hard way: search the type definition that contains the interface type
                    var ii = _tablesContext.TypeDefinitionTable.Items.FirstOrDefault(t => t.MetadataToken == i.InterfaceType.MetadataToken);
                    if (ii == null)
                    {
                        interfacesToRemove.Add(i);
                        break;
                    }
                }

                interfacesToRemove.Select(i => c.Interfaces.Remove(i)).ToList();
            }

            // flag minimize completed
            _tablesContext.MinimizeComplete = true;
        }

        private void ShowDependencies(MetadataToken token, HashSet<MetadataToken> set, HashSet<MetadataToken> setTmp)
        {
            var tokenFrom = TokenToString(token);

            foreach (var m in setTmp.OrderBy(mt => mt.ToInt32()))
            {
                if (!set.Contains(m))
                {
                    System.Console.WriteLine($"{tokenFrom} -> {TokenToString(m)}");
                }
            }
        }

        private void ShowDependencies(int token, HashSet<int> set, HashSet<int> setTmp)
        {
            var tokenFrom = TokenToString(token);

            foreach (var m in setTmp.OrderBy(mt => mt))
            {
                if (!set.Contains(m))
                {
                    System.Console.WriteLine($"{tokenFrom} -> {TokenToString(m)}");
                }
            }
        }

        private HashSet<int> BuildDependencyList(int token)
        {
            var tokens = BuildDependencyList(_tablesContext.AssemblyDefinition.MainModule.LookupToken(token).MetadataToken);

            var output = new HashSet<int>();

            var dummy = tokens.Select(t => output.Add(t.ToInt32())).ToList();

            return output;
        }

        private HashSet<MetadataToken> BuildDependencyList(MetadataToken token)
        {
            var set = new HashSet<MetadataToken>();

            switch (token.TokenType)
            {
                case TokenType.TypeRef:
                    var tr = _tablesContext.TypeReferencesTable.Items.FirstOrDefault(i => i.MetadataToken == token);

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

                    Collection<ParameterDefinition> parameters = null;

                    // try to find a method reference
                    var mr = _tablesContext.MethodReferencesTable.Items.FirstOrDefault(i => i.MetadataToken == token);

                    if (mr != null &&
                        mr.ReturnType != null)
                    {
                        parameters = mr.Parameters;

                        if (mr.DeclaringType != null)
                        {
                            set.Add(mr.DeclaringType.MetadataToken);
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
                                set.Add(mr.DeclaringType.MetadataToken);
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
                        foreach (var p in mr.Parameters)
                        {
                            if (p.ParameterType.DeclaringType != null)
                            {
                                var resolvedType = p.ParameterType.Resolve();
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
                        }
                    }

                    if (mr == null)
                    {
                        // try now with field references
                        var fr = _tablesContext.FieldReferencesTable.Items.FirstOrDefault(i => i.MetadataToken == token);

                        if (fr != null)
                        {
                            if (fr.DeclaringType != null)
                            {
                                set.Add(fr.DeclaringType.MetadataToken);
                            }

                            if (fr.FieldType.IsValueType &&
                                !fr.FieldType.IsPrimitive)
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
                                    set.Add(fr.DeclaringType.MetadataToken);
                                }
                            }
                            else if (fr.FieldType.FullName != "System.Void" &&
                                    fr.FieldType.FullName != "System.String" &&
                                    fr.FieldType.FullName != "System.Object" &&
                                    fr.FieldType.IsValueType &&
                                    !fr.FieldType.IsPrimitive)
                            {
                                set.Add(fr.FieldType.MetadataToken);
                            }
                            else if (fr.FieldType.DeclaringType != null)
                            {
                                set.Add(fr.FieldType.DeclaringType.MetadataToken);
                            }
                        }
                    }

                    break;

                case TokenType.TypeSpec:
                    var ts = _tablesContext.TypeSpecificationsTable.TryGetTypeSpecification(token);

                    if(ts != null)
                    {
                        set.Add(token);
                    }

                    break;

                case TokenType.TypeDef:
                    var td = _tablesContext.TypeDefinitionTable.Items.FirstOrDefault(i => i.MetadataToken == token);

                    if (td.BaseType != null)
                    {
                        set.Add(td.BaseType.MetadataToken);
                    }

                    if (td.DeclaringType != null)
                    {
                        set.Add(td.DeclaringType.MetadataToken);
                    }

                    // include attributes
                    foreach(var c in td.CustomAttributes)
                    {
                        if (!nanoTablesContext.ClassNamesToExclude.Contains(c.AttributeType.FullName) && 
                            c.AttributeType.IsToInclude())
                        {
                            set.Add(c.AttributeType.MetadataToken);
                        }
                    }

                    // fields
                    var tdFields = td.Fields.Where(f => !f.IsLiteral);

                    foreach (var f in tdFields)
                    {
                        if (!nanoTablesContext.ClassNamesToExclude.Contains(f.DeclaringType.FullName))
                        {
                            set.Add(f.MetadataToken);
                        }
                    }

                    // methods
                    foreach (var m in td.Methods)
                    {
                        if (!nanoTablesContext.ClassNamesToExclude.Contains(m.DeclaringType.FullName))
                        {
                            set.Add(m.MetadataToken);
                        }
                    }

                    // interfaces
                    foreach (var i in td.Interfaces)
                    {
                        if (!nanoTablesContext.ClassNamesToExclude.Contains(i.InterfaceType.FullName))
                        {
                            set.Add(i.MetadataToken);
                        }
                    }

                    break;

                case TokenType.Field:
                    var fd = _tablesContext.FieldsTable.Items.FirstOrDefault(i => i.MetadataToken == token);

                    if (fd is TypeReference)
                    {
                        set.Add(fd.MetadataToken);
                    }
                    else if (fd.FieldType.IsValueType &&
                             !fd.FieldType.IsPrimitive)
                    {
                        set.Add(fd.FieldType.MetadataToken);
                    }
                    else if (fd.FieldType.IsArray)
                    {
                        if (fd.FieldType.DeclaringType != null)
                        {
                            set.Add(fd.FieldType.DeclaringType.MetadataToken);
                        }
                        else
                        {
                            set.Add(fd.DeclaringType.MetadataToken);
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
                    foreach (var c in fd.CustomAttributes)
                    {
                        if (!nanoTablesContext.ClassNamesToExclude.Contains(c.AttributeType.FullName) &&
                            c.AttributeType.IsToInclude())
                        {   
                            set.Add(c.Constructor.MetadataToken);
                        }
                    }

                    break;

                case TokenType.Method:
                    var md = _tablesContext.MethodDefinitionTable.Items.FirstOrDefault(i => i.MetadataToken == token);

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
                            set.Add(md.DeclaringType.MetadataToken);
                        }
                    }
                    else if (!md.ReturnType.IsValueType &&
                            !md.ReturnType.IsPrimitive &&
                            md.ReturnType.FullName != "System.Void" &&
                            md.ReturnType.FullName != "System.String" &&
                            md.ReturnType.FullName != "System.Object")
                    {
                        set.Add(md.ReturnType.MetadataToken);
                    }

                    // parameters
                    foreach (var p in md.Parameters)
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

                        if (parameterType.DeclaringType != null)
                        {
                            set.Add(parameterType.DeclaringType.MetadataToken);
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
                    }

                    if (md.HasBody)
                    {
                        // variables
                        foreach (var v in md.Body.Variables)
                        {
                            if (v.VariableType.DeclaringType != null)
                            {
                                set.Add(v.VariableType.DeclaringType.MetadataToken);
                            }
                            else if (v.VariableType.MetadataType == MetadataType.Class)
                            {
                                set.Add(v.VariableType.MetadataToken);
                            }
                            else if (v.VariableType.IsValueType &&
                                    !v.VariableType.IsPrimitive)
                            {
                                set.Add(v.VariableType.MetadataToken);
                            }
                        }

                        // op codes
                        foreach (var i in md.Body.Instructions)
                        {
                            if (i.Operand is MethodReference ||
                                i.Operand is FieldReference ||
                                i.Operand is TypeDefinition ||
                                i.Operand is TypeSpecification ||
                                i.Operand is TypeReference)
                            {
                                set.Add(((IMetadataTokenProvider)i.Operand).MetadataToken);
                            }
                            else if (i.Operand is string)
                            {
                                var stringId = _tablesContext.StringTable.GetOrCreateStringId((string)i.Operand);

                                var newToken = new MetadataToken(TokenType.String, stringId);

                                set.Add(newToken);
                            }

                        }
                   
                        // exceptions
                        foreach (var e in md.Body.ExceptionHandlers)
                        {
                            if(e.HandlerType ==  Mono.Cecil.Cil.ExceptionHandlerType.Filter)
                            {
                                set.Add(((IMetadataTokenProvider)e.FilterStart.Operand).MetadataToken);
                            }
                        }
                    }

                    // attributes
                    foreach (var c in md.CustomAttributes)
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
                    foreach (var t in _tablesContext.TypeDefinitionTable.Items)
                    {
                        var ii1 = t.Interfaces.FirstOrDefault(i => i.MetadataToken == token);
                        if (ii1 != null)
                        {
                            set.Add(ii1.InterfaceType.MetadataToken);
                            set.Add(t.MetadataToken);

                            break;
                        }
                    }

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
                    var tr = _tablesContext.TypeReferencesTable.Items.FirstOrDefault(i => i.MetadataToken == token);

                    if(tr.Scope != null)
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
                    var td = _tablesContext.TypeDefinitionTable.Items.FirstOrDefault(i => i.MetadataToken == token);

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
                    var fd = _tablesContext.FieldsTable.Items.FirstOrDefault(i => i.MetadataToken == token);

                    if (fd.DeclaringType != null)
                    {
                        output.Append(TokenToString(fd.DeclaringType.MetadataToken));
                        output.Append("::");
                    }

                    output.Append(fd.Name);
                    break;

                case TokenType.Method:
                    var md = _tablesContext.MethodDefinitionTable.Items.FirstOrDefault(i => i.MetadataToken == token);

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
                    foreach (var t in _tablesContext.TypeDefinitionTable.Items)
                    {
                        var ii = t.Interfaces.FirstOrDefault(i => i.MetadataToken == token);
                        if (ii != null)
                        {
                            var classToken = TokenToString(t.MetadataToken);
                            var interfaceToken = TokenToString(ii.InterfaceType.MetadataToken);

                            output.Append($"[{classToken} implements {interfaceToken}]");

                            break;
                        }
                    }

                    break;

                case TokenType.MemberRef:

                    TypeReference typeRef = null;
                    string typeName = string.Empty;

                    // try to find a method reference
                    var mr = _tablesContext.MethodReferencesTable.Items.FirstOrDefault(i => i.MetadataToken == token);

                    if (mr != null)
                    {
                        typeRef = mr.DeclaringType;
                        typeName = mr.Name;
                    }
                    else
                    {
                        // try now with field references
                        var fr = _tablesContext.FieldReferencesTable.Items.FirstOrDefault(i => i.MetadataToken == token);

                        if (fr != null)
                        {
                            typeRef = fr.DeclaringType;
                            typeName = fr.Name;
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
                    var ar = _tablesContext.AssemblyReferenceTable.Items.FirstOrDefault(i => i.MetadataToken == token);

                    output.Append($"[{ar.Name}]");
                    break;

                case TokenType.String:
                    var sr = _tablesContext.StringTable.TryGetString((ushort)token.RID);

                    if (sr != null)
                    {
                        output.Append($"'{sr}'");
                    }
                    break;
            }

            // output token ID if empty
            if (output.Length == 0)
            {
                output.Append($"[0x{token.ToUInt32().ToString("X8")}]");
            }

            return output.ToString();
        }

        public void Write(
            XmlWriter xmlWriter)
        {
            var pdbxWriter = new nanoPdbxFileWriter(_tablesContext);
            pdbxWriter.Write(xmlWriter);
        }

        private static IEnumerable<InanoTable> GetTables(
            nanoTablesContext context)
        {
            yield return context.AssemblyReferenceTable;

            yield return context.TypeReferencesTable;

            yield return context.FieldReferencesTable;

            yield return context.MethodReferencesTable;

            yield return context.TypeDefinitionTable;

            yield return context.FieldsTable;

            yield return context.MethodDefinitionTable;

            yield return context.AttributesTable;

            yield return context.TypeSpecificationsTable;

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
