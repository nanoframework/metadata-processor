// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Original work from Oleg Rakhmatulin.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Mono.Cecil;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Encapsulates logic for storing external assembly references list and writing
    /// this collected list into target assembly in .NET nanoFramework format.
    /// </summary>
    public sealed class nanoAssemblyReferenceTable :
        nanoReferenceTableBase<AssemblyNameReference>
    {
        //////////////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////////////
        // when updating this size here need to update matching define in nanoCLR_Types.h in native //
        private const int sizeOf_CLR_RECORD_ASSEMBLYREF = 10;
        //////////////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Helper class for comparing two instances of <see cref="AssemblyNameReference"/> objects
        /// using <see cref="AssemblyNameReference.FullName"/> property as unique key for comparison.
        /// </summary>
        private sealed class AssemblyNameReferenceComparer : IEqualityComparer<AssemblyNameReference>
        {
            /// <inheritdoc/>
            public bool Equals(AssemblyNameReference lhs, AssemblyNameReference rhs)
            {
                return string.Equals(lhs.FullName, rhs.FullName, StringComparison.Ordinal);
            }

            /// <inheritdoc/>
            public int GetHashCode(AssemblyNameReference item)
            {
                return item.FullName.GetHashCode();
            }
        }

        /// <summary>
        /// Creates new instance of <see cref="nanoAssemblyReferenceTable"/> object.
        /// </summary>
        /// <param name="items">List of assembly references in Mono.Cecil format.</param>
        /// <param name="context">
        /// Assembly tables context - contains all tables used for building target assembly.
        /// </param>
        public nanoAssemblyReferenceTable(
            IEnumerable<AssemblyNameReference> items,
            nanoTablesContext context)
            : base(items, new AssemblyNameReferenceComparer(), context)
        {
        }

        /// <inheritdoc/>
        protected override void WriteSingleItem(
            nanoBinaryWriter writer,
            AssemblyNameReference item)
        {
            if (!_context.MinimizeComplete)
            {
                return;
            }

            var writerStartPosition = writer.BaseStream.Position;

            WriteStringReference(writer, item.Name);
            writer.WriteVersion(item.Version);

            var writerEndPosition = writer.BaseStream.Position;

            Debug.Assert((writerEndPosition - writerStartPosition) == sizeOf_CLR_RECORD_ASSEMBLYREF);
        }

        /// <inheritdoc/>
        protected override void AllocateSingleItemStrings(
            AssemblyNameReference item)
        {
            GetOrCreateStringId(item.Name);
        }

        /// <summary>
        /// Gets assembly reference ID by assembly name reference in Mono.Cecil format.
        /// </summary>
        /// <param name="assemblyNameReference">Assembly name reference in Mono.Cecil format.</param>
        /// <returns>Reference ID for passed <paramref name="assemblyNameReference"/> item.</returns>
        public ushort GetReferenceId(
            AssemblyNameReference assemblyNameReference)
        {
            ushort referenceId;
            TryGetIdByValue(assemblyNameReference, out referenceId);
            return referenceId;
        }
    }
}
