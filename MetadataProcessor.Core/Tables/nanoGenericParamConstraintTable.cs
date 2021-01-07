//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using Mono.Collections.Generic;
using System;
using System.Collections.Generic;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Encapsulates logic for storing generic parameters constraints list and writing
    /// this collected list into target assembly in .NET nanoFramework format.
    /// </summary>
    public sealed class nanoGenericParameterConstraintTable :
        nanoReferenceTableBase<GenericParameterConstraint>
    {
        /// <summary>
        /// Helper class for comparing two instances of <see cref="GenericParameterConstraint"/> objects
        /// using <see cref="MetadataToken"/> property as unique key for comparison.
        /// </summary>
        private sealed class MemberReferenceComparer : IEqualityComparer<GenericParameterConstraint>
        {
            /// <inheritdoc/>
            public bool Equals(GenericParameterConstraint x, GenericParameterConstraint y)
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
            public int GetHashCode(GenericParameterConstraint obj)
            {
                return obj.MetadataToken.ToInt32().GetHashCode();
            }
        }

        /// <summary>
        /// Maps for each unique type specification and related identifier.
        /// </summary>
        private readonly IDictionary<ushort, ushort> _parameterOwnerId =
            new Dictionary<ushort, ushort>();

        /// <summary>
        /// Creates new instance of <see cref="nanoGenericParamTable"/> object.
        /// </summary>
        /// <param name="items">List of member references in Mono.Cecil format.</param>
        /// <param name="context">
        /// Assembly tables context - contains all tables used for building target assembly.
        /// </param>
        public nanoGenericParameterConstraintTable(
            IEnumerable<GenericParameterConstraint> items,
            nanoTablesContext context)
            : base(items, new MemberReferenceComparer(), context)
        {
        }

        /// <summary>
        /// Gets parameter constraint ID if possible.
        /// </summary>
        /// <param name="genericParameterConstraint">Generic Parameter Constraint metadata in Mono.Cecil format.</param>
        /// <param name="referenceId">Generic Parameter Constraint ID in .NET nanoFramework format.</param>
        /// <returns>Returns <c>true</c> if reference found, otherwise returns <c>false</c>.</returns>
        public bool TryGetParameterConstraintId(
            GenericParameterConstraint genericParameterConstraint,
            out ushort referenceId)
        {
            return TryGetIdByValue(genericParameterConstraint, out referenceId);
        }

        /// <inheritdoc/>
        protected override void WriteSingleItem(
            nanoBinaryWriter writer,
            GenericParameterConstraint item)
        {
            if (!_context.MinimizeComplete)
            {
                return;
            }

            // owner
            ushort constraint;
            ushort paramConstId;

            if (TryGetIdByValue(item, out paramConstId))
            {
                writer.WriteUInt16(_parameterOwnerId[paramConstId]);
            }

            if (item.ConstraintType is TypeDefinition &&
                _context.TypeDefinitionTable.TryGetTypeReferenceId(item.ConstraintType as TypeDefinition, out constraint))
            {
                // TypeDefOrRef tag is 0 (TypeDef)
                constraint |= 0x0000;
            }
            else if (_context.TypeSpecificationsTable.TryGetTypeReferenceId(item.ConstraintType as TypeSpecification, out constraint))
            {
                // TypeDefOrRef tag is 2 (TypeSpec)
                constraint |= 0x8000;
            }
            else if (_context.TypeReferencesTable.TryGetTypeReferenceId(item.ConstraintType, out constraint))
            {
                // TypeDefOrRef tag is 1 (TypeRef)
                constraint |= 0x4000;
            }
            else
            {
                throw new ArgumentException($"Can't find entry in the type definition, reference or specification tables for constraint [0x{item.MetadataToken.ToInt32():x8}].");
            }

            writer.WriteUInt16(constraint);
        }

        public void SetIdOfParamConstraintOwner(Collection<GenericParameterConstraint> constraints, ushort paramId)
        {
            ushort paramConstId;

            foreach(var c in constraints)
            {
                if (TryGetIdByValue(c, out paramConstId))
                {
                    _parameterOwnerId.Add(paramConstId, paramId);
                }
                else
                {
                    // TODO
                }
            }
        }
    }
}
