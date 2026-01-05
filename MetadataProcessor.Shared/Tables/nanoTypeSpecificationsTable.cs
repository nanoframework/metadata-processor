// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Original work from Oleg Rakhmatulin.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Encapsulates logic for storing type specifications list and writing this
    /// list into target assembly in .NET nanoFramework format.
    /// </summary>
    public sealed class nanoTypeSpecificationsTable : InanoTable
    {

        //////////////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////////////
        // <SYNC-WITH-NATIVE>                                                                       //
        // when updating this size here need to update matching define in nanoCLR_Types.h in native //
        private const int sizeOf_CLR_RECORD_TYPESPEC = 2;
        //////////////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Helper class for comparing two instances of <see cref="TypeSpecification"/> objects
        /// using <see cref="TypeSpecification.MetadataToken"/> property as unique key for comparison.
        /// </summary>
        private sealed class TypeSpecificationEqualityComparer : IEqualityComparer<TypeSpecification>
        {
            /// <inheritdoc/>
            public bool Equals(TypeSpecification x, TypeSpecification y)
            {
                if (x is null)
                {
                    throw new ArgumentNullException(nameof(x));
                }

                if (y is null)
                {
                    throw new ArgumentNullException(nameof(y));
                }

                return x.MetadataToken.Equals(y.MetadataToken);
            }

            /// <inheritdoc/>
            public int GetHashCode(TypeSpecification obj)
            {
                if (obj is null)
                {
                    throw new ArgumentNullException(nameof(obj));
                }

                return obj.MetadataToken.GetHashCode();
            }
        }

        private sealed class TypeSpecBySignatureComparer : IEqualityComparer<KeyValuePair<ushort, TypeReference>>
        {
            public bool Equals(KeyValuePair<ushort, TypeReference> x, KeyValuePair<ushort, TypeReference> y)
            {
                return x.Key == y.Key;
            }

            /// <inheritdoc/>
            public int GetHashCode(KeyValuePair<ushort, TypeReference> that)
            {
                return that.Key;
            }
        }

        /// <summary>
        /// Maps for each unique type specification and related identifier.
        /// </summary>
        private Dictionary<TypeReference, ushort> _idByTypeSpecifications;

        /// <summary>
        /// Maps type reference to its index in the table (insertion order).
        /// </summary>
        private Dictionary<TypeReference, ushort> _indexByTypeReference;

        /// <summary>
        /// Assembly tables context - contains all tables used for building target assembly.
        /// </summary>
        private readonly nanoTablesContext _context;

        public NanoClrTable TableIndex => NanoClrTable.TBL_TypeSpec;

        /// <summary>
        /// Creates new instance of <see cref="nanoTypeSpecificationsTable"/> object.
        /// </summary>
        /// <param name="context">
        /// Assembly tables context - contains all tables used for building target assembly.
        /// </param>
        public nanoTypeSpecificationsTable(
            nanoTablesContext context)
        {
            _context = context;

            _idByTypeSpecifications = new Dictionary<TypeReference, ushort>(new TypeReferenceEqualityComparer(context));
            _indexByTypeReference = new Dictionary<TypeReference, ushort>(new TypeReferenceEqualityComparer(context));

            AddTypeLevelGenericParameters();
            FillTypeSpecsFromTypes();
            FillTypeSpecsFromMemberReferences();
        }

        /// <summary>
        /// Gets type specification identifier.
        /// </summary>
        /// <param name="typeReference">Type reference in Mono.Cecil format.</param>
        /// <param name="referenceId">Type Specification identifier for filling.</param>
        /// <returns>Returns <c>true</c> if item found, otherwise returns <c>false</c>.</returns>
        /// <summary>
        /// Gets type specification identifier.
        /// </summary>
        /// <param name="typeReference">Type reference in Mono.Cecil format.</param>
        /// <param name="referenceId">Type Specification identifier for filling.</param>
        /// <returns>Returns <c>true</c> if item found, otherwise returns <c>false</c>.</returns>
        public bool TryGetTypeReferenceId(
            TypeReference typeReference,
            out ushort referenceId)
        {
            referenceId = 0;

            // sanity check for bug in Mono.Cecil where TypeSpecification instances may show with RID = 0
            if (typeReference is TypeSpecification && typeReference.MetadataToken.RID == 0)
            {

                // don't add this invalid entry
                return false;
            }

            if (_indexByTypeReference != null && _indexByTypeReference.TryGetValue(typeReference, out referenceId))
            {
                return true;
            }

            return false;
        }

        public TypeReference TryGetTypeSpecification(MetadataToken token)
        {
            // try a direct match on the TypeReference itself
            TypeReference direct = _idByTypeSpecifications.Keys.FirstOrDefault(t => t.MetadataToken == token);

            if (direct != null)
            {
                return direct;
            }

            // Look among the generic-instances already seeded
            GenericInstanceType genericInst = _idByTypeSpecifications.Keys
                .OfType<GenericInstanceType>()
                .FirstOrDefault(git => git.ElementType.MetadataToken == token);

            if (genericInst != null)
            {
                return genericInst;
            }

            // maybe this instanced type is in TypeReferencesTable
            GenericInstanceType external = _context.TypeReferencesTable.Items
                .OfType<GenericInstanceType>()
                .FirstOrDefault(git => git.ElementType.MetadataToken == token);

            if (external != null)
            {
                // seed it now so future lookups find it immediately
                ushort sigId = _context.SignaturesTable.GetOrCreateSignatureId(external);

                AddIfNew(external, sigId);

                // and pull in its nested specs (generic arguments, element types, etc.)
                ExpandNestedTypeSpecs(external);

                return external;
            }

            // some edge case not being handled...
            // default to null
            return null;
        }


        /// <summary>
        /// Tries to find type reference by the index on the <see cref="TypeSpec"/> list.
        /// </summary>
        /// <param name="index">Index of the type reference in the list.</param>
        /// <returns>Returns the type reference if found, otherwise returns <c>null</c>.</returns>
        public TypeReference TryGetTypeReferenceByIndex(ushort index)
        {
            if (index >= _idByTypeSpecifications.Count)
            {
                return null;
            }

            return _idByTypeSpecifications.ElementAt(index).Key;
        }

        /// <inheritdoc/>
        public void Write(
            nanoBinaryWriter writer)
        {

            foreach (var item in _idByTypeSpecifications)
            {
                var writerStartPosition = writer.BaseStream.Position;

                writer.WriteUInt16(item.Value);

                var writerEndPosition = writer.BaseStream.Position;

                Debug.Assert((writerEndPosition - writerStartPosition) == sizeOf_CLR_RECORD_TYPESPEC);
            }
        }

        public void ForEachItems(Action<uint, TypeReference> action)
        {
            foreach (var item in _idByTypeSpecifications)
            {
                action(item.Value, item.Key);
            }
        }

        private void FillTypeSpecsFromMemberReferences()
        {
            List<TypeSpecification> typeSpecs = new List<TypeSpecification>();

            foreach (var m in _context.MemberReferencesTable.Items.Where(mr => mr.DeclaringType is TypeSpecification))
            {
                if (!typeSpecs.Contains(m.DeclaringType as TypeSpecification, new TypeSpecificationEqualityComparer()))
                {
                    // check for array in TypeSpec because we don't support for multidimensional arrays
                    if (m.DeclaringType.IsArray &&
                        (m.DeclaringType as ArrayType).Rank > 1)
                    {
                        throw new ArgumentException($".NET nanoFramework doesn't have support for multidimensional arrays. Unable to parse {m.DeclaringType.FullName}.");
                    }

                    if (!(m.DeclaringType as TypeSpecification).IsToExclude())
                    {
                        typeSpecs.Add(m.DeclaringType as TypeSpecification);

                        // get index of signature for the TypeSpecification 
                        ushort signatureId = _context.SignaturesTable.GetOrCreateSignatureId(m.DeclaringType);

                        if (!_idByTypeSpecifications.TryGetValue(m.DeclaringType, out ushort referenceId))
                        {
                            // is not on the list yet, add it
                            _idByTypeSpecifications.Add(m.DeclaringType, signatureId);
                        }
                    }
                }
            }

            // make sure we pick up *all* GenericInstanceType entries
            // that may have come in via the TypeReferencesTable.
            foreach (GenericInstanceType genericInstanceType in _context.TypeReferencesTable.Items.OfType<GenericInstanceType>())
            {
                if (!_idByTypeSpecifications.ContainsKey(genericInstanceType) && !genericInstanceType.IsToExclude())
                {
                    // create or get the signature ID for this instanced type
                    ushort sigId = _context.SignaturesTable.GetOrCreateSignatureId(genericInstanceType);
                    _idByTypeSpecifications.Add(genericInstanceType, sigId);

                    // (and don’t forget to pull in any nested generic-parameter args)
                    foreach (GenericParameter arg in genericInstanceType.GenericArguments.OfType<GenericParameter>())
                    {
                        if (!_idByTypeSpecifications.ContainsKey(arg) && !arg.IsToExclude())
                        {
                            ushort argSig = _context.SignaturesTable.GetOrCreateSignatureId(arg);
                            _idByTypeSpecifications.Add(arg, argSig);
                        }
                    }
                }
            }

            // make sure we pick up *all* GenericInstanceType that we know
            // about via MethodSpecificationTable as well as TypeReferencesTable
            IEnumerable<TypeReference> allGenericInstances =
                _context.MethodSpecificationTable.Items
                    .OfType<GenericInstanceMethod>()
                    .Select(ms => ms.DeclaringType as GenericInstanceType)
                .Concat(_context.TypeReferencesTable.Items.OfType<GenericInstanceType>())
                .Where(git => git != null)
                .Distinct(new TypeReferenceEqualityComparer(_context));

            foreach (TypeReference typeRefItem in allGenericInstances)
            {
                if (!_idByTypeSpecifications.ContainsKey(typeRefItem) && !typeRefItem.IsToExclude())
                {
                    ushort sigId = _context.SignaturesTable.GetOrCreateSignatureId(typeRefItem);
                    _idByTypeSpecifications.Add(typeRefItem, sigId);
                    ExpandNestedTypeSpecs(typeRefItem);
                }
            }
        }

        private void FillTypeSpecsFromTypes()
        {
            foreach (TypeDefinition td in _context.TypeDefinitionTable.Items)
            {
                // Fields of the type
                foreach (var field in td.Fields.Where(f => !f.IsLiteral))
                {
                    if (field.FieldType is ArrayType array && array.ElementType is GenericParameter)
                    {
                        AddIfNew(field.FieldType, _context.SignaturesTable.GetOrCreateSignatureId(field.FieldType));
                    }
                }

                foreach (MethodDefinition m in td.Methods.Where(m => m.HasBody))
                {
                    foreach (VariableDefinition variable in m.Body.Variables)
                    {
                        ExpandNestedTypeSpecs(variable.VariableType);
                    }

                    foreach (Instruction instr in m.Body.Instructions)
                    {
                        if (instr.Operand is GenericInstanceMethod genericInstanceMethod)
                        {
                            GenericInstanceType genericInstanceType = genericInstanceMethod.DeclaringType as GenericInstanceType;
                            if (genericInstanceType != null
                                && !_idByTypeSpecifications.ContainsKey(genericInstanceType)
                                && !genericInstanceType.IsToExclude())
                            {
                                ushort sigId = _context.SignaturesTable.GetOrCreateSignatureId(genericInstanceType);
                                _idByTypeSpecifications.Add(genericInstanceType, sigId);

                                // also pull in its element‐type and args
                                ExpandNestedTypeSpecs(genericInstanceType);
                            }

                            // capture the *return‐type* of the instantiation (e.g. T[] for Array.Empty<T>())
                            TypeReference returnType = genericInstanceMethod.ReturnType;
                            ExpandNestedTypeSpecs(returnType);

                            if (returnType is ArrayType)
                            {
                                if (!_idByTypeSpecifications.ContainsKey(returnType.GetElementType())
                                    && !returnType.GetElementType().IsToExclude())
                                {
                                    AddIfNew(returnType.GetElementType(), _context.SignaturesTable.GetOrCreateSignatureId(returnType.GetElementType()));
                                }
                            }
                        }
                        else if (instr.Operand is MethodReference mr)
                        {
                            // register return‐type...
                            ExpandNestedTypeSpecs(mr.ReturnType);

                            // ... and parameters
                            foreach (ParameterDefinition p in mr.Parameters)
                            {
                                ExpandNestedTypeSpecs(p.ParameterType);
                            }
                        }
                        else if (instr.Operand is GenericParameter gp)
                        {
                            AddIfNew(gp, _context.SignaturesTable.GetOrCreateSignatureId(gp));
                        }
                        // catch field‐refs too
                        else if (instr.Operand is FieldReference fieldRef)
                        {
                            ExpandNestedTypeSpecs(fieldRef.DeclaringType);
                            ExpandNestedTypeSpecs(fieldRef.FieldType);
                        }
                        else if (instr.Operand is TypeReference tr)
                        {
                            // refuse multi-dimensional arrays (we only support jagged arrays)
                            if (tr.IsArray)
                            {
                                var at = (ArrayType)tr;

                                if (at.Rank > 1)
                                {
                                    throw new ArgumentException(
                                        $".NET nanoFramework only supports jagged arrays: {tr.FullName}");
                                }
                            }

                            // register the type reference itself, if it is a TypeSpec
                            if (tr is TypeSpecification)
                            {
                                ushort sigId = _context.SignaturesTable.GetOrCreateSignatureId(tr);
                                AddIfNew(tr, sigId);

                                // also walk into any nested TypeSpecifications it might contain
                                ExpandNestedTypeSpecs(tr);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Recursively finds any TypeSpecification bits of 't' and adds them
        /// (element types, generic arguments, declaring types, by/ref, pointers, etc.)
        /// </summary>
        private void ExpandNestedTypeSpecs(TypeReference t)
        {
            if (!(t is TypeSpecification ts))
            {
                return;
            }

            // element type of pointers, by-refs, modifiers, arrays, & generic definitions
            TypeReference inner = null;
            switch (ts)
            {
                case GenericInstanceType git:
                    inner = git.ElementType;
                    foreach (var arg in git.GenericArguments)
                    {
                        ExpandNestedTypeSpecs(arg);
                    }

                    ushort declId = _context.SignaturesTable.GetOrCreateSignatureId(git);
                    AddIfNew(git, declId);

                    break;

                case ArrayType at:
                    inner = at.ElementType;
                    break;

                case ByReferenceType br:
                    inner = br.ElementType;
                    break;

                case PointerType pt:
                    inner = pt.ElementType;
                    break;

                case OptionalModifierType om:
                    inner = om.ElementType;
                    break;

                case RequiredModifierType rm:
                    inner = rm.ElementType;
                    break;
            }

            if (inner is TypeSpecification)
            {
                ushort innerId = _context.SignaturesTable.GetOrCreateSignatureId(inner);
                AddIfNew(inner, innerId);
                ExpandNestedTypeSpecs(inner);
            }

            // nested/declaring types
            if (ts.DeclaringType != null)
            {
                TypeReference decl = ts.DeclaringType;
                ushort declId = _context.SignaturesTable.GetOrCreateSignatureId(decl);
                AddIfNew(decl, declId);
                ExpandNestedTypeSpecs(decl);
            }
        }

        /// <summary>
        /// Helper to add to `_idByTypeSpecifications` only if we haven’t already seen it
        /// </summary>
        private void AddIfNew(
            TypeReference tr,
            ushort sigId)
        {
            // sanity check for bug in Mono.Cecil where TypeSpecification instances may show with RID = 0
            if (tr is TypeSpecification && tr.MetadataToken.RID == 0)
            {
                // don't add this invalid entry
                return;
            }

            if (!tr.IsToExclude() && !_idByTypeSpecifications.ContainsKey(tr))
            {
                ushort index = (ushort)_idByTypeSpecifications.Count;
                _idByTypeSpecifications.Add(tr, sigId);
                _indexByTypeReference.Add(tr, index);
            }
        }

        private void AddTypeLevelGenericParameters()
        {
            foreach (TypeDefinition td in _context.TypeDefinitionTable.Items.Where(t => t.HasGenericParameters))
            {
                // register each generic parameter (T)
                foreach (GenericParameter gp in td.GenericParameters)
                {
                    ushort gpSig = _context.SignaturesTable.GetOrCreateSignatureId(gp);
                    AddIfNew(gp, gpSig);
                }
            }
        }

        internal void RemoveEmptyItems()
        {
            var itemsToRemove = new List<TypeReference>();

            foreach (var kvp in _idByTypeSpecifications.ToList())
            {
                TypeReference typeReference = kvp.Key;
                ushort sigId = kvp.Value;

                // Get the index of this TypeSpec in the collection
                if (!TryGetTypeReferenceId(typeReference, out ushort index))
                {
                    continue;
                }

                bool hasMemberReferences = false;

                // Check if this TypeSpec has any member references
                if (typeReference is GenericParameter)
                {
                    // Generic parameters might be referenced without explicit member refs
                    // Keep them for now
                    continue;
                }
                else if (typeReference.IsArray)
                {
                    // Arrays might be referenced without explicit member refs
                    // Keep them for now
                    continue;
                }
                else if (typeReference is ByReferenceType || typeReference is PointerType)
                {
                    // ByRef and Pointer types might be referenced without explicit member refs
                    // Keep them for now
                    continue;
                }
                else if (typeReference is GenericInstanceType genericInstanceType)
                {
                    // Check MethodReferencesTable
                    foreach (MethodReference mr in _context.MethodReferencesTable.Items)
                    {
                        if (TryGetTypeReferenceId(mr.DeclaringType, out ushort referenceId)
                            && referenceId == index
                            && _context.MethodReferencesTable.TryGetMethodReferenceId(mr, out ushort _))
                        {
                            hasMemberReferences = true;
                            break;
                        }
                    }

                    // Check MethodSpecificationTable if no refs found yet
                    if (!hasMemberReferences)
                    {
                        foreach (MethodSpecification ms in _context.MethodSpecificationTable.Items)
                        {
                            if (TryGetTypeReferenceId(ms.DeclaringType, out ushort referenceId)
                                && referenceId == index
                                && _context.MethodSpecificationTable.TryGetMethodSpecificationId(ms, out ushort _))
                            {
                                hasMemberReferences = true;
                                break;
                            }
                        }
                    }

                    // Check FieldReferencesTable if no refs found yet
                    if (!hasMemberReferences)
                    {
                        foreach (FieldReference fr in _context.FieldReferencesTable.Items)
                        {
                            if (TryGetTypeReferenceId(fr.DeclaringType, out ushort referenceId)
                                && referenceId == index
                                && _context.FieldReferencesTable.TryGetFieldReferenceId(fr, out ushort _))
                            {
                                hasMemberReferences = true;
                                break;
                            }
                        }
                    }

                    // Check if the ElementType is a TypeDefinition with methods/fields
                    if (!hasMemberReferences && genericInstanceType.ElementType is TypeDefinition definition)
                    {
                        // Check if any of the definition's methods are in MethodDefinitionTable
                        foreach (MethodDefinition md in definition.Methods)
                        {
                            if (_context.MethodDefinitionTable.TryGetMethodReferenceId(md, out ushort _))
                            {
                                hasMemberReferences = true;
                                break;
                            }
                        }

                        // Check if any of the definition's fields are in FieldsTable
                        if (!hasMemberReferences)
                        {
                            foreach (FieldDefinition fd in definition.Fields)
                            {
                                if (_context.FieldsTable.TryGetFieldDefinitionId(fd, false, out ushort _))
                                {
                                    hasMemberReferences = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    // For other TypeSpecification types (arrays, pointers, etc.)
                    // check member references tables
                    foreach (MemberReference mr in _context.MemberReferencesTable.Items)
                    {
                        try
                        {
                            if (TryGetTypeReferenceId(mr.DeclaringType, out ushort referenceId) && referenceId == index)
                            {
                                hasMemberReferences = true;
                                break;
                            }
                        }
                        catch
                        {
                            // ignore errors here, as the TypeSpec might not be available
                        }
                    }
                }

                if (!hasMemberReferences)
                {
                    itemsToRemove.Add(typeReference);
                }
            }

            // remove items that have no member references and rebuild index
            foreach (TypeReference item in itemsToRemove)
            {
                _idByTypeSpecifications.Remove(item);
                _indexByTypeReference.Remove(item);
            }

            // rebuild the index mapping after removal
            _indexByTypeReference.Clear();

            ushort newIndex = 0;

            foreach (var kvp in _idByTypeSpecifications)
            {
                _indexByTypeReference.Add(kvp.Key, newIndex++);
            }
        }
    }
}
