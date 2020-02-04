//
// Copyright (c) 2019 The nanoFramework project contributors
// Original work from Oleg Rakhmatulin.
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using System;
using System.Collections.Generic;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Encapsulates logic for storing member (methods or fields) references list and writing
    /// this collected list into target assembly in .NET nanoFramework format.
    /// </summary>
    public sealed class nanoFieldReferenceTable :
        nanoReferenceTableBase<FieldReference>
    {
        /// <summary>
        /// Helper class for comparing two instances of <see cref="FieldReference"/> objects
        /// using <see cref="FieldReference.FullName"/> property as unique key for comparison.
        /// </summary>
        private sealed class MemberReferenceComparer : IEqualityComparer<FieldReference>
        {
            /// <inheritdoc/>
            public bool Equals(FieldReference lhs, FieldReference rhs)
            {
                return string.Equals(lhs.FullName, rhs.FullName, StringComparison.Ordinal);
            }

            /// <inheritdoc/>
            public int GetHashCode(FieldReference that)
            {
                return that.FullName.GetHashCode();
            }
        }

        /// <summary>
        /// Creates new instance of <see cref="MFMetadataProcessor.nanoFieldReferenceTable"/> object.
        /// </summary>
        /// <param name="items">List of member references in Mono.Cecil format.</param>
        /// <param name="context">
        /// Assembly tables context - contains all tables used for building target assembly.
        /// </param>
        public nanoFieldReferenceTable(
            IEnumerable<FieldReference> items,
            nanoTablesContext context)
            : base(items, new MemberReferenceComparer(), context)
        {
        }

        /// <summary>
        /// Gets field reference ID if possible (if field is external and stored in this table).
        /// </summary>
        /// <param name="fieldReference">Field reference metadata in Mono.Cecil format.</param>
        /// <param name="referenceId">Field reference ID in .NET nanoFramework format.</param>
        /// <returns>Returns <c>true</c> if reference found, otherwise returns <c>false</c>.</returns>
        public bool TryGetFieldReferenceId(
            FieldReference fieldReference,
            out ushort referenceId)
        {
            return TryGetIdByValue(fieldReference, out referenceId);
        }

        /// <inheritdoc/>
        protected override void WriteSingleItem(
            nanoBinaryWriter writer,
            FieldReference item)
        {
            if (!_context.MinimizeComplete)
            {
                return;
            }

            ushort referenceId;
            _context.TypeReferencesTable.TryGetTypeReferenceId(item.DeclaringType, out referenceId);

            WriteStringReference(writer, item.Name);
            writer.WriteUInt16(referenceId);

            writer.WriteUInt16(_context.SignaturesTable.GetOrCreateSignatureId(item));
            writer.WriteUInt16(0); // padding
        }
    }
}
