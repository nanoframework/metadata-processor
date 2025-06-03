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
        /// Maps each unique generic parameter and its type.
        /// </summary>
        private Dictionary<GenericParameter, TypeReference> _typeForGenericParam =
            new Dictionary<GenericParameter, TypeReference>();

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
            foreach (GenericParameter gp in items)
            {
                MethodDefinition methodWithGenericParam = _context.MethodDefinitionTable.Items.SingleOrDefault(m => m.GenericParameters.Contains(gp));

                if (methodWithGenericParam != null)
                {
                    // get the first method specification that matches this type AND name
                    GenericInstanceMethod instanceMethod = _context.MethodSpecificationTable.Items.FirstOrDefault(
                        mr => mr.DeclaringType.GetElementType() == methodWithGenericParam.DeclaringType &&
                        mr.Name == methodWithGenericParam.Name) as GenericInstanceMethod;

                    if (instanceMethod == null)
                    {
                        // No instantiation was ever emitted in the IL, treat the parameter as an "open generic"
                        _typeForGenericParam.Add(gp, gp);
                    }
                    else
                    {
                        // found a closed instantiation, so pick the real type argument
                        _typeForGenericParam.Add(
                            gp,
                            instanceMethod.GenericArguments.ElementAt(gp.Position)
                        );
                    }
                }
                else
                {
                    TypeDefinition typeWithGenericParam = _context.TypeDefinitionTable.Items.SingleOrDefault(t => t.GenericParameters.Contains(gp));

                    if (typeWithGenericParam != null)
                    {
                        if (_context.MethodReferencesTable.Items.Any())
                        {
                            // try to find an existing GenericInstanceType in the TypeReferencesTable
                            GenericInstanceType genericInstance = _context.TypeReferencesTable.Items
                                .OfType<GenericInstanceType>()
                                .FirstOrDefault(gt => gt.ElementType == typeWithGenericParam);

                            // fallback to whatever TryGetTypeSpecification gives us
                            if (genericInstance == null)
                            {
                                TypeReference spec = _context.TypeSpecificationsTable.TryGetTypeSpecification(typeWithGenericParam.MetadataToken);

                                // If it really is an instance, grab its args...
                                if (spec is GenericInstanceType genericInstanceType)
                                {
                                    genericInstance = genericInstanceType;
                                }
                                else
                                {
                                    // ... otherwise it's just an open generic definition (unbound T),
                                    // so our "argument" for gp is gp itself:
                                    _typeForGenericParam.Add(gp, gp);

                                    // done here
                                    continue;
                                }
                            }

                            // at this point we know we've got a GenericInstanceType,
                            // so pull out the actual generic‐argument at position gp.Position:
                            Debug.Assert(
                                genericInstance != null,
                                $"Couldn't find a generic instance for type {typeWithGenericParam} when processing generic parameter {gp}."
                            );

                            _typeForGenericParam.Add(
                                gp,
                                genericInstance.GenericArguments.ElementAt(gp.Position)
                            );
                        }
                        else
                        {
                            // No methods in scope → no instantiation possible
                            _typeForGenericParam.Add(gp, null);
                        }
                    }
                    else
                    {
                        _typeForGenericParam.Add(gp, null);
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

            // Signature
            if (_typeForGenericParam[item] != null)
            {
                writer.WriteUInt16(_context.SignaturesTable.GetOrCreateSignatureId(_typeForGenericParam[item]));
            }
            else
            {
                // no type for this generic parameter
                writer.WriteUInt16(0xFFFF);
            }

            // name
            WriteStringReference(writer, item.Name);

            var writerEndPosition = writer.BaseStream.Position;

            Debug.Assert((writerEndPosition - writerStartPosition) == sizeOf_CLR_RECORD_GENERICPARAM);
        }
    }
}
