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
            return _idByTypeSpecifications.FirstOrDefault(typeEntry => typeEntry.Key.MetadataToken == token).Key;
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
        }

        private void FillTypeSpecsFromTypes()
        {
            foreach (TypeDefinition t in _context.TypeDefinitionTable.Items)
            {
                foreach (MethodDefinition m in t.Methods.Where(method => method.HasBody))
                {
                    foreach (Instruction instruction in m.Body.Instructions)
                    {
                        if (instruction.Operand is GenericParameter genericParameter)
                        {
                            ushort signatureId = _context.SignaturesTable.GetOrCreateSignatureId(genericParameter);

                            if (!_idByTypeSpecifications.ContainsKey(genericParameter))
                            {
                                _idByTypeSpecifications.Add(genericParameter, signatureId);
                            }
                        }
                        else if (instruction.Operand is TypeReference typeReference)
                        {
                            // Optional: Check additional conditions if needed,
                            // for example, if the operand type should be an array.
                            if (instruction.OpCode.OperandType == OperandType.InlineType && !typeReference.IsArray)
                            {
                                continue;
                            }

                            ushort signatureId = _context.SignaturesTable.GetOrCreateSignatureId(typeReference);

                            if (!_idByTypeSpecifications.ContainsKey(typeReference))
                            {
                                _idByTypeSpecifications.Add(typeReference, signatureId);
                            }
                        }
                    }
                }
            }
        }
    }
}
