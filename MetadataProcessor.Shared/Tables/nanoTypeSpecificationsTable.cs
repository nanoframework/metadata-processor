// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Original work from Oleg Rakhmatulin.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

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

                return string.Equals(x.MetadataToken, y.MetadataToken);
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
        public bool TryGetTypeReferenceId(
            TypeReference typeReference,
            out ushort referenceId)
        {
            if (_idByTypeSpecifications.TryGetValue(typeReference, out referenceId))
            {
                referenceId = (ushort)Array.IndexOf(_idByTypeSpecifications.Values.ToArray(), referenceId);

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

            // make sure we pick up *all* GenericInstanceType entries
            // that may have come in via the TypeReferencesTable.
            foreach (GenericInstanceType genericInstanceType in _context.TypeReferencesTable.Items.OfType<GenericInstanceType>())
            {
                if (!_idByTypeSpecifications.ContainsKey(genericInstanceType))
                {
                    // create or get the signature ID for this instanced type
                    ushort sigId = _context.SignaturesTable.GetOrCreateSignatureId(genericInstanceType);
                    _idByTypeSpecifications.Add(genericInstanceType, sigId);

                    // (and don’t forget to pull in any nested generic-parameter args)
                    foreach (GenericParameter arg in genericInstanceType.GenericArguments.OfType<GenericParameter>())
                    {
                        if (!_idByTypeSpecifications.ContainsKey(arg))
                        {
                            ushort argSig = _context.SignaturesTable.GetOrCreateSignatureId(arg);
                            _idByTypeSpecifications.Add(arg, argSig);
                        }
                    }
                }
            }
        }

        private void FillTypeSpecsFromTypes()
        {
            foreach (TypeDefinition td in _context.TypeDefinitionTable.Items)
            {
                foreach (MethodDefinition m in td.Methods.Where(m => m.HasBody))
                {
                    foreach (Instruction instr in m.Body.Instructions)
                    {
                        if (instr.Operand is GenericParameter gp)
                        {
                            AddIfNew(gp, _context.SignaturesTable.GetOrCreateSignatureId(gp));
                        }
                        else if (instr.Operand is TypeReference tr)
                        {
                            // refuse multi-dimensional arrays
                            // we only support jagged arrays
                            if (tr.IsArray)
                            {
                                var at = (ArrayType)tr;

                                if (at.Rank > 1)
                                {
                                    throw new ArgumentException(
                                        $".NET nanoFramework only supports jagged arrays: {tr.FullName}");
                                }
                            }

                            // register the type reference itself...
                            ushort sigId = _context.SignaturesTable.GetOrCreateSignatureId(tr);
                            AddIfNew(tr, sigId);

                            // ... then walk *into* any nested TypeSpecifications it might contain
                            ExpandNestedTypeSpecs(tr);
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
                        ExpandNestedTypeSpecs(arg);
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

            if (inner != null)
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
            if (!_idByTypeSpecifications.ContainsKey(tr))
            {
                _idByTypeSpecifications.Add(tr, sigId);
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

                // seed the *open* GenericInstanceType (e.g. Action`1<T>)
                var openGeneric = new GenericInstanceType(td);
                foreach (GenericParameter gp in td.GenericParameters)
                {
                    openGeneric.GenericArguments.Add(gp);
                }

                ushort openSig = _context.SignaturesTable.GetOrCreateSignatureId(openGeneric);
                AddIfNew(openGeneric, openSig);
            }
        }
    }
}
