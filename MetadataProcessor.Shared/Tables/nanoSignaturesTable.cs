﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Original work from Oleg Rakhmatulin.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Encapsulates logic for storing member (methods or fields) signatures list and writing
    /// this collected list into target assembly in .NET nanoFramework format.
    /// </summary>
    public sealed class nanoSignaturesTable : InanoTable
    {
        /// <summary>
        /// Helper class for comparing two instances of <see cref="Byte()"/> objects
        /// using full array content for comparison (length of arrays also should be equal).
        /// </summary>
        private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            /// <inheritdoc/>
            public bool Equals(byte[] lhs, byte[] rhs)
            {
                return (lhs.Length == rhs.Length && lhs.SequenceEqual(rhs));
            }

            /// <inheritdoc/>
            public int GetHashCode(byte[] that)
            {
                return that.Aggregate(37, (hash, item) => item ^ hash);
            }
        }

        private static readonly IDictionary<string, NanoCLRDataType> s_primitiveTypes =
            new Dictionary<string, NanoCLRDataType>(StringComparer.Ordinal);

        /// <summary>
        /// Built-in types (see flags in c_CLR_RT_DataTypeLookup at CLR Core code)
        /// </summary>
        private static readonly IDictionary<string, NanoCLRDataType> s_builtInTypes =
            new Dictionary<string, NanoCLRDataType>(StringComparer.Ordinal);

        public static IDictionary<string, NanoCLRDataType> PrimitiveTypes => s_primitiveTypes;

        static nanoSignaturesTable()
        {
            s_primitiveTypes.Add(typeof(void).FullName, NanoCLRDataType.DATATYPE_VOID);

            s_primitiveTypes.Add(typeof(sbyte).FullName, NanoCLRDataType.DATATYPE_I1);
            s_primitiveTypes.Add(typeof(short).FullName, NanoCLRDataType.DATATYPE_I2);
            s_primitiveTypes.Add(typeof(int).FullName, NanoCLRDataType.DATATYPE_I4);
            s_primitiveTypes.Add(typeof(long).FullName, NanoCLRDataType.DATATYPE_I8);

            s_primitiveTypes.Add(typeof(byte).FullName, NanoCLRDataType.DATATYPE_U1);
            s_primitiveTypes.Add(typeof(ushort).FullName, NanoCLRDataType.DATATYPE_U2);
            s_primitiveTypes.Add(typeof(uint).FullName, NanoCLRDataType.DATATYPE_U4);
            s_primitiveTypes.Add(typeof(ulong).FullName, NanoCLRDataType.DATATYPE_U8);

            s_primitiveTypes.Add(typeof(float).FullName, NanoCLRDataType.DATATYPE_R4);
            s_primitiveTypes.Add(typeof(double).FullName, NanoCLRDataType.DATATYPE_R8);

            s_primitiveTypes.Add(typeof(char).FullName, NanoCLRDataType.DATATYPE_CHAR);
            s_primitiveTypes.Add(typeof(string).FullName, NanoCLRDataType.DATATYPE_STRING);
            s_primitiveTypes.Add(typeof(bool).FullName, NanoCLRDataType.DATATYPE_BOOLEAN);

            s_primitiveTypes.Add(typeof(object).FullName, NanoCLRDataType.DATATYPE_OBJECT);
            s_primitiveTypes.Add(typeof(IntPtr).FullName, NanoCLRDataType.DATATYPE_I4);
            s_primitiveTypes.Add(typeof(UIntPtr).FullName, NanoCLRDataType.DATATYPE_U4);

            s_primitiveTypes.Add(typeof(WeakReference).FullName, NanoCLRDataType.DATATYPE_WEAKCLASS);

            // from c_CLR_RT_DataTypeLookup at CLR Core code
            s_builtInTypes.Add(typeof(bool).FullName, NanoCLRDataType.DATATYPE_BOOLEAN);
            s_builtInTypes.Add(typeof(char).FullName, NanoCLRDataType.DATATYPE_CHAR);
            s_builtInTypes.Add(typeof(sbyte).FullName, NanoCLRDataType.DATATYPE_I1);
            s_builtInTypes.Add(typeof(byte).FullName, NanoCLRDataType.DATATYPE_U1);
            s_builtInTypes.Add(typeof(short).FullName, NanoCLRDataType.DATATYPE_I2);
            s_builtInTypes.Add(typeof(ushort).FullName, NanoCLRDataType.DATATYPE_U2);
            s_builtInTypes.Add(typeof(int).FullName, NanoCLRDataType.DATATYPE_I4);
            s_builtInTypes.Add(typeof(uint).FullName, NanoCLRDataType.DATATYPE_U4);
            s_builtInTypes.Add(typeof(long).FullName, NanoCLRDataType.DATATYPE_I8);
            s_builtInTypes.Add(typeof(ulong).FullName, NanoCLRDataType.DATATYPE_U8);
            s_builtInTypes.Add(typeof(float).FullName, NanoCLRDataType.DATATYPE_R4);
            s_builtInTypes.Add(typeof(double).FullName, NanoCLRDataType.DATATYPE_R8);

            s_builtInTypes.Add(typeof(DateTime).FullName, NanoCLRDataType.DATATYPE_DATETIME);
            s_builtInTypes.Add(typeof(TimeSpan).FullName, NanoCLRDataType.DATATYPE_TIMESPAN);
            s_builtInTypes.Add(typeof(string).FullName, NanoCLRDataType.DATATYPE_STRING);

            s_builtInTypes.Add("System.RuntimeTypeHandle", NanoCLRDataType.DATATYPE_REFLECTION);
            s_builtInTypes.Add("System.RuntimeFieldHandle", NanoCLRDataType.DATATYPE_REFLECTION);
            s_builtInTypes.Add("System.RuntimeMethodHandle", NanoCLRDataType.DATATYPE_REFLECTION);

            s_builtInTypes.Add(typeof(WeakReference).FullName, NanoCLRDataType.DATATYPE_WEAKCLASS);
        }

        /// <summary>
        /// Stores list of unique signatures and corresponding identifiers.
        /// </summary>
        private readonly IDictionary<byte[], ushort> _idsBySignatures =
            new Dictionary<byte[], ushort>(new ByteArrayComparer());

        /// <summary>
        /// Assembly tables context - contains all tables used for building target assembly.
        /// </summary>
        private readonly nanoTablesContext _context;

        private readonly bool _verbose = false;

        /// <summary>
        /// Last available signature id (offset in resulting table).
        /// </summary>
        private ushort _lastAvailableId;

        public NanoClrTable TableIndex => NanoClrTable.TBL_Signatures;

        /// <summary>
        /// Creates new instance of <see cref="nanoSignaturesTable"/> object.
        /// </summary>
        /// <param name="context">
        /// Assembly tables context - contains all tables used for building target assembly.
        /// </param>
        public nanoSignaturesTable(nanoTablesContext context)
        {
            _context = context;

            _verbose = context._verbose;
        }

        /// <summary>
        /// Gets existing or creates new signature identifier for field definition.
        /// </summary>
        /// <param name="fieldDefinition">Field definition in Mono.Cecil format.</param>
        public ushort GetOrCreateSignatureId(
            FieldDefinition fieldDefinition)
        {
            var sig = GetSignature(fieldDefinition.FieldType, true);
            var sigId = GetOrCreateSignatureIdImpl(sig);

            if (_verbose)
            {
                Console.WriteLine($"{fieldDefinition.MetadataToken} ({fieldDefinition.FullName}) {fieldDefinition.MetadataToken.ToInt32():X8} -> {sig.BufferToHexString()} -> {sigId:X4}");
            }

            return sigId;
        }

        /// <summary>
        /// Gets existing or creates new signature identifier for the list of generic parameters.
        /// </summary>
        /// <param name="genericParameters">List of parameters information in Mono.Cecil format.</param>
        public ushort GetOrCreateSignatureId(Collection<GenericParameter> genericParameters)
        {
            if (genericParameters == null || genericParameters.Count == 0)
            {
                return 0xFFFF; // No generic parameters
            }

            return GetOrCreateSignatureIdImpl(GetSignature(genericParameters));
        }

        /// <summary>
        /// Gets existing or creates new signature identifier for field reference.
        /// </summary>
        /// <param name="fieldReference">Field reference in Mono.Cecil format.</param>
        public ushort GetOrCreateSignatureId(
            FieldReference fieldReference)
        {
            var sig = GetSignature(fieldReference);
            var sigId = GetOrCreateSignatureIdImpl(sig);

            if (_verbose)
            {
                Console.WriteLine($"{fieldReference.MetadataToken} ({fieldReference.FullName}) {fieldReference.MetadataToken.ToInt32():X8} -> {sig.BufferToHexString()} -> {sigId:X4}");
            }

            return sigId;
        }

        /// <summary>
        /// Gets existing or creates new signature identifier for method definition.
        /// </summary>
        /// <param name="methodDefinition">Method definition in Mono.Cecil format.</param>
        public ushort GetOrCreateSignatureId(
            MethodDefinition methodDefinition)
        {
            var sig = GetSignature(methodDefinition);
            var sigId = GetOrCreateSignatureIdImpl(sig);

            if (_verbose)
            {
                Console.WriteLine($"{methodDefinition.MetadataToken} ({methodDefinition.FullName}) {methodDefinition.MetadataToken.ToInt32():X8} -> {sig.BufferToHexString()} -> {sigId:X4}");
            }

            return sigId;
        }

        /// <summary>
        /// Gets existing or creates new signature identifier for method reference.
        /// </summary>
        /// <param name="methodReference">Method reference in Mono.Cecil format.</param>
        public ushort GetOrCreateSignatureId(
            MethodReference methodReference)
        {
            var sig = GetSignature(methodReference);
            var sigId = GetOrCreateSignatureIdImpl(sig);

            if (_verbose)
            {
                Console.WriteLine($"{methodReference.MetadataToken} ({methodReference.FullName}) {methodReference.MetadataToken.ToInt32():X8} -> {sig.BufferToHexString()} -> {sigId:X4}");
            }

            return sigId;
        }

        /// <summary>
        /// Gets existing or creates new signature identifier for method specification.
        /// </summary>
        /// <param name="methodReference">Method reference in Mono.Cecil format.</param>
        public ushort GetOrCreateSignatureId(
            MethodSpecification methodSpecification)
        {
            var sig = GetSignature(methodSpecification);
            var sigId = GetOrCreateSignatureIdImpl(sig);

            if (_verbose)
            {
                Console.WriteLine($"{methodSpecification.MetadataToken} ({methodSpecification.FullName}) {methodSpecification.MetadataToken.ToInt32():X8} -> {sig.BufferToHexString()} -> {sigId:X4}");
            }

            return sigId;
        }

        /// <summary>
        /// Gets existing or creates new signature identifier for list of local variables.
        /// </summary>
        /// <param name="variables">List of variables information in Mono.Cecil format.</param>
        public ushort GetOrCreateSignatureId(
            Collection<VariableDefinition> variables)
        {
            if (variables == null || variables.Count == 0)
            {
                return 0xFFFF; // No local variables
            }

            return GetOrCreateSignatureIdImpl(GetSignature(variables));
        }

        /// <summary>
        /// Gets existing or creates new signature identifier for list of class interfaces.
        /// </summary>
        /// <param name="interfaces">List of interfaes information in Mono.Cecil format.</param>
        public ushort GetOrCreateSignatureId(
            Collection<InterfaceImplementation> interfaces)
        {
            if (interfaces == null || interfaces.Count == 0)
            {
                return 0xFFFF; // No implemented interfaces
            }

            return GetOrCreateSignatureIdImpl(GetSignature(interfaces));
        }

        /// <summary>
        /// Gets existing or creates new field default value (just writes value as is with size).
        /// </summary>
        /// <param name="defaultValue">Default field value in binary format.</param>
        public ushort GetOrCreateSignatureId(
            byte[] defaultValue)
        {
            if (defaultValue == null || defaultValue.Length == 0)
            {
                return 0xFFFF; // No default value
            }

            return GetOrCreateSignatureIdImpl(GetSignature(defaultValue));
        }

        /// <summary>
        /// Gets existing or creates new type reference signature (used for encoding type specification).
        /// </summary>
        /// <param name="interfaceImplementation">Interface implementation in Mono.Cecil format.</param>
        public ushort GetOrCreateSignatureId(
            InterfaceImplementation interfaceImplementation)
        {
            var sig = GetSignature(interfaceImplementation, false);
            var sigId = GetOrCreateSignatureIdImpl(sig);

            if (_verbose)
            {
                Console.WriteLine($"{interfaceImplementation.MetadataToken} ({interfaceImplementation.InterfaceType.FullName}) {interfaceImplementation.MetadataToken.ToInt32():X8} -> {sig.BufferToHexString()} -> {sigId:X4}");
            }

            return sigId;
        }

        /// <summary>
        /// Gets existing or creates new type reference signature (used for encoding type specification).
        /// </summary>
        /// <param name="typeReference">Type reference in Mono.Cecil format.</param>
        public ushort GetOrCreateSignatureId(
            TypeReference typeReference)
        {
            var sig = GetSignature(typeReference, false);
            var sigId = GetOrCreateSignatureIdImpl(sig);

            if (_verbose)
            {
                Console.WriteLine($"{typeReference.MetadataToken} ({typeReference.FullName}) {typeReference.MetadataToken.ToInt32():X8} -> {sig.BufferToHexString()} -> {sigId:X4}");
            }

            return sigId;
        }

        /// <summary>
        /// Gets existing or creates new custom attribute signature.
        /// </summary>
        /// <param name="customAttribute">Custom attribute in Mono.Cecil format.</param>
        public ushort GetOrCreateSignatureId(CustomAttribute customAttribute)
        {
            var sig = GetSignature(customAttribute);
            var sigId = GetOrCreateSignatureIdImpl(sig);

            if (_verbose)
            {
                Console.WriteLine($"{customAttribute} -> {sig.BufferToHexString()} -> {sigId:X4}");
            }

            return sigId;
        }

        /// <summary>
        /// Writes data tzpe signature into ouput stream.
        /// </summary>
        /// <param name="typeDefinition">Tzpe reference or definition in Mono.Cecil format.</param>
        /// <param name="writer">Target binary writer for writing signature information.</param>
        /// <param name="alsoWriteSubType">If set to <c>true</c> also sub-type will be written.</param>
        /// <param name="expandEnumType">If set to <c>true</c> expand enum with base type.</param>
        public void WriteDataType(
            TypeReference typeDefinition,
            nanoBinaryWriter writer,
            bool alsoWriteSubType,
            bool expandEnumType,
            bool isTypeDefinition)
        {
            if (isTypeDefinition &&
                typeDefinition.MetadataType == MetadataType.Object)
            {
                writer.WriteByte((byte)NanoCLRDataType.DATATYPE_CLASS);
                return;
            }

            if (s_primitiveTypes.TryGetValue(
                typeDefinition.FullName,
                out NanoCLRDataType dataType))
            {
                writer.WriteByte((byte)dataType);
                return;
            }

            if (typeDefinition is TypeSpecification)
            {
                //Debug.Fail("Gotcha!");
            }

            if (typeDefinition.MetadataType == MetadataType.Class)
            {
                writer.WriteByte((byte)NanoCLRDataType.DATATYPE_CLASS);
                if (alsoWriteSubType)
                {
                    WriteSubTypeInfo(typeDefinition, writer);
                }
                return;
            }

            if (typeDefinition.MetadataType == MetadataType.ValueType)
            {
                var resolvedType = typeDefinition.Resolve();
                if (resolvedType != null && resolvedType.IsEnum && expandEnumType)
                {
                    var baseTypeValue = resolvedType.Fields.FirstOrDefault(item => item.IsSpecialName);
                    if (baseTypeValue != null)
                    {
                        WriteTypeInfo(baseTypeValue.FieldType, writer);
                        return;
                    }
                }

                writer.WriteByte((byte)NanoCLRDataType.DATATYPE_VALUETYPE);
                if (alsoWriteSubType)
                {
                    WriteSubTypeInfo(typeDefinition, writer);
                }
                return;
            }

            if (typeDefinition.MetadataType == MetadataType.Var)
            {
                writer.WriteByte((byte)NanoCLRDataType.DATATYPE_VAR);

                if (alsoWriteSubType)
                {
                    // following ECMA-335 VI.B.4.3 Metadata
                    writer.WriteByte((byte)(typeDefinition as GenericParameter).Position);
                }

                return;
            }

            if (typeDefinition.MetadataType == MetadataType.MVar)
            {
                writer.WriteByte((byte)NanoCLRDataType.DATATYPE_MVAR);

                if (alsoWriteSubType)
                {
                    // following ECMA-335 VI.B.4.3 Metadata
                    writer.WriteByte((byte)(typeDefinition as GenericParameter).Position);
                }

                return;
            }

            if (typeDefinition.IsArray)
            {
                writer.WriteByte((byte)NanoCLRDataType.DATATYPE_SZARRAY);

                var array = (ArrayType)typeDefinition;

                if (array.ElementType.IsGenericParameter)
                {
                    // ECMA 335 VI.B.4.3 Metadata
                    writer.WriteByte((byte)NanoCLRDataType.DATATYPE_VAR);

                    // OK to use byte here as we won't support more than 0x7F generic parameters
                    writer.WriteByte((byte)(array.ElementType as GenericParameter).Position);
                }
                else if (alsoWriteSubType)
                {

                    WriteDataType(array.ElementType, writer, true, expandEnumType, isTypeDefinition);
                }

                return;
            }

            if (typeDefinition.IsByReference)
            {
                writer.WriteByte((byte)NanoCLRDataType.DATATYPE_BYREF);

                if (alsoWriteSubType)
                {
                    var resolvedType = typeDefinition.Resolve();

                    WriteDataType(resolvedType, writer, false, expandEnumType, isTypeDefinition);
                }

                return;
            }

            if (typeDefinition.IsGenericInstance)
            {
                // following ECMA-335 VI.B.4.3 Metadata
                // II.23.2.12 Type
                writer.WriteByte((byte)NanoCLRDataType.DATATYPE_GENERICINST);

                var genericType = (GenericInstanceType)typeDefinition;
                WriteDataType(genericType.Resolve(), writer, true, expandEnumType, isTypeDefinition);

                // OK to use byte here as we won't support more than 0x7F arguments
                writer.WriteByte((byte)genericType.GenericArguments.Count);

                foreach (var a in genericType.GenericArguments)
                {
                    WriteDataType(a, writer, true, expandEnumType, isTypeDefinition);
                }

                return;
            }

            writer.WriteByte(0x00);
        }

        /// <summary>
        /// Writes data type for type reference.
        /// </summary>
        /// <param name="typeDefinition">Type reference or definition in Mono.Cecil format.</param>
        /// <param name="writer">Target binary writer for writing signature information.</param>
        public void WriteDataTypeForTypeDef(TypeDefinition typeDefinition, nanoBinaryWriter writer)
        {
            // start checking with the built-in types
            if (s_builtInTypes.TryGetValue(
                typeDefinition.FullName,
                out NanoCLRDataType dataType))
            {
                writer.WriteByte((byte)dataType);
            }
            else
            {
                // check order matters because some types are derived from others
                if (typeDefinition.IsEnum)
                {
                    FieldDefinition baseTypeValue = typeDefinition.Fields.FirstOrDefault(item => item.IsSpecialName);

                    if (baseTypeValue != null)
                    {
                        WriteTypeInfo(baseTypeValue.FieldType, writer);
                    }
                    else
                    {
                        writer.WriteByte((byte)NanoCLRDataType.DATATYPE_I4);
                    }
                }
                else if (typeDefinition.IsValueType)
                {
                    writer.WriteByte((byte)NanoCLRDataType.DATATYPE_VALUETYPE);
                }
                else if (typeDefinition.IsClass || typeDefinition.IsInterface)
                {
                    writer.WriteByte((byte)NanoCLRDataType.DATATYPE_CLASS);
                }
                else
                {
                    Debug.Fail("Gotcha!");
                }
            }
        }

        /// <inheritdoc/>
        public void Write(
            nanoBinaryWriter writer)
        {
            foreach (var signature in _idsBySignatures
                .OrderBy(item => item.Value)
                .Select(item => item.Key))
            {
                writer.WriteBytes(signature);
            }
        }

        private byte[] GetSignature(
            FieldReference fieldReference)
        {
            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer)) // Only Write(Byte) will be used
            {
                var binaryWriter = nanoBinaryWriter.CreateBigEndianBinaryWriter(writer);

                // Field reference calling convention
                binaryWriter.WriteByte(0x06);
                WriteTypeInfo(fieldReference.FieldType, binaryWriter);

                return buffer.ToArray();
            }
        }

        internal byte[] GetSignature(
            IMethodSignature methodReference)
        {
            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer)) // Only Write(Byte) will be used
            {
                var binaryWriter = nanoBinaryWriter.CreateLittleEndianBinaryWriter(writer);

                // method calling convention

                // IMAGE_CEE_CS_CALLCONV_DEFAULT: 0x00
                // IMAGE_CEE_CS_CALLCONV_HASTHIS: 0x20
                // IMAGE_CEE_CS_CALLCONV_GENERIC: 0x10

                byte callingConvention = methodReference.HasThis ? (byte)0x20 : (byte)0x00;
                callingConvention |= (byte)methodReference.CallingConvention;

                writer.Write(callingConvention);

                // generic parameters count, if any
                if (methodReference.CallingConvention == MethodCallingConvention.Generic)
                {
                    writer.Write((byte)(methodReference as MethodReference).GenericParameters.Count);
                }

                // regular parameter count
                writer.Write((byte)(methodReference.Parameters.Count));

                WriteTypeInfo(methodReference.ReturnType, binaryWriter);
                foreach (var parameter in methodReference.Parameters)
                {
                    WriteTypeInfo(parameter.ParameterType, binaryWriter);
                }

                return buffer.ToArray();
            }
        }

        internal byte[] GetSignature(
            MethodSpecification methodSpecification)
        {
            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer)) // Only Write(Byte) will be used
            {
                var binaryWriter = nanoBinaryWriter.CreateLittleEndianBinaryWriter(writer);

                var genericInstance = methodSpecification as GenericInstanceMethod;

                // implementation from ECMA-335 II.23.2.15

                // method calling convention
                // IMAGE_CEE_CS_CALLCONV_GENERICINST: 0x0A

                writer.Write((byte)0x0A);

                // generic arguments count
                writer.Write((byte)(genericInstance.GenericArguments.Count));

                foreach (var argument in genericInstance.GenericArguments)
                {
                    WriteTypeInfo(argument.GetElementType(), binaryWriter);
                }

                return buffer.ToArray();
            }
        }

        private byte[] GetSignature(
            IEnumerable<VariableDefinition> variables)
        {
            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer)) // Only Write(Byte) will be used
            {
                var binaryWriter = nanoBinaryWriter.CreateBigEndianBinaryWriter(writer);
                foreach (var variable in variables)
                {
                    WriteTypeInfo(variable.VariableType, binaryWriter);
                }

                return buffer.ToArray();
            }
        }

        private byte[] GetSignature(
            Collection<InterfaceImplementation> interfaces)
        {
            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer)) // Only Write(Byte) will be used
            {
                var binaryWriter = nanoBinaryWriter.CreateBigEndianBinaryWriter(writer);

                binaryWriter.WriteByte((byte)interfaces.Count);
                foreach (var item in interfaces)
                {
                    WriteSubTypeInfo(item.InterfaceType, binaryWriter);
                }

                return buffer.ToArray();
            }
        }

        private byte[] GetSignature(
            TypeReference typeReference,
            bool isFieldSignature)
        {
            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer)) // Only Write(Byte) will be used
            {
                var binaryWriter = nanoBinaryWriter.CreateBigEndianBinaryWriter(writer);

                if (isFieldSignature)
                {
                    //////////////////////////////////////////////////////////
                    // dev notes: this is coming from
                    // CorCallingConvention.IMAGE_CEE_CS_CALLCONV_FIELD = 0x06
                    //////////////////////////////////////////////////////////
                    writer.Write((byte)0x06); // Field signature prefix
                }
                WriteTypeInfo(typeReference, binaryWriter);

                return buffer.ToArray();
            }
        }

        private byte[] GetSignature(Collection<GenericParameter> genericParameters)
        {
            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer)) // Only Write(Byte) will be used
            {
                foreach (var parameter in genericParameters)
                {
                    WriteGenericParameterValue(writer, parameter);
                }

                return buffer.ToArray();
            }
        }

        private byte[] GetSignature(
            InterfaceImplementation typeReference,
            bool isFieldSignature)
        {
            return GetSignature(typeReference.InterfaceType, isFieldSignature);
        }

        private byte[] GetSignature(
            byte[] defaultValue)
        {
            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer))
            {
                writer.Write((ushort)defaultValue.Length);
                writer.Write(defaultValue);

                return buffer.ToArray();
            }
        }

        private byte[] GetSignature(
            CustomAttribute customAttribute)
        {
            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer))
            {
                foreach (var argument in customAttribute.ConstructorArguments)
                {
                    WriteAttributeArgumentValue(writer, argument);
                }

                // TODO: use compressed format
                writer.Write((ushort)(customAttribute.Properties.Count + customAttribute.Fields.Count));

                foreach (var namedArgument in customAttribute.Fields.OrderBy(item => item.Name))
                {
                    writer.Write((byte)nanoSerializationType.SERIALIZATION_TYPE_FIELD);
                    writer.Write(_context.StringTable.GetOrCreateStringId(namedArgument.Name));
                    WriteAttributeArgumentValue(writer, namedArgument.Argument);
                }

                foreach (var namedArgument in customAttribute.Properties.OrderBy(item => item.Name))
                {
                    writer.Write((byte)nanoSerializationType.SERIALIZATION_TYPE_PROPERTY);
                    writer.Write(_context.StringTable.GetOrCreateStringId(namedArgument.Name));
                    WriteAttributeArgumentValue(writer, namedArgument.Argument);
                }

                return buffer.ToArray();
            }
        }

        private void WriteAttributeArgumentValue(
            BinaryWriter writer,
            CustomAttributeArgument argument)
        {
            NanoCLRDataType dataType;
            if (s_primitiveTypes.TryGetValue(argument.Type.FullName, out dataType))
            {
                switch (dataType)
                {
                    case NanoCLRDataType.DATATYPE_BOOLEAN:
                        writer.Write((byte)nanoSerializationType.ELEMENT_TYPE_BOOLEAN);
                        writer.Write((byte)((bool)argument.Value ? 1 : 0));
                        break;
                    case NanoCLRDataType.DATATYPE_I1:
                        writer.Write((byte)nanoSerializationType.ELEMENT_TYPE_I1);
                        writer.Write((sbyte)argument.Value);
                        break;
                    case NanoCLRDataType.DATATYPE_U1:
                        writer.Write((byte)nanoSerializationType.ELEMENT_TYPE_U1);
                        writer.Write((byte)argument.Value);
                        break;
                    case NanoCLRDataType.DATATYPE_I2:
                        writer.Write((byte)nanoSerializationType.ELEMENT_TYPE_I2);
                        writer.Write((short)argument.Value);
                        break;
                    case NanoCLRDataType.DATATYPE_U2:
                        writer.Write((byte)nanoSerializationType.ELEMENT_TYPE_U2);
                        writer.Write((ushort)argument.Value);
                        break;
                    case NanoCLRDataType.DATATYPE_I4:
                        writer.Write((byte)nanoSerializationType.ELEMENT_TYPE_I4);
                        writer.Write((int)argument.Value);
                        break;
                    case NanoCLRDataType.DATATYPE_U4:
                        writer.Write((byte)nanoSerializationType.ELEMENT_TYPE_U4);
                        writer.Write((uint)argument.Value);
                        break;
                    case NanoCLRDataType.DATATYPE_I8:
                        writer.Write((byte)nanoSerializationType.ELEMENT_TYPE_I8);
                        writer.Write((long)argument.Value);
                        break;
                    case NanoCLRDataType.DATATYPE_U8:
                        writer.Write((byte)nanoSerializationType.ELEMENT_TYPE_U8);
                        writer.Write((ulong)argument.Value);
                        break;
                    case NanoCLRDataType.DATATYPE_R4:
                        writer.Write((byte)nanoSerializationType.ELEMENT_TYPE_R4);
                        writer.Write((float)argument.Value);
                        break;
                    case NanoCLRDataType.DATATYPE_R8:
                        writer.Write((byte)nanoSerializationType.ELEMENT_TYPE_R8);
                        writer.Write((double)argument.Value);
                        break;
                    case NanoCLRDataType.DATATYPE_CHAR:
                        writer.Write((byte)nanoSerializationType.ELEMENT_TYPE_CHAR);
                        writer.Write((char)argument.Value);
                        break;
                    case NanoCLRDataType.DATATYPE_STRING:
                        writer.Write((byte)nanoSerializationType.ELEMENT_TYPE_STRING);
                        writer.Write(_context.StringTable.GetOrCreateStringId((string)argument.Value));
                        break;
                    default:
                        Debug.Fail(dataType.ToString());
                        throw new ArgumentException($"Failed to generate signature for CustomAttribute. Unsupported type: {argument.Type.FullName}.");
                }
            }

            if (argument.Type.IsArray && argument.Type.GetElementType().FullName == "System.Object")
            {
                var paramCollection = (CustomAttributeArgument[])argument.Value;

                // add count of array elements that will follow
                writer.Write((byte)paramCollection.Length);

                // now add parameters as usual
                foreach (var attributeArgument in paramCollection)
                {
                    WriteAttributeArgumentValue(writer, (CustomAttributeArgument)attributeArgument.Value);
                }
            }

            if (argument.Type.FullName == "System.Type")
            {
                writer.Write((byte)nanoSerializationType.ELEMENT_TYPE_STRING);
                writer.Write(_context.StringTable.GetOrCreateStringId(((TypeReference)argument.Value).FullName));
            }
        }

        private void WriteGenericParameterValue(BinaryWriter writer, GenericParameter parameter)
        {
            if (_context.GenericParamsTable.TryGetParameterId(parameter, out ushort referenceId))
            {
                writer.Write((byte)nanoSerializationType.ELEMENT_TYPE_GENERICINST);

                // OK to use byte because we are not supporting more than 0x7F generic parameters
                writer.Write((byte)referenceId);
            }
            else
            {
                // TODO
            }
        }

        private ushort GetOrCreateSignatureIdImpl(
            byte[] signature)
        {
            ushort id;
            if (_idsBySignatures.TryGetValue(signature, out id))
            {
                return id;
            }

            var fullSignatures = GetFullSignaturesArray();
            for (var i = 0; i <= fullSignatures.Length - signature.Length; ++i)
            {
                if (signature.SequenceEqual(fullSignatures.Skip(i).Take(signature.Length)))
                {
                    return (ushort)i;
                }
            }

            id = _lastAvailableId;
            _idsBySignatures.Add(signature, id);
            _lastAvailableId += (ushort)signature.Length;

            return id;
        }

        private void WriteTypeInfo(
            TypeReference typeReference,
            nanoBinaryWriter writer)
        {
            // dev notes: from the original MDP
            // If there is modifier on type record of local variable, we put it before type of local variable.
            if (typeReference.IsOptionalModifier)
            {
                writer.WriteByte(0x0); // OpTypeModifier ???
            }

            var byReference = typeReference as ByReferenceType;
            if (byReference != null)
            {
                writer.WriteByte((byte)NanoCLRDataType.DATATYPE_BYREF);
                WriteDataType(byReference.ElementType, writer, true, false, false);
            }
            else
            {
                WriteDataType(typeReference, writer, true, false, false);
            }
        }

        private byte[] GetFullSignaturesArray()
        {
            return _idsBySignatures
                .OrderBy(item => item.Value)
                .Select(item => item.Key)
                .Aggregate(new List<byte>(),
                    (current, item) =>
                    {
                        current.AddRange(item);
                        return current;
                    })
                .ToArray();
        }

        private void WriteSubTypeInfo(TypeReference typeDefinition, nanoBinaryWriter writer)
        {
            // decoded at target with CLR_TkFromStream

            ushort tag;

            if ((typeDefinition is TypeSpecification ||
                 typeDefinition is GenericParameter) &&
                _context.TypeSpecificationsTable.TryGetTypeReferenceId(typeDefinition, out ushort referenceId))
            {
                // TypeDefOrRef tag is 2 (TypeSpec)
                tag = 2;

                // TypeDefOrRef tag is 2 bits
                referenceId = (ushort)(referenceId << 2);
            }
            else if (_context.TypeReferencesTable.TryGetTypeReferenceId(typeDefinition, out referenceId))
            {
                // TypeDefOrRef tag is 1 (TypeRef)
                tag = 1;

                // TypeDefOrRef tag is 2 bits
                referenceId = (ushort)(referenceId << 2);
            }
            else if (_context.TypeDefinitionTable.TryGetTypeReferenceId(
                typeDefinition.Resolve(),
                out referenceId))
            {
                // TypeDefOrRef tag is 0 (TypeDef)
                tag = 0;

                // TypeDefOrRef tag is 2 bits
                referenceId = (ushort)(referenceId << 2);
            }
            else
            {
                throw new ArgumentException($"Can't find entry in type reference table for {typeDefinition.FullName}.");
            }

            // OR with tag to form coded index
            referenceId |= tag;

            writer.WriteMetadataToken(referenceId);
        }
    }
}
