// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Encapsulates logic for storing attributes for types/methods/fields list and writing
    /// this collected list into target assembly in .NET nanoFramework format.
    /// </summary>
    public sealed class nanoAttributesTable : InanoTable
    {
        /// <summary>
        /// List of custom attributes in Mono.Cecil format for all internal types.
        /// </summary>
        private IEnumerable<Tuple<CustomAttribute, ICustomAttributeProvider>> _typesAttributes;

        /// <summary>
        /// List of custom attributes in Mono.Cecil format for all internal fields.
        /// </summary>
        /// 
        private IEnumerable<Tuple<CustomAttribute, ICustomAttributeProvider>> _fieldsAttributes;

        /// <summary>
        /// List of custom attributes in Mono.Cecil format for all internal methods.
        /// </summary>
        private IEnumerable<Tuple<CustomAttribute, ICustomAttributeProvider>> _methodsAttributes;

        /// <summary>
        /// Assembly tables context - contains all tables used for building target assembly.
        /// </summary>
        private readonly nanoTablesContext _context;

        public NanoClrTable TableIndex => NanoClrTable.TBL_Attributes;

        /// <summary>
        /// Gets all attributes (types, fields, and methods combined).
        /// </summary>
        public IEnumerable<Tuple<CustomAttribute, ICustomAttributeProvider>> GetAllAttributes()
        {
            return _typesAttributes
                .Concat(_fieldsAttributes)
                .Concat(_methodsAttributes);
        }

        /// <summary>
        /// Creates new instance of <see cref="nanoAttributesTable"/> object.
        /// </summary>
        /// <param name="typesAttributes">
        /// List of custom attributes in Mono.Cecil format for all internal types.
        /// </param>
        /// <param name="fieldsAttributes">
        /// List of custom attributes in Mono.Cecil format for all internal fields.
        /// </param>
        /// <param name="methodsAttributes">
        /// List of custom attributes in Mono.Cecil format for all internal methods.
        /// </param>
        /// <param name="context">
        /// Assembly tables context - contains all tables used for building target assembly.
        /// </param>
        public nanoAttributesTable(
            IEnumerable<Tuple<CustomAttribute, ICustomAttributeProvider>> typesAttributes,
            IEnumerable<Tuple<CustomAttribute, ICustomAttributeProvider>> fieldsAttributes,
            IEnumerable<Tuple<CustomAttribute, ICustomAttributeProvider>> methodsAttributes,
            nanoTablesContext context)
        {
            _typesAttributes = typesAttributes.ToList();
            _fieldsAttributes = fieldsAttributes.ToList();
            _methodsAttributes = methodsAttributes.ToList();

            _context = context;
        }

        /// <inheritdoc/>
        public void Write(
            nanoBinaryWriter writer)
        {
            WriteAttributes(
                writer,
                (ushort)NanoClrTable.TBL_TypeDef,
                _typesAttributes);

            WriteAttributes(
                writer,
                (ushort)NanoClrTable.TBL_FieldDef,
                _fieldsAttributes);

            WriteAttributes(
                writer,
                (ushort)NanoClrTable.TBL_MethodDef,
                _methodsAttributes);
        }

        private void WriteAttributes(
            nanoBinaryWriter writer,
            ushort tableNumber,
            IEnumerable<Tuple<CustomAttribute, ICustomAttributeProvider>> attributes)
        {
            foreach (Tuple<CustomAttribute, ICustomAttributeProvider> item in attributes)
            {
                CustomAttribute attribute = item.Item1;
                ICustomAttributeProvider owner = item.Item2;

                writer.WriteUInt16(tableNumber);

                // Get the reference ID based on the owner type
                ushort targetIdentifier = GetOwnerReferenceId(owner);
                writer.WriteUInt16(targetIdentifier);

                writer.WriteUInt16(_context.GetMethodReferenceId(attribute.Constructor));
                writer.WriteUInt16(_context.SignaturesTable.GetOrCreateSignatureId(attribute));
            }
        }

        private ushort GetOwnerReferenceId(ICustomAttributeProvider owner)
        {
            ushort referenceId = 0xFFFF;

            if (owner is TypeDefinition typeDef)
            {
                _context.TypeDefinitionTable.TryGetTypeReferenceId(typeDef, out referenceId);
            }
            else if (owner is MethodDefinition methodDef)
            {
                _context.MethodDefinitionTable.TryGetMethodReferenceId(methodDef, out referenceId);
            }
            else if (owner is FieldDefinition fieldDef)
            {
                _context.FieldsTable.TryGetFieldDefinitionId(fieldDef, false, out referenceId);
            }

            return referenceId;
        }

        /// <summary>
        /// Remove unused items from attribute tables.
        /// </summary>
        public void RemoveUnusedItems(HashSet<MetadataToken> set)
        {
            // build a collection of the current items that are present in the used items set
            List<Tuple<CustomAttribute, ICustomAttributeProvider>> usedItems = new List<Tuple<CustomAttribute, ICustomAttributeProvider>>();

            // types attributes
            foreach (var item in _typesAttributes.Where(item => set.Contains(item.Item1.AttributeType.MetadataToken)
                                                                || set.Contains(((IMetadataTokenProvider)item.Item1.Constructor).MetadataToken)))
            {
                usedItems.Add(item);
            }

            // re-create the items dictionary with the used items only
            _typesAttributes = usedItems.Select(a => new Tuple<CustomAttribute, ICustomAttributeProvider>(a.Item1, a.Item2));

            // fields attributes
            usedItems = new List<Tuple<CustomAttribute, ICustomAttributeProvider>>();

            foreach (var item in _fieldsAttributes.Where(item => set.Contains(item.Item1.AttributeType.MetadataToken)
                                                                 || set.Contains(((IMetadataTokenProvider)item.Item1.Constructor).MetadataToken)))
            {
                usedItems.Add(item);
            }

            // re-create the items dictionary with the used items only
            _fieldsAttributes = usedItems.Select(a => new Tuple<CustomAttribute, ICustomAttributeProvider>(a.Item1, a.Item2));

            // methods attributes
            usedItems = new List<Tuple<CustomAttribute, ICustomAttributeProvider>>();

            foreach (var item in _methodsAttributes.Where(item => set.Contains(item.Item1.AttributeType.MetadataToken)
                                                                  || set.Contains(((IMetadataTokenProvider)item.Item1.Constructor).MetadataToken)))
            {
                usedItems.Add(item);
            }

            // re-create the items dictionary with the used items only
            _methodsAttributes = usedItems.Select(a => new Tuple<CustomAttribute, ICustomAttributeProvider>(a.Item1, a.Item2));
        }
    }
}
