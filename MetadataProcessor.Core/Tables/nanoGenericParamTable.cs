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
    /// Encapsulates logic for storing generic parameters list and writing
    /// this collected list into target assembly in .NET nanoFramework format.
    /// </summary>
    public sealed class nanoGenericParamTable :
        nanoReferenceTableBase<GenericParameter>
    {
        /// <summary>
        /// Helper class for comparing two instances of <see cref="GenericParameter"/> objects
        /// using <see cref="MetadataToken"/> property as unique key for comparison.
        /// </summary>
        private sealed class MemberReferenceComparer : IEqualityComparer<GenericParameter>
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
        /// Creates new instance of <see cref="nanoGenericParamTable"/> object.
        /// </summary>
        /// <param name="items">List of member references in Mono.Cecil format.</param>
        /// <param name="context">
        /// Assembly tables context - contains all tables used for building target assembly.
        /// </param>
        public nanoGenericParamTable(
            IEnumerable<GenericParameter> items,
            nanoTablesContext context)
            : base(items, new MemberReferenceComparer(), context)
        {
        }

        /// <summary>
        /// Gets method reference ID if possible (if method is external and stored in this table).
        /// </summary>
        /// <param name="genericParameter">Method reference metadata in Mono.Cecil format.</param>
        /// <param name="referenceId">Method reference ID in .NET nanoFramework format.</param>
        /// <returns>Returns <c>true</c> if reference found, otherwise returns <c>false</c>.</returns>
        public bool TryGetParameterId(
            GenericParameter genericParameter,
            out ushort referenceId)
        {
            return TryGetIdByValue(genericParameter, out referenceId);
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

            // number
            writer.WriteUInt16((ushort)item.Position);

            // flags
            writer.WriteUInt16((ushort)item.Attributes);

            // owner
            ushort tag;

            if (item.Owner is TypeDefinition &&
                _context.TypeDefinitionTable.TryGetTypeReferenceId(item.Owner as TypeDefinition, out ushort owner))
            {
                // TypeOrMethodDef tag is 0 (TypeDef)
                tag = 0;
            }
            else if (_context.MethodDefinitionTable.TryGetMethodReferenceId(item.Owner as MethodDefinition, out owner))
            {
                // TypeOrMethodDef tag is 1 (MethodDef)
                tag = 1;
            }
            else
            {
                throw new ArgumentException($"Can't find entry in type or method definition tables for generic parameter '{item.FullName}' [0x{item.Owner.MetadataToken.ToInt32():x8}].");
            }

            // TypeOrMethodDef tag is 1 bit
            owner = (ushort)(owner << 1);

            // OR with tag to form coded index
            owner |= tag;

            writer.WriteUInt16(owner);

            // name
            WriteStringReference(writer, item.Name);

            // link to constrains table
            if (item.HasConstraints)
            {
                ushort paramId;

                if (TryGetParameterId(item, out paramId))
                {
                    _context.GenericParamsConstraintTable.SetIdOfParamConstraintOwner(item.Constraints, paramId);
                }
                else
                {

                }
            }
        }
    }
}
