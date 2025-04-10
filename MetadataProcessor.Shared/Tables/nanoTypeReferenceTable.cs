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
    /// Encapsulates logic for storing external type references list and writing
    /// this collected list into target assembly in .NET nanoFramework format.
    /// </summary>
    public sealed class nanoTypeReferenceTable :
        nanoReferenceTableBase<TypeReference>
    {
        //////////////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////////////
        // <SYNC-WITH-NATIVE>                                                                       //
        // when updating this size here need to update matching define in nanoCLR_Types.h in native //
        private const int sizeOf_CLR_RECORD_TYPEREF = 6;
        //////////////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////////////

        public NanoClrTable TableIndex => NanoClrTable.TBL_TypeRef;

        /// <summary>
        /// Creates new instance of <see cref="nanoTypeReferenceTable"/> object.
        /// </summary>
        /// <param name="items">Type references list in Mono.Cecil format.</param>
        /// <param name="context">
        /// Assembly tables context - contains all tables used for building target assembly.
        /// </param>
        public nanoTypeReferenceTable(
            IEnumerable<TypeReference> items,
            nanoTablesContext context)
            : base(items, new TypeReferenceEqualityComparer(context), context)
        {
        }

        /// <summary>
        /// Gets type reference identifier (if type is provided and this type is defined in target assembly).
        /// </summary>
        /// <remarks>
        /// For <c>null</c> value passed in <paramref name="typeReference"/> returns <c>0xFFFF</c> value.
        /// </remarks>
        /// <param name="typeReference">Type definition in Mono.Cecil format.</param>
        /// <param name="referenceId">Type reference identifier for filling.</param>
        /// <returns>Returns <c>true</c> if item found, otherwise returns <c>false</c>.</returns>
        public bool TryGetTypeReferenceId(
            TypeReference typeReference,
            out ushort referenceId)
        {
            if (typeReference == null) // This case is possible for encoding 'nested inside' case
            {
                referenceId = 0xFFFF;
                return true;
            }

            return TryGetIdByValue(typeReference, out referenceId);
        }

        /// <inheritdoc/>
        protected override void WriteSingleItem(
            nanoBinaryWriter writer,
            TypeReference item)
        {
            var writerStartPosition = writer.BaseStream.Position;


            // Get the full name for nested types
            string fullName = nanoTypeReferenceTable.GetFullName(item);

            WriteStringReference(writer, fullName);
            WriteStringReference(writer, item.Namespace);

            writer.WriteUInt16(GetScope(item)); // scope - TBL_AssemblyRef | TBL_TypeRef // 0x8000

            var writerEndPosition = writer.BaseStream.Position;

            Debug.Assert((writerEndPosition - writerStartPosition) == sizeOf_CLR_RECORD_TYPEREF);
        }

        /// <inheritdoc/>
        protected override void AllocateSingleItemStrings(
            TypeReference item)
        {
            GetOrCreateStringId(item.Namespace);
            GetOrCreateStringId(item.Name);
        }

        internal ushort GetScope(
            TypeReference typeReference)
        {
            // Check if the type is defined in the same assembly
            if (typeReference.Scope is ModuleDefinition moduleDefinition
                && moduleDefinition.Assembly == _context.AssemblyDefinition)
            {
                // The type is defined in the same assembly
                if (_context.TypeReferencesTable.TryGetTypeReferenceId(
                    typeReference.DeclaringType,
                    out ushort referenceId))
                {
                    return (ushort)(0x8000 | referenceId);
                }
                else
                {
                    // unknown scope
                    throw new InvalidOperationException($"Unknown scope for type reference '{typeReference.FullName}'");
                }
            }
            else if (typeReference.Scope is AssemblyNameReference assemblyNameReference)
            {
                // The type is defined in a referenced assembly
                return _context.AssemblyReferenceTable.GetReferenceId(assemblyNameReference);
            }
            else
            {
                // unknown scope
                throw new InvalidOperationException($"Unknown scope for type reference '{typeReference.FullName}'");
            }

        }

        private static string GetFullName(TypeReference typeReference)
        {
            if (typeReference.DeclaringType == null)
            {
                return typeReference.Name;
            }

            // return the full name of the declaring type
            return typeReference.FullName;
        }
    }
}
