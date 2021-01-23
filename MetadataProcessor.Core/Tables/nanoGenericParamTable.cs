﻿//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Encapsulates logic for storing generic parameters list and writing
    /// this collected list into target assembly in .NET nanoFramework format.
    /// </summary>
    public sealed class nanoGenericParamTable :
        nanoReferenceTableBase<GenericParameter>
    {
        private const int sizeOf_CLR_RECORD_GENERICPARAM = 8;

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
        /// Maps each unique generic parameter and its type.
        /// </summary>
        private Dictionary<GenericParameter, TypeReference> _typeForGenericParam =
            new Dictionary<GenericParameter, TypeReference>();

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
            foreach(var gp in items)
            {
                var methodWithGenericParam = _context.MethodDefinitionTable.Items.SingleOrDefault(m => m.GenericParameters.Contains(gp));

                if(methodWithGenericParam != null)
                {
                    // get the first method specification that matches this type AND name
                    var instanceMethod = _context.MethodSpecificationTable.Items.SingleOrDefault(
                        mr => mr.DeclaringType.GetElementType() == methodWithGenericParam.DeclaringType &&
                        mr.Name == methodWithGenericParam.Name) as GenericInstanceMethod;

                    _typeForGenericParam.Add(gp, instanceMethod.GenericArguments.ElementAt(gp.Position));
                }
                else
                {
                    var typeWithGenericParam = _context.TypeDefinitionTable.Items.SingleOrDefault(t => t.GenericParameters.Contains(gp));

                    if(typeWithGenericParam != null)
                    {
                        // get the first member that matches this type
                        var genericInstance = _context.MemberReferencesTable.Items.First(
                            mr => mr.DeclaringType.GetElementType() == typeWithGenericParam)
                            .DeclaringType as GenericInstanceType;

                        _typeForGenericParam.Add(gp, genericInstance.GenericArguments.ElementAt(gp.Position));
                    }
                    else
                    {
                        Debug.Fail("Can't find generic parameter in either methods or type definitions");
                    }
                }
            }
        }

        /// <summary>
        /// Gets generic parameter ID if possible (if generic parameter is stored in this table).
        /// </summary>
        /// <param name="genericParameter">Generic parameter TypeReference in Mono.Cecil format.</param>
        /// <param name="referenceId">Generic parameter identifier for filling.</param>
        /// <returns>Returns <c>true</c> the parameter was found, otherwise returns <c>false</c>.</returns>
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

            // owner
            writer.WriteUInt16(owner);

            // Signature
            writer.WriteUInt16(_context.SignaturesTable.GetOrCreateSignatureId(_typeForGenericParam[item]));

            // name
            WriteStringReference(writer, item.Name);

            var writerEndPosition = writer.BaseStream.Position;

            Debug.Assert((writerEndPosition - writerStartPosition) == sizeOf_CLR_RECORD_GENERICPARAM);
        }
    }
}
