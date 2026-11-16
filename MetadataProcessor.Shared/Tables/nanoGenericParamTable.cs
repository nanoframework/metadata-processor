// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mono.Cecil;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Encapsulates logic for storing generic parameters list and writing
    /// this collected list into target assembly in .NET nanoFramework format.
    /// </summary>
    public sealed class nanoGenericParamTable :
        nanoReferenceTableBase<GenericParameter>
    {
        //////////////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////////////
        // <SYNC-WITH-NATIVE>                                                                       //
        // when updating this size here need to update matching define in nanoCLR_Types.h in native //
        private const int sizeOf_CLR_RECORD_GENERICPARAM = 10;
        //////////////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Helper class for comparing two instances of <see cref="GenericParameter"/> objects
        /// using <see cref="MetadataToken"/> property as unique key for comparison.
        /// </summary>
        private sealed class GenericParameterComparer : IEqualityComparer<GenericParameter>
        {
            /// <inheritdoc/>
            public bool Equals(GenericParameter x, GenericParameter y)
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
            public int GetHashCode(GenericParameter obj)
            {
                return obj.MetadataToken.ToInt32().GetHashCode();
            }
        }

        /// <summary>
        /// Gets the table index of this table in the target assembly.
        /// </summary>
        public NanoClrTable TableIndex => NanoClrTable.TBL_GenericParam;

        /// <summary>
        /// Creates new instance of <see cref="nanoGenericParamTable"/> object.
        /// </summary>
        /// <param name="items">List of member references in Mono.Cecil format.</param>
        /// <param name="context">
        /// Assembly tables context - contains all tables used for building target assembly.
        /// </param>
        public nanoGenericParamTable(
            IEnumerable<GenericParameter> items,
            nanoTablesContext context)
            : base(items, new GenericParameterComparer(), context)
        {
        }

        public bool TryGetParameterId(
            TypeReference genericParameter,
            out ushort referenceId)
        {
            return TryGetIdByValue(genericParameter as GenericParameter, out referenceId);
        }

        /// <inheritdoc/>
        protected override void WriteSingleItem(
            nanoBinaryWriter writer,
            GenericParameter item)
        {
            if (!_context.MinimizeComplete)
            {
                return;
            }

            var writerStartPosition = writer.BaseStream.Position;

            // number
            writer.WriteUInt16((ushort)item.Position);

            // flags
            writer.WriteUInt16((ushort)item.Attributes);

            // find owner
            if (item.Owner is TypeDefinition &&
                _context.TypeDefinitionTable.TryGetTypeReferenceId(item.Owner as TypeDefinition, out ushort referenceId))
            {
                // is TypeDef
            }
            else if (_context.MethodDefinitionTable.TryGetMethodReferenceId(item.Owner as MethodDefinition, out referenceId))
            {
                // is MethodDef
            }
            else
            {
                throw new ArgumentException($"Can't find entry in type or method definition tables for generic parameter '{item.FullName}' [0x{item.Owner.MetadataToken.ToInt32():x8}].");
            }

            // owner
            writer.WriteUInt16((ushort)(item.Owner.ToEncodedNanoTypeOrMethodDefToken() | referenceId));

            // Signature: type constraints, 0xFFFF when there are none
            writer.WriteUInt16(_context.SignaturesTable.GetOrCreateConstraintsSignatureId(item));

            // name
            WriteStringReference(writer, item.Name);

            var writerEndPosition = writer.BaseStream.Position;

            Debug.Assert((writerEndPosition - writerStartPosition) == sizeOf_CLR_RECORD_GENERICPARAM);
        }
    }
}
