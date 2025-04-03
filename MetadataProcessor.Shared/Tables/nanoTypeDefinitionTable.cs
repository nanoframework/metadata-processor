// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Original work from Oleg Rakhmatulin.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Collections.Generic;
using nanoFramework.Tools.MetadataProcessor.Core;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Encapsulates logic for storing type definitions (complete type metadata) list and writing
    /// this collected list into target assembly in .NET nanoFramework format.
    /// </summary>
    public sealed class nanoTypeDefinitionTable :
        nanoReferenceTableBase<TypeDefinition>
    {
        //////////////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////////////
        // <SYNC-WITH-NATIVE>                                                                       //
        // when updating this size here need to update matching define in nanoCLR_Types.h in native //
        private const int sizeOf_CLR_RECORD_TYPEDEF = 27;
        //////////////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Helper class for comparing two instances of <see cref="TypeDefinition"/> objects
        /// using <see cref="TypeDefinition.FullName"/> property as unique key for comparison.
        /// </summary>
        private sealed class TypeDefinitionEqualityComparer : IEqualityComparer<TypeDefinition>
        {
            /// <inheritdoc/>
            public bool Equals(TypeDefinition lhs, TypeDefinition rhs)
            {
                return string.Equals(lhs.FullName, rhs.FullName, StringComparison.Ordinal);
            }

            /// <inheritdoc/>
            public int GetHashCode(TypeDefinition item)
            {
                return item.FullName.GetHashCode();
            }
        }

        private IDictionary<uint, List<Tuple<uint, uint>>> _byteCodeOffsets =
            new Dictionary<uint, List<Tuple<uint, uint>>>();

        private List<TypeDefinition> TypeDefinitions;

        public List<EnumDeclaration> EnumDeclarations { get; }

        public NanoClrTable TableIndex => NanoClrTable.TBL_TypeDef;

        /// <summary>
        /// Creates new instance of <see cref="nanoTypeDefinitionTable"/> object.
        /// </summary>
        /// <param name="items">List of types definitions in Mono.Cecil format.</param>
        /// <param name="context">
        /// Assembly tables context - contains all tables used for building target assembly.
        /// </param>
        public nanoTypeDefinitionTable(
            IEnumerable<TypeDefinition> items,
            nanoTablesContext context)
            : base(items, new TypeDefinitionEqualityComparer(), context)
        {
            TypeDefinitions = items
                .Select(t => t).OrderBy(t => t.FullName).ToList();

            // need to build enum declarations here, because the fields with the enums will be removed during minimization
            EnumDeclarations = TypeDefinitions.Where(t => t.IsEnum).Select(et => et.ToEnumDeclaration()).ToList();
        }

        /// <summary>
        /// Gets type reference identifier (if type is provided and this type is defined in target assembly).
        /// </summary>
        /// <remarks>
        /// For <c>null</c> value passed in <paramref name="typeDefinition"/> returns <c>0xFFFF</c> value.
        /// </remarks>
        /// <param name="typeDefinition">Type definition in Mono.Cecil format.</param>
        /// <param name="referenceId">Type reference identifier for filling.</param>
        /// <returns>Returns <c>true</c> if item found, otherwise returns <c>false</c>.</returns>
        public bool TryGetTypeReferenceId(
            TypeDefinition typeDefinition,
            out ushort referenceId)
        {
            if (typeDefinition == null) // This case is possible for encoding 'nested inside' case
            {
                referenceId = 0xFFFF;
                return true;
            }

            return TryGetIdByValue(typeDefinition, out referenceId);
        }

        public IEnumerable<Tuple<uint, uint>> GetByteCodeOffsets(
            uint clrMethodToken)
        {
            return _byteCodeOffsets[clrMethodToken];
        }

        /// <inheritdoc/>
        protected override void WriteSingleItem(
            nanoBinaryWriter writer,
            TypeDefinition item)
        {
            var writerStartPosition = writer.BaseStream.Position;

            _context.StringTable.GetOrCreateStringId(item.Namespace);

            // Name
            WriteStringReference(writer, item.Name);

            // NameSpace
            WriteStringReference(writer, item.Namespace);

            // Extends
            writer.WriteUInt16(GetEncodedTypeReferenceOrDefinitionId(item.BaseType));

            // EnclosingType
            writer.WriteUInt16(GetEncodedTypeReferenceOrDefinitionId(item.DeclaringType));

            var fieldsList = item.Fields
                .Where(field => !field.HasConstant)
                .OrderByDescending(field => field.IsStatic)
                .ToList();

            using (var stream = new MemoryStream(6))
            {
                WriteClassFields(fieldsList, writer.GetMemoryBasedClone(stream));

                if (_context.MinimizeComplete)
                {

                    if (item.DeclaringType == null)
                    {
                        foreach (var method in item.Methods)
                        {
                            var offsets = CodeWriter
                                .PreProcessMethod(method, _context.ByteCodeTable.FakeStringTable)
                                .ToList();

                            _byteCodeOffsets.Add(method.MetadataToken.ToUInt32(), offsets);
                        }
                    }
                    foreach (var nestedType in item.NestedTypes)
                    {
                        foreach (var method in nestedType.Methods)
                        {
                            var offsets = CodeWriter
                                .PreProcessMethod(method, _context.ByteCodeTable.FakeStringTable)
                                .ToList();

                            _byteCodeOffsets.Add(method.MetadataToken.ToUInt32(), offsets);
                        }
                    }

                    WriteMethodBodies(
                        item.FullName,
                        item.Methods,
                        item.Interfaces,
                        writer
                    );

                    _context.SignaturesTable.WriteDataTypeForTypeDef(item, writer);

                    writer.WriteBytes(stream.ToArray());
                }
            }

            ushort genericParamRefId = 0xFFFF;

            if (item.HasGenericParameters)
            {
                // no need to check if it's found
                _context.GenericParamsTable.TryGetParameterId(item.GenericParameters.FirstOrDefault(), out genericParamRefId);
            }

            // FirstGenericParam
            writer.WriteUInt16(genericParamRefId);

            // GenericParamCount
            writer.WriteByte((byte)item.GenericParameters.Count);

            // Flags
            writer.WriteUInt16(
                (ushort)GetFlags(
                    item,
                    _context.MethodDefinitionTable));

            var writerEndPosition = writer.BaseStream.Position;

            // ignore assert when not minimize
            if (_context.MinimizeComplete)
            {
                Debug.Assert((writerEndPosition - writerStartPosition) == sizeOf_CLR_RECORD_TYPEDEF);
            }
        }

        /// <summary>
        /// Add a "fake" TypeDef as placeholder for an instanced generic type.
        /// </summary>
        /// <param name="typeReference"></param>
        public void AddGenericInstanceType(TypeReference typeReference)
        {
            // drop namespace as it's already on the full name
            // OK to use full name as type name to help comparison ahead
            var genericType = new TypeDefinition(
                "",
                typeReference.FullName,
                typeReference.Resolve().Attributes);

            // add to items list
            AddItem(genericType);
        }

        private void WriteClassFields(
            IList<FieldDefinition> fieldsList,
            nanoBinaryWriter writer)
        {
            ushort firstStaticFieldId = 0;
            var staticFieldsCount = 0;
            foreach (var field in fieldsList.Where(item => item.IsStatic))
            {
                ushort fieldReferenceId;
                if (_context.FieldsTable.TryGetFieldReferenceId(field, false, out fieldReferenceId))
                {
                    if (staticFieldsCount == 0)
                    {
                        // this is to be checked only on the 1st pass
                        firstStaticFieldId = fieldReferenceId;
                    }

                    _context.SignaturesTable.GetOrCreateSignatureId(field);
                    _context.SignaturesTable.GetOrCreateSignatureId(field.InitialValue);
                    _context.StringTable.GetOrCreateStringId(field.Name);

                    ++staticFieldsCount;
                }
                else
                {
                    // field ID not found!!
                }
            }

            ushort firstInstanceFieldId = 0;
            var instanceFieldsCount = 0;
            foreach (var field in fieldsList.Where(item => !item.IsStatic))
            {
                ushort fieldReferenceId;
                if (_context.FieldsTable.TryGetFieldReferenceId(field, false, out fieldReferenceId))
                {
                    if (instanceFieldsCount == 0)
                    {
                        // this is to be checked only on the 1st pass
                        firstInstanceFieldId = fieldReferenceId;
                    }

                    _context.SignaturesTable.GetOrCreateSignatureId(field);
                    _context.StringTable.GetOrCreateStringId(field.Name);

                    ++instanceFieldsCount;
                }
                else
                {
                    // field ID not found!!
                }
            }

            if (firstStaticFieldId < firstInstanceFieldId)
            {
                if (instanceFieldsCount > 0 && staticFieldsCount == 0)
                {
                    firstStaticFieldId = firstInstanceFieldId;
                }
            }

            // FirstStaticField
            writer.WriteUInt16(firstStaticFieldId);

            // FirstInstanceField
            writer.WriteUInt16(firstInstanceFieldId);

            // StaticFieldsCount
            writer.WriteByte((byte)staticFieldsCount);

            // InstanceFieldsCount
            writer.WriteByte((byte)instanceFieldsCount);
        }

        private void WriteMethodBodies(
            string typeName,
            Collection<MethodDefinition> methods,
            Collection<InterfaceImplementation> iInterfaces,
            nanoBinaryWriter writer)
        {
            ushort firstMethodId = 0xFFFF;
            var virtualMethodsCount = 0;
            foreach (var method in methods.Where(item => item.IsVirtual))
            {
                firstMethodId = Math.Min(firstMethodId, _context.ByteCodeTable.GetMethodId(method));
                CreateMethodSignatures(method);
                ++virtualMethodsCount;
            }

            var instanceMethodsCount = 0;
            foreach (var method in methods.Where(item => !(item.IsVirtual || item.IsStatic)))
            {
                firstMethodId = Math.Min(firstMethodId, _context.ByteCodeTable.GetMethodId(method));
                CreateMethodSignatures(method);
                ++instanceMethodsCount;
            }

            var staticMethodsCount = 0;
            foreach (var method in methods.Where(item => item.IsStatic))
            {
                firstMethodId = Math.Min(firstMethodId, _context.ByteCodeTable.GetMethodId(method));
                CreateMethodSignatures(method);
                ++staticMethodsCount;
            }

            if (virtualMethodsCount + instanceMethodsCount + staticMethodsCount == 0)
            {
                firstMethodId = _context.ByteCodeTable.NextMethodId;
            }

            // Interfaces
            writer.WriteUInt16(_context.SignaturesTable.GetOrCreateSignatureId(iInterfaces));

            // FirstMethod
            writer.WriteUInt16(firstMethodId);

            // sanity checks
            if (virtualMethodsCount > byte.MaxValue)
            {
                throw new InvalidOperationException($"Fatal error processing '{typeName}', virtual methods count ({virtualMethodsCount}) exceeds maximum supported (255).");
            }

            if (instanceMethodsCount > byte.MaxValue)
            {
                throw new InvalidOperationException($"Fatal error processing '{typeName}', instance methods count ({instanceMethodsCount}) exceeds maximum supported (255).");
            }

            if (staticMethodsCount > byte.MaxValue)
            {
                throw new InvalidOperationException($"Fatal error processing '{typeName}', static methods count ({staticMethodsCount}) exceeds maximum supported (255).");
            }

            // VirtualMethodCount
            writer.WriteByte((byte)virtualMethodsCount);
            // InstanceMethodCount
            writer.WriteByte((byte)instanceMethodsCount);
            // StaticMethodCount
            writer.WriteByte((byte)staticMethodsCount);
        }

        private void CreateMethodSignatures(
            MethodDefinition method)
        {
            _context.SignaturesTable.GetOrCreateSignatureId(method);
            if (method.HasBody)
            {
                _context.SignaturesTable.GetOrCreateSignatureId(method.Body.Variables);
            }
            _context.StringTable.GetOrCreateStringId(method.Name);
        }

        private ushort GetEncodedTypeReferenceOrDefinitionId(
            TypeReference typeReference)
        {
            ushort tag;

            if (_context.TypeReferencesTable.TryGetTypeReferenceId(typeReference, out ushort referenceId))
            {
                // check "nested inside" case
                if (referenceId != 0xFFFF)
                {
                    // is TypeRef

                    return (ushort)(typeReference.ToEncodedNanoTypeDefOrRefToken() | referenceId);
                }
            }

            if (TryGetTypeReferenceId(typeReference?.Resolve(), out ushort typeId))
            {
                // check "nested inside" case
                if (referenceId != 0xFFFF)
                {
                    // is TypeDef

                    return (ushort)(typeReference.Resolve().ToEncodedNanoTypeDefOrRefToken() | typeId);
                }
            }

            return 0xFFFF;
        }

        /// <summary>
        /// Get flags for the Type definition.
        /// </summary>
        /// <param name="definition"><see cref="TypeDefinition"/> to get the flags</param>
        /// <param name="methodDefinitioTable">List with type definitions that have finalizers, if this check is required.</param>
        /// <returns></returns>
        /// <remarks>
        /// If the <paramref name="typesWithFinalizers"/> is null, the check for finalizer won't be checked.
        /// </remarks>
        internal static nanoTypeDefinitionFlags GetFlags(
            TypeDefinition definition,
            nanoMethodDefinitionTable methodDefinitioTable = null)
        {
            var flags = nanoTypeDefinitionFlags.TD_Scope_None;

            if (definition.IsPublic)
            {
                flags = nanoTypeDefinitionFlags.TD_Scope_Public;
            }
            else if (definition.IsNestedPublic)
            {
                flags = nanoTypeDefinitionFlags.TD_Scope_NestedPublic;
            }
            else if (definition.IsNestedPrivate)
            {
                flags = nanoTypeDefinitionFlags.TD_Scope_NestedPrivate;
            }
            else if (definition.IsNestedFamily)
            {
                flags = nanoTypeDefinitionFlags.TD_Scope_NestedFamily;
            }
            else if (definition.IsNestedAssembly)
            {
                flags = nanoTypeDefinitionFlags.TD_Scope_NestedAssembly;
            }
            else if (definition.IsNestedFamilyAndAssembly)
            {
                flags = nanoTypeDefinitionFlags.TD_Scope_NestedFamANDAssem;
            }
            else if (definition.IsNestedFamilyOrAssembly)
            {
                flags = nanoTypeDefinitionFlags.TD_Scope_NestedFamORAssem;
            }

            if (definition.IsSerializable)
            {
                flags |= nanoTypeDefinitionFlags.TD_Serializable;
            }

            if (definition.IsEnum)
            {
                flags |= nanoTypeDefinitionFlags.TD_Semantics_Enum;
                flags |= nanoTypeDefinitionFlags.TD_Serializable;
            }
            else if (definition.IsValueType)
            {
                flags |= nanoTypeDefinitionFlags.TD_Semantics_ValueType;
            }
            else if (definition.IsInterface)
            {
                flags |= nanoTypeDefinitionFlags.TD_Semantics_Interface;
            }

            if (definition.IsAbstract)
            {
                flags |= nanoTypeDefinitionFlags.TD_Abstract;
            }
            if (definition.IsSealed)
            {
                flags |= nanoTypeDefinitionFlags.TD_Sealed;
            }

            if (definition.IsSpecialName)
            {
                flags |= nanoTypeDefinitionFlags.TD_SpecialName;
            }

            if (definition.IsBeforeFieldInit)
            {
                flags |= nanoTypeDefinitionFlags.TD_BeforeFieldInit;
            }
            if (definition.HasSecurity)
            {
                flags |= nanoTypeDefinitionFlags.TD_HasSecurity;
            }
            if (definition.HasCustomAttributes)
            {
                flags |= nanoTypeDefinitionFlags.TD_HasAttributes;
            }

            var baseType = definition.BaseType;
            if (baseType != null &&
                baseType.FullName == "System.MulticastDelegate")
            {
                flags |= nanoTypeDefinitionFlags.TD_MulticastDelegate;
            }

            if (baseType != null &&
                baseType.FullName == "System.Delegate" &&
                definition.FullName != "System.MulticastDelegate")
            {
                flags |= nanoTypeDefinitionFlags.TD_Delegate;
            }

            // need to check for finalizers methods
            if (methodDefinitioTable != null)
            {
                foreach (var m in definition.Methods)
                {
                    if (methodDefinitioTable.Items.Contains(m))
                    {
                        var methodName = m.Name;
                        if (methodName == "Finalize" &&
                            m.ReturnType.FullName == "System.Void" &&
                            !m.HasParameters)
                        {
                            if (m.DeclaringType.FullName != "System.Object")
                            {
                                flags |= nanoTypeDefinitionFlags.TD_HasFinalizer;
                            }
                        }
                    }
                }
            }

            return flags;
        }

        internal void ResetByteCodeOffsets()
        {
            _byteCodeOffsets = new Dictionary<uint, List<Tuple<uint, uint>>>();
        }

        public override void RemoveUnusedItems(HashSet<MetadataToken> set)
        {
            base.RemoveUnusedItems(set);

            // remove
            // build a collection of the current items that are present in the used items set
            List<TypeDefinition> usedItems = new List<TypeDefinition>();

            foreach (var item in TypeDefinitions
                                    .Where(item => set.Contains(item.MetadataToken)))
            {
                usedItems.Add(item);
            }

            // replace existing collection
            TypeDefinitions = usedItems;
        }
    }
}
