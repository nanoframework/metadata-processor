//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using System;
using System.Collections.Generic;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Encapsulates logic for storing method specifications list and writing
    /// this collected list into target assembly in .NET nanoFramework format.
    /// </summary>
    public sealed class nanoMethodSpecificationTable :
        nanoReferenceTableBase<MethodSpecification>
    {
        /// <summary>
        /// Helper class for comparing two instances of <see cref="MethodSpecification"/> objects
        /// using <see cref="MetadataToken"/> property as unique key for comparison.
        /// </summary>
        private sealed class MemberReferenceComparer : IEqualityComparer<MethodSpecification>
        {
            /// <inheritdoc/>
            public bool Equals(MethodSpecification x, MethodSpecification y)
            {
                if (x is null)
                {
                    throw new ArgumentNullException(nameof(x));
                }

                if (y is null)
                {
                    throw new ArgumentNullException(nameof(y));
                }

                return x.MetadataToken.ToInt32() == y.MetadataToken.ToInt32();
            }

            /// <inheritdoc/>
            public int GetHashCode(MethodSpecification obj)
            {
                return obj.MetadataToken.ToInt32().GetHashCode();
            }
        }

        /// <summary>
        /// Creates new instance of <see cref="nanoMethodSpecificationTable"/> object.
        /// </summary>
        /// <param name="items">List of member references in Mono.Cecil format.</param>
        /// <param name="context">
        /// Assembly tables context - contains all tables used for building target assembly.
        /// </param>
        public nanoMethodSpecificationTable(
            IEnumerable<MethodSpecification> items,
            nanoTablesContext context)
            : base(items, new MemberReferenceComparer(), context)
        {
        }

        /// <summary>
        /// Gets method specification ID if possible.
        /// </summary>
        /// <param name="genericParameter">Method reference metadata in Mono.Cecil format.</param>
        /// <param name="referenceId">Method reference ID in .NET nanoFramework format.</param>
        /// <returns>Returns <c>true</c> if reference found, otherwise returns <c>false</c>.</returns>
        public bool TryGetMethodSpecificationId(
            MethodSpecification genericParameter,
            out ushort referenceId)
        {
            return TryGetIdByValue(genericParameter, out referenceId);
        }

        /// <inheritdoc/>
        protected override void WriteSingleItem(
            nanoBinaryWriter writer,
            MethodSpecification item)
        {
            if (!_context.MinimizeComplete)
            {
                return;
            }

            ushort instantiation;
            ushort tag;

            if (_context.MethodDefinitionTable.TryGetMethodReferenceId(item.Resolve(), out ushort method))
            {
                // MethodDefOrRef tag is 0 (MethodDef)
                tag = 0;

                instantiation = _context.SignaturesTable.GetOrCreateSignatureId(item.Resolve());
            }
            else if (_context.MethodReferencesTable.TryGetMethodReferenceId(item, out method))
            {
                // MethodDefOrRef tag is 1 (MemberRef)
                tag = 1;

                instantiation = _context.SignaturesTable.GetOrCreateSignatureId(item);
            }
            else
            {
                throw new ArgumentException($"Can't find entry in method definition or reference tables for method '{item.FullName}' [0x{item.MetadataToken.ToInt32():x8}].");
            }

            // MethodDefOrRef tag is 1 bit
            method = (ushort)(method << 1);

            // OR with tag to form coded index
            method |= tag;

            writer.WriteUInt16(method);

            writer.WriteUInt16(instantiation);
        }
    }
}
