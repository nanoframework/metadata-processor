//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Encapsulates logic for storing method specifications list and writing
    /// this collected list into target assembly in .NET nanoFramework format.
    /// </summary>
    public sealed class nanoMethodSpecificationTable :
        nanoReferenceTableBase<MethodSpecification>
    {
        private const int sizeOf_CLR_RECORD_METHODSPEC = 6;

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

        public nanoClrTable TableIndex => nanoClrTable.TBL_MethodSpec;

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

            var writerStartPosition = writer.BaseStream.Position;

            // Method
            if (_context.MethodDefinitionTable.TryGetMethodReferenceId(item.Resolve(), out ushort referenceId))
            {
                // method is method definition
            }
            else
            {
                Debug.Fail($"Can't find a reference for {item.Resolve()}");
            }

            writer.WriteUInt16((ushort)((item.Resolve() as MemberReference).ToEncodedNanoMethodToken() | referenceId));

            // Instantiation
            writer.WriteUInt16(_context.SignaturesTable.GetOrCreateSignatureId(item));

            // Container
            if (_context.TypeSpecificationsTable.TryGetTypeReferenceId(item.DeclaringType, out referenceId))
            {
                // method is method definition
            }

            writer.WriteUInt16(referenceId);

            var writerEndPosition = writer.BaseStream.Position;

            Debug.Assert((writerEndPosition - writerStartPosition) == sizeOf_CLR_RECORD_METHODSPEC);
        }
    }
}
