//
// Copyright (c) .NET Foundation and Contributors
// Original work from Oleg Rakhmatulin.
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Encapsulates logic for storing member (methods or fields) references list and writing
    /// this collected list into target assembly in .NET nanoFramework format.
    /// </summary>
    public sealed class nanoFieldReferenceTable :
        nanoReferenceTableBase<FieldReference>
    {
        private const int sizeOf_CLR_RECORD_FIELDREF = 6;

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

            var writerStartPosition = writer.BaseStream.Position;

            // name
            WriteStringReference(writer, item.Name);

            // owner
            if (_context.TypeReferencesTable.TryGetTypeReferenceId(item.DeclaringType, out ushort referenceId))
            {
                writer.WriteUInt16(referenceId);
            }
            else if(item.FieldType is GenericParameter &&
                _context.GenericParamsTable.TryGetParameterId(item.FieldType, out referenceId))
            {
                // TODO
                writer.WriteUInt16(referenceId);
            }
            else
            {
                throw new ArgumentException($"Can't find entry in type reference table for Field {item.FullName}.");
            }

            // signature
            writer.WriteUInt16(_context.SignaturesTable.GetOrCreateSignatureId(item));

            var writerEndPosition = writer.BaseStream.Position;

            Debug.Assert((writerEndPosition - writerStartPosition) == sizeOf_CLR_RECORD_FIELDREF);
        }
    }
}
