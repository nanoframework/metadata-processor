//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Text;

namespace nanoFramework.Tools.MetadataProcessor.Core.Extensions
{
    internal static class TypeReferenceExtensions
    {
        public static bool IsToInclude(this TypeReference value)
        {
            return !nanoTablesContext.IgnoringAttributes.Contains(value.FullName);
        }

        public static string TypeSignatureAsString(this TypeReference type)
        {
            if (type.MetadataType == MetadataType.IntPtr)
            {
                return "I";
            }

            if (type.MetadataType == MetadataType.UIntPtr)
            {
                return "U";
            }

            nanoClrDataType dataType;
            if (nanoSignaturesTable.PrimitiveTypes.TryGetValue(type.FullName, out dataType))
            {
                switch (dataType)
                {
                    case nanoClrDataType.DATATYPE_VOID:
                    case nanoClrDataType.DATATYPE_BOOLEAN:
                    case nanoClrDataType.DATATYPE_CHAR:
                    case nanoClrDataType.DATATYPE_I1:
                    case nanoClrDataType.DATATYPE_U1:
                    case nanoClrDataType.DATATYPE_I2:
                    case nanoClrDataType.DATATYPE_U2:
                    case nanoClrDataType.DATATYPE_I4:
                    case nanoClrDataType.DATATYPE_U4:
                    case nanoClrDataType.DATATYPE_I8:
                    case nanoClrDataType.DATATYPE_U8:
                    case nanoClrDataType.DATATYPE_R4:
                    case nanoClrDataType.DATATYPE_BYREF:
                    case nanoClrDataType.DATATYPE_OBJECT:
                    case nanoClrDataType.DATATYPE_WEAKCLASS:
                        return dataType.ToString().Replace("DATATYPE_", "");

                    case nanoClrDataType.DATATYPE_LAST_PRIMITIVE:
                        return "STRING";

                    case nanoClrDataType.DATATYPE_LAST_PRIMITIVE_TO_PRESERVE:
                        return "R8";

                    case nanoClrDataType.DATATYPE_LAST_PRIMITIVE_TO_MARSHAL:
                        return "TIMESPAN";

                    case nanoClrDataType.DATATYPE_REFLECTION:
                        return type.FullName.Replace(".", "");
                }
            }

            if (type.MetadataType == MetadataType.ValueType)
            {
                StringBuilder valueTypeSig = new StringBuilder("VALUETYPE [");
                valueTypeSig.Append(type.MetadataToken.ToInt32().ToString("x8"));
                valueTypeSig.Append("]");

                return valueTypeSig.ToString();
            }

            if (type.IsArray)
            {
                StringBuilder arraySig = new StringBuilder("SZARRAY ");
                arraySig.Append(type.GetElementType().TypeSignatureAsString());

                return arraySig.ToString();
            }

            if (type.IsByReference)
            {
                StringBuilder byrefSig = new StringBuilder("BYREF ");
                byrefSig.Append(type.GetElementType().TypeSignatureAsString());

                return byrefSig.ToString();
            }

            if (type.IsGenericParameter)
            {
                StringBuilder genericParamTypeSig = new StringBuilder();

                if ((type as GenericParameter).Owner is TypeDefinition)
                {
                    genericParamTypeSig.Append("!");
                }
                if ((type as GenericParameter).Owner is MethodDefinition)
                {
                    genericParamTypeSig.Append("!!");
                }

                genericParamTypeSig.Append($"{type.Name}");


                return genericParamTypeSig.ToString();
            }

            return "";
        }

        public static string ToNativeTypeAsString(this TypeReference type)
        {
            nanoClrDataType dataType;
            if (nanoSignaturesTable.PrimitiveTypes.TryGetValue(type.FullName, out dataType))
            {
                switch (dataType)
                {
                    case nanoClrDataType.DATATYPE_VOID:
                        return "void";
                    case nanoClrDataType.DATATYPE_BOOLEAN:
                        return "bool";
                    case nanoClrDataType.DATATYPE_CHAR:
                        return "char";
                    case nanoClrDataType.DATATYPE_I1:
                        return "int8_t";
                    case nanoClrDataType.DATATYPE_U1:
                        return "uint8_t";
                    case nanoClrDataType.DATATYPE_I2:
                        return "int16_t";
                    case nanoClrDataType.DATATYPE_U2:
                        return "uint16_t";
                    case nanoClrDataType.DATATYPE_I4:
                        return "signed int";
                    case nanoClrDataType.DATATYPE_U4:
                        return "unsigned int";
                    case nanoClrDataType.DATATYPE_I8:
                        return "int64_t";
                    case nanoClrDataType.DATATYPE_U8:
                        return "uint64_t";
                    case nanoClrDataType.DATATYPE_R4:
                        return "float";
                    case nanoClrDataType.DATATYPE_BYREF:
                        return "";

                    // system.String
                    case nanoClrDataType.DATATYPE_LAST_PRIMITIVE:
                        return "const char*";

                    // System.Double
                    case nanoClrDataType.DATATYPE_LAST_PRIMITIVE_TO_PRESERVE:
                        return "double";

                    default:
                        return "UNSUPPORTED";
                }
            }

            if (type.MetadataType == MetadataType.Class)
            {
                return "UNSUPPORTED";
            }

            if (type.MetadataType == MetadataType.ValueType)
            {
                return "UNSUPPORTED";
            }

            if (type.IsArray)
            {
                StringBuilder arraySig = new StringBuilder("CLR_RT_TypedArray_");
                arraySig.Append(type.GetElementType().ToCLRTypeAsString());

                return arraySig.ToString();
            }

            if (type.IsGenericParameter)
            {
                return "UNSUPPORTED";
            }
            return "";
        }

        public static string ToCLRTypeAsString(this TypeReference type)
        {
            nanoClrDataType dataType;
            if (nanoSignaturesTable.PrimitiveTypes.TryGetValue(type.FullName, out dataType))
            {
                switch (dataType)
                {
                    case nanoClrDataType.DATATYPE_VOID:
                        return "void";
                    case nanoClrDataType.DATATYPE_BOOLEAN:
                        return "bool";
                    case nanoClrDataType.DATATYPE_CHAR:
                        return "CHAR";
                    case nanoClrDataType.DATATYPE_I1:
                        return "INT8";
                    case nanoClrDataType.DATATYPE_U1:
                        return "UINT8";
                    case nanoClrDataType.DATATYPE_I2:
                        return "INT16";
                    case nanoClrDataType.DATATYPE_U2:
                        return "UINT16";
                    case nanoClrDataType.DATATYPE_I4:
                        return "INT32";
                    case nanoClrDataType.DATATYPE_U4:
                        return "UINT32";
                    case nanoClrDataType.DATATYPE_I8:
                        return "INT64";
                    case nanoClrDataType.DATATYPE_U8:
                        return "UINT64";
                    case nanoClrDataType.DATATYPE_R4:
                        return "float";
                    case nanoClrDataType.DATATYPE_BYREF:
                        return "NONE";

                    // system.String
                    case nanoClrDataType.DATATYPE_LAST_PRIMITIVE:
                        return "LPCSTR";

                    // System.Double
                    case nanoClrDataType.DATATYPE_LAST_PRIMITIVE_TO_PRESERVE:
                        return "double";

                    default:
                        return "UNSUPPORTED";
                }
            }

            if (type.MetadataType == MetadataType.Class)
            {
                return "UNSUPPORTED";
            }

            if (type.MetadataType == MetadataType.ValueType)
            {
                return "UNSUPPORTED";
            }

            if (type.IsArray)
            {
                StringBuilder arraySig = new StringBuilder();
                arraySig.Append(type.GetElementType().ToCLRTypeAsString());
                arraySig.Append("_ARRAY");

                return arraySig.ToString();
            }

            if (type.IsGenericParameter)
            {
                return "UNSUPPORTED";
            }

            return "";
        }

        public static nanoSerializationType ToSerializationType(this TypeReference value)
        {
            nanoClrDataType dataType;
            if (nanoSignaturesTable.PrimitiveTypes.TryGetValue(value.FullName, out dataType))
            {
                switch (dataType)
                {
                    case nanoClrDataType.DATATYPE_BOOLEAN:
                        return nanoSerializationType.ELEMENT_TYPE_BOOLEAN;
                    case nanoClrDataType.DATATYPE_I1:
                        return nanoSerializationType.ELEMENT_TYPE_I1;
                    case nanoClrDataType.DATATYPE_U1:
                        return nanoSerializationType.ELEMENT_TYPE_U1;
                    case nanoClrDataType.DATATYPE_I2:
                        return nanoSerializationType.ELEMENT_TYPE_I2;
                    case nanoClrDataType.DATATYPE_U2:
                        return nanoSerializationType.ELEMENT_TYPE_U2;
                    case nanoClrDataType.DATATYPE_I4:
                        return nanoSerializationType.ELEMENT_TYPE_I4;
                    case nanoClrDataType.DATATYPE_U4:
                        return nanoSerializationType.ELEMENT_TYPE_U4;
                    case nanoClrDataType.DATATYPE_I8:
                        return nanoSerializationType.ELEMENT_TYPE_I8;
                    case nanoClrDataType.DATATYPE_U8:
                        return nanoSerializationType.ELEMENT_TYPE_U8;
                    case nanoClrDataType.DATATYPE_R4:
                        return nanoSerializationType.ELEMENT_TYPE_R4;
                    case nanoClrDataType.DATATYPE_R8:
                        return nanoSerializationType.ELEMENT_TYPE_R8;
                    case nanoClrDataType.DATATYPE_CHAR:
                        return nanoSerializationType.ELEMENT_TYPE_CHAR;
                    case nanoClrDataType.DATATYPE_STRING:
                        return nanoSerializationType.ELEMENT_TYPE_STRING;
                    default:
                        return 0;
                }
            }

            return 0;
        }

        public static ushort ToEncodedNanoTypeToken(this TypeReference value)
        {
            // implements .NET nanoFramework encoding for TypeToken
            // encodes TypeReference to be decoded with CLR_UncompressTypeToken
            // CLR tables are
            // 0: TBL_TypeDef
            // 1: TBL_TypeRef
            // 2: TBL_TypeSpec
            // 3: TBL_GenericParam

            return nanoTokenHelpers.EncodeTableIndex(value.ToNanoClrTable(), nanoTokenHelpers.NanoTypeTokenTables);
        }

        public static nanoClrTable ToNanoClrTable(this TypeReference value)
        {
            // this one has to be before the others because generic parameters are also "other" types
            if (value is GenericParameter)
            {
                return nanoClrTable.TBL_GenericParam;
            }
            else if (value is TypeDefinition)
            {
                return nanoClrTable.TBL_TypeDef;
            }
            else if (value is TypeReference)
            {
                return nanoClrTable.TBL_TypeRef;
            }
            else if (value is TypeSpecification)
            {
                return nanoClrTable.TBL_TypeSpec;
            }

            else
            {
                throw new ArgumentException("Unknown conversion to ClrTable.");
            }
        }
    }
}
