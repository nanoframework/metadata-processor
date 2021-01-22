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
    /// Encapsulates logic for storing methods references list and writing
    /// this collected list into target assembly in .NET nanoFramework format.
    /// </summary>
    public sealed class nanoMemberReferenceTable :
        nanoReferenceTableBase<MemberReference>
    {
        private const int sizeOf_CLR_RECORD_MEMBERREF = 6;

        /// <summary>
        /// Helper class for comparing two instances of <see cref="MethodReference"/> objects
        /// using <see cref="MemberReference.MetadataToken"/> property as unique key for comparison.
        /// </summary>
        private sealed class MemberReferenceComparer : IEqualityComparer<MemberReference>
        {
            /// <inheritdoc/>
            public bool Equals(MemberReference x, MemberReference y)
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
            public int GetHashCode(MemberReference obj)
            {
                return obj.MetadataToken.ToInt32().GetHashCode();
            }
        }
        public ClrTable TableIndex => ClrTable.MemberRef;

        /// <summary>
        /// Creates new instance of <see cref="nanoMemberReferenceTable"/> object.
        /// </summary>
        /// <param name="items">List of member references in Mono.Cecil format.</param>
        /// <param name="context">
        /// Assembly tables context - contains all tables used for building target assembly.
        /// </param>
        public nanoMemberReferenceTable(
            IEnumerable<MemberReference> items,
            nanoTablesContext context)
            : base(items, new MemberReferenceComparer(), context)
        {
        }

        /// <summary>
        /// Gets member reference ID if possible (if method is external and stored in this table).
        /// </summary>
        /// <param name="memberReference">Member reference metadata in Mono.Cecil format.</param>
        /// <param name="referenceId">Member reference ID in .NET nanoFramework format.</param>
        /// <returns>Returns <c>true</c> if reference found, otherwise returns <c>false</c>.</returns>
        public bool TryGetMemberReferenceId(
            MemberReference memberReference,
            out ushort referenceId)
        {
            return TryGetIdByValue(memberReference, out referenceId);
        }

        /// <inheritdoc/>
        protected override void WriteSingleItem(
            nanoBinaryWriter writer,
            MemberReference item)
        {
            if (!_context.MinimizeComplete)
            {
                return;
            }

            var writerStartPosition = writer.BaseStream.Position;

            ushort tag;
            ushort signature = 0;
            ushort referenceId = 0;

            if (item is MethodDefinition &&
                _context.MethodDefinitionTable.TryGetMethodReferenceId(item as MethodDefinition, out referenceId))
            {
                // MemberRefParent tag is 3 (MethodDef)
                tag = 3;

                //


                signature = 0;
            }
            else if (_context.TypeReferencesTable.TryGetTypeReferenceId(item.DeclaringType, out referenceId))
            {
                // MemberRefParent tag is 1 (TypeRef)
                tag = 1;

                // get signature index
                signature = _context.SignaturesTable.GetOrCreateSignatureId(item.DeclaringType);
            }
            else if (_context.TypeSpecificationsTable.TryGetTypeReferenceId(item.DeclaringType, out referenceId))
            {
                // MemberRefParent tag is 4 (TypeSpec)
                tag = 4;

                // get signature index
                signature = _context.SignaturesTable.GetOrCreateSignatureId(item.DeclaringType);
            }
            else if (_context.TypeDefinitionTable.TryGetTypeReferenceId(item.DeclaringType.Resolve(), out referenceId))
            {
                // MemberRefParent tag is 0 (TypeDef)
                tag = 0;

                // get signature index
                signature = _context.SignaturesTable.GetOrCreateSignatureId(item.DeclaringType.Resolve());
            }
            //else if (_context.TypeDefinitionTable.TryGetTypeReferenceId(item.DeclaringType.Resolve(), out referenceId))
            //{
            //    // MemberRefParent tag is 0 (TypeDef)
            //    tag = 0;
            //// get signature index
            //signature = _context.SignaturesTable.GetOrCreateSignatureId(item.DeclaringType.Resolve());
            //}
            else
            {
                // developer note:
                // The current implementation is lacking support for: ModuleRef and MethodDef

                throw new ArgumentException($"Can't find entry in type reference table for {item.DeclaringType.FullName} for MethodReference {item.FullName}.");
            }

            // MethodDefOrRef tag is 3 bits
            referenceId = (ushort)(referenceId << 3);

            // OR with tag to form coded index
            referenceId |= tag;

            WriteStringReference(writer, item.Name);
            writer.WriteUInt16(referenceId);

            writer.WriteUInt16(signature);

            var writerEndPosition = writer.BaseStream.Position;

            Debug.Assert((writerEndPosition - writerStartPosition) == sizeOf_CLR_RECORD_MEMBERREF);
        }
    }
}
