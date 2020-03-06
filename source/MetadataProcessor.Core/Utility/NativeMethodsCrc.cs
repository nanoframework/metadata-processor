//
// Copyright (c) 2019 The nanoFramework project contributors
// Original work from Oleg Rakhmatulin.
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Helper class for calculating native methods CRC value. Really calculates CRC32 value
    /// for native method signatures (not methods itself) and signatures treated as string
    /// values, formatted by weird rules backported from .NETMF original implementation.
    /// </summary>
    public sealed class NativeMethodsCrc
    {
        private readonly byte[] _null = Encoding.ASCII.GetBytes("NULL");

        private readonly byte[] _name;

        private readonly List<string> _classNamesToExclude;

        public NativeMethodsCrc(
            AssemblyDefinition assembly,
            List<string> classNamesToExclude)
        {
            _name = Encoding.ASCII.GetBytes(assembly.Name.Name);
            _classNamesToExclude = classNamesToExclude;
        }

        public uint Current { get; private set; }

        public void UpdateCrc(MethodDefinition method)
        {
            var type = method.DeclaringType;

            if (type.IncludeInStub() &&
                (method.RVA == 0 && !method.IsAbstract) )
            {
                Current = Crc32.Compute(_name, Current);
                Current = Crc32.Compute(Encoding.ASCII.GetBytes(GetClassName(type)), Current);
                Current = Crc32.Compute(Encoding.ASCII.GetBytes(GetMethodName(method)), Current);
            }
            else
            {
                Current = Crc32.Compute(_null, Current);
            }
        }

        internal static string GetClassName(
            TypeDefinition type)
        {
            return (type != null
                ? string.Join("_", GetClassName(type.DeclaringType), type.Namespace, type.Name)
                    .Replace(".", "_").TrimStart('_')
                : string.Empty);
        }

        internal static string GetMethodName(
            MethodDefinition method)
        {
            var name = string.Concat(method.Name, (method.IsStatic ? "___STATIC__" : "___"),
                string.Join("__", GetAllParameters(method)));

            var originalName = name.Replace(".", "_")
                                .Replace("/", "");

            return originalName;
        }

        private static IEnumerable<string> GetAllParameters(
            MethodDefinition method)
        {
            yield return GetParameterType(method.ReturnType);

            if (method.HasParameters)
            {
                foreach (var item in method.Parameters)
                {
                    yield return GetParameterType(item.ParameterType);
                }
            }
        }

        private static string GetParameterType(
            TypeReference parameterType)
        {
            var typeName = "";
            bool continueProcessing = true;

            // special processing for arrays
            if(parameterType.IsArray)
            {
                typeName += nanoCLR_DataType.DATATYPE_SZARRAY + "_" + GetnanoClrTypeName(parameterType.GetElementType());
                continueProcessing = false;
            }
            else if (parameterType.IsByReference)
            {
                var elementType = ((TypeSpecification)parameterType).ElementType;

                typeName += nanoCLR_DataType.DATATYPE_BYREF + "_";

                if (elementType.IsArray)
                {
                    typeName += nanoCLR_DataType.DATATYPE_SZARRAY + "_" + GetnanoClrTypeName(elementType.GetElementType());
                }
                else
                {
                    typeName += GetnanoClrTypeName(elementType);
                }
                continueProcessing = false;
            }
            else if(!parameterType.IsPrimitive)
            {
                // TBD
                continueProcessing = true;
            }

            if (continueProcessing)
            {
                typeName = GetnanoClrTypeName(parameterType);
            }

            // clear 'DATATYPE_' prefixes 
            // and make it upper case
            return typeName.Replace("DATATYPE_", "");
        }

        internal static string GetnanoClrTypeName(TypeReference parameterType)
        {
            // try getting primitive type

            nanoCLR_DataType myType;
            if(nanoSignaturesTable.PrimitiveTypes.TryGetValue(parameterType.FullName, out myType))
            {
                if (myType == nanoCLR_DataType.DATATYPE_LAST_PRIMITIVE)
                {
                    return "DATATYPE_STRING";
                }
                else if (myType == nanoCLR_DataType.DATATYPE_LAST_NONPOINTER)
                {
                    return "DATATYPE_TIMESPAN";
                }
                else if (myType == nanoCLR_DataType.DATATYPE_LAST_PRIMITIVE_TO_MARSHAL)
                {
                    return "DATATYPE_TIMESPAN";
                }
                else if (myType == nanoCLR_DataType.DATATYPE_LAST_PRIMITIVE_TO_PRESERVE)
                {
                    return "DATATYPE_R8";
                }
                else
                {
                    return myType.ToString();
                }
            }
            else
            {
                // type is not primitive, get full qualified type name
                return parameterType.FullName.Replace(".", String.Empty);
            }
        }

        internal void UpdateCrc(nanoTypeDefinitionTable typeDefinitionTable)
        {
            foreach (var c in typeDefinitionTable.Items)
            {
                if (c.IncludeInStub() && !IsClassToExclude(c))
                {
                    foreach (var m in nanoTablesContext.GetOrderedMethods(c.Methods))
                    {
                        UpdateCrc(m);
                    }
                }
            }
        }

        private bool IsClassToExclude(TypeDefinition td)
        {
            return (_classNamesToExclude.Contains(td.FullName) ||
                    _classNamesToExclude.Contains(td.DeclaringType?.FullName));
        }
    }
}
