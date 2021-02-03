//
// Copyright (c) .NET Foundation and Contributors
// Original work from Oleg Rakhmatulin.
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Encapsulates logic for storing type specifications list and writing this
    /// list into target assembly in .NET nanoFramework format.
    /// </summary>
    public sealed class nanoTypeSpecificationsTable : InanoTable
    {
        private const int sizeOf_CLR_RECORD_TYPESPEC = 2;

        /// <summary>
        /// Helper class for comparing two instances of <see cref="TypeReference"/> objects
        /// using <see cref="TypeReference.FullName"/> property as unique key for comparison.
        /// </summary>
        private sealed class TypeReferenceComparer : IEqualityComparer<TypeReference>
        {
            /// <inheritdoc/>
            public bool Equals(TypeReference x, TypeReference y)
            {
                if (x is null)
                {
                    throw new ArgumentNullException(nameof(x));
                }

                if (y is null)
                {
                    throw new ArgumentNullException(nameof(y));
                }

                return x.MetadataToken == y.MetadataToken;
            }

            /// <inheritdoc/>
            public int GetHashCode(TypeReference that)
            {
                return that.MetadataToken.GetHashCode();
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
        private Dictionary<TypeReference, ushort> _idByTypeSpecifications =
            new Dictionary<TypeReference, ushort>(new TypeReferenceComparer());


        /// <summary>
        /// Maps for each unique type specification and related identifier.
        /// </summary>
        private List<ushort> _idSignatures = new List<ushort>();

        /// <summary>
        /// Assembly tables context - contains all tables used for building target assembly.
        /// </summary>
        private readonly nanoTablesContext _context;

        public nanoClrTable TableIndex => nanoClrTable.TBL_TypeSpec;

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
        }

        /// <summary>
        /// Gets existing or creates new type specification reference identifier.
        /// </summary>
        /// <param name="typeReference">Type reference value for obtaining identifier.</param>
        /// <returns>Existing identifier if specification already in table or new one.</returns>
        public ushort GetOrCreateTypeSpecificationId(
            TypeReference typeReference)
        {
            // get index of signature for the TypeSpecification 
            ushort signatureId = _context.SignaturesTable.GetOrCreateSignatureId(typeReference);
            
            if (!_idByTypeSpecifications.TryGetValue(typeReference, out ushort referenceId))
            {
                // check for array in TypeSpec because we don't support for multidimensional arrays
                if (typeReference.IsArray &&
                    (typeReference as ArrayType).Rank > 1)
                {
                    throw new ArgumentException($".NET nanoFramework doesn't have support for multidimensional arrays. Unable to parse {typeReference.FullName}.");
                }

                _idByTypeSpecifications.Add(typeReference, _lastAvailableId);

                if(!_idSignatures.Contains(signatureId))
                {
                    _idSignatures.Add(signatureId);

                    // if this is a generic type instance, add it to type definitions too
                    if (typeReference.IsGenericInstance)
                    {
                        _context.TypeDefinitionTable.AddGenericInstanceType(typeReference);
                    }
                }
            }

            return (ushort)_idSignatures.IndexOf(signatureId);
        }

        /// <summary>
        /// Gets type specification identifier (if it already added into type specifications list).
        /// </summary>
        /// <param name="typeReference">Type reference in Mono.Cecil format.</param>
        /// <param name="referenceId">Type reference identifier for filling.</param>
        /// <returns>Returns <c>true</c> if item found, otherwise returns <c>false</c>.</returns>
        public bool TryGetTypeReferenceId(
            TypeReference typeReference,
            out ushort referenceId)
        {
            if(_idByTypeSpecifications.TryGetValue(typeReference, out referenceId))
            {
                referenceId = (ushort)_idSignatures.IndexOf(referenceId);

                return true;
            }

            return false;
        }

        public TypeReference TryGetTypeSpecification(MetadataToken token)
        {
            foreach (var t in _idByTypeSpecifications)
            {
                if(t.Key.MetadataToken == token)
                {
                    return t.Key;
                }
            }

            return null;
        }

        /// <inheritdoc/>
        public void Write(
            nanoBinaryWriter writer)
        {

            foreach (var item in GetItems())
            {
                var writerStartPosition = writer.BaseStream.Position;

                writer.WriteUInt16(item.Key);

                var writerEndPosition = writer.BaseStream.Position;

                Debug.Assert((writerEndPosition - writerStartPosition) == sizeOf_CLR_RECORD_TYPESPEC);
            }
        }

        public void ForEachItems(Action<uint, TypeReference> action)
        {
            foreach (var item in _idByTypeSpecifications.Select(
                i => new KeyValuePair<ushort, TypeReference>(
                    (ushort)_idSignatures.IndexOf(i.Value),
                    i.Key)).Distinct(new TypeSpecBySignatureComparer()))
            {
                action(item.Key, item.Value);
            }
        }

        public IEnumerable<KeyValuePair<ushort, TypeReference>> GetItems()
        {
            return _idByTypeSpecifications.Select(
                i => new KeyValuePair<ushort, TypeReference>(
                    (ushort)_idSignatures.IndexOf(i.Value),
                    i.Key)).Distinct(new TypeSpecBySignatureComparer());
        }

        internal void RemoveUnusedItems(HashSet<MetadataToken> set)
        {
            // build a collection of the current items that are present in the used items set
            List<TypeReference> usedItems = new List<TypeReference>();

            foreach (var item in _idByTypeSpecifications
                                    .Where(item => set.Contains(item.Key.MetadataToken)))
            {
                usedItems.Add(item.Key);
            }

            // reset dictionary
            _idByTypeSpecifications = new Dictionary<TypeReference, ushort>(
                new TypeReferenceComparer());

            // reset list
            _idSignatures = new List<ushort>();

            // rebuild
            foreach (var item in usedItems)
            {
                GetOrCreateTypeSpecificationId(item);
            }
        }
    }
}
