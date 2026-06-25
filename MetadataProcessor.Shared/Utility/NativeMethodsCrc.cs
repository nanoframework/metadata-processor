// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Original work from Oleg Rakhmatulin.

using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Helper class for calculating native methods CRC value. Really calculates CRC32 value
    /// for native method signatures (not methods itself) and signatures treated as string
    /// values, formatted by weird rules backported from .NETMF original implementation.
    /// </summary>
    public sealed class NativeMethodsCrc
    {
        private readonly byte[] _null = Encoding.ASCII.GetBytes("nullptr");

        private readonly byte[] _name;

        private int _methodsWithNativeImplementation = 0;
        private uint _currentCrc = 0;
        private readonly List<string> _crcLog = new List<string>();

        public NativeMethodsCrc(
            AssemblyDefinition assembly)
        {
            _name = Encoding.ASCII.GetBytes(assembly.Name.Name);
        }

        /// <summary>
        /// Current CRC32 of the native methods.
        /// Will return 0 if there are no methods with native implementation.
        /// </summary>
        public uint CurrentCrc
        {
            get
            {
                if (_methodsWithNativeImplementation > 0)
                {
                    return _currentCrc;
                }
                else
                {
                    return 0;
                }
            }
        }

        public void UpdateCrc(MethodDefinition method)
        {
            TypeDefinition type = method.DeclaringType;

            // Always update CRC for every method to make it position-dependent.
            // This ensures any structural change (adding methods, reordering, etc.) will change the CRC.
            string logClassName = null;
            string logMethodName = null;

            if (type.IncludeInStub() &&
                (method.RVA == 0 && !method.IsAbstract))
            {
                logClassName = GetSafeClassName(type);
                logMethodName = GetSafeMethodName(method);

                _currentCrc = Crc32.Compute(_name, _currentCrc);
                _currentCrc = Crc32.Compute(Encoding.ASCII.GetBytes(logClassName), _currentCrc);
                _currentCrc = Crc32.Compute(Encoding.ASCII.GetBytes(logMethodName), _currentCrc);

                _methodsWithNativeImplementation++;
            }

            // Always add nullptr marker to make CRC position-dependent
            _currentCrc = Crc32.Compute(_null, _currentCrc);

            if (logClassName != null)
            {
                _crcLog.Add($"  [{_methodsWithNativeImplementation,4}] 0x{_currentCrc:X8}  {logClassName}::{logMethodName}");
            }
        }

        internal static string GetSafeClassName(TypeDefinition type)
        {
            string className = (type != null
                ? string.Join("_", GetSafeClassName(type.DeclaringType), type.Namespace, type.Name)
                    .Replace(".", "_")
                    .TrimStart('_')
                : string.Empty);

            return CleanupGenericName(className);
        }

        internal static string GetSafeMethodName(MethodDefinition method)
        {
            string name = string.Concat(method.Name, (method.IsStatic ? "___STATIC__" : "___"),
                string.Join("__", GetAllParameters(method)));

            string originalName = name.Replace(".", "_")
                                .Replace("/", "");

            return CleanupGenericName(originalName);
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
            if (parameterType.IsArray)
            {
                typeName += NanoCLRDataType.DATATYPE_SZARRAY + "_" + GetParameterType(parameterType.GetElementType());
                continueProcessing = false;
            }
            else if (parameterType.IsByReference)
            {
                var elementType = ((TypeSpecification)parameterType).ElementType;

                typeName += NanoCLRDataType.DATATYPE_BYREF + "_";

                if (elementType.IsArray)
                {
                    typeName += NanoCLRDataType.DATATYPE_SZARRAY + "_" + GetParameterType(((TypeSpecification)elementType).ElementType);
                }
                else
                {
                    typeName += GetNanoCLRTypeName(elementType);
                }
                continueProcessing = false;
            }
            else if (!parameterType.IsPrimitive)
            {
                // TBD
                continueProcessing = true;
            }

            if (continueProcessing)
            {
                typeName = GetNanoCLRTypeName(parameterType);
            }

            // clear 'DATATYPE_' prefixes 
            // and make it upper case
            return typeName.Replace("DATATYPE_", "");
        }

        internal static string GetNanoCLRTypeName(TypeReference parameterType)
        {
            // try getting primitive type

            NanoCLRDataType myType;
            if (nanoSignaturesTable.PrimitiveTypes.TryGetValue(parameterType.FullName, out myType))
            {
                if (myType == NanoCLRDataType.DATATYPE_LAST_PRIMITIVE)
                {
                    return "DATATYPE_STRING";
                }
                else if (myType == NanoCLRDataType.DATATYPE_LAST_NONPOINTER)
                {
                    return "DATATYPE_TIMESPAN";
                }
                else if (myType == NanoCLRDataType.DATATYPE_LAST_PRIMITIVE_TO_MARSHAL)
                {
                    return "DATATYPE_TIMESPAN";
                }
                else if (myType == NanoCLRDataType.DATATYPE_LAST_PRIMITIVE_TO_PRESERVE)
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
                // type is not primitive

                if (parameterType.IsGenericParameter)
                {
                    // check if it's generic
                    return "DATATYPE_GENERICTYPE";
                }
                else if (parameterType.IsPointer)
                {
                    if (nanoSignaturesTable.PrimitiveTypes.TryGetValue(parameterType.GetElementType().FullName, out myType))
                    {
                        return $"{myType}ptr";
                    }
                }

                // last attempt: get full qualified type name
                string typeName = parameterType.FullName.Replace(".", string.Empty);

                return CleanupGenericName(typeName);
            }
        }

        /// <summary>
        /// Returns the collected CRC log entries, one per native method processed.
        /// Each entry includes the running index, the CRC after that method and the safe method signature.
        /// </summary>
        public IReadOnlyList<string> GetCrcLog() => _crcLog;

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
            return (nanoTablesContext.ClassNamesToExclude.Contains(td.FullName) ||
                    nanoTablesContext.ClassNamesToExclude.Contains(td.DeclaringType?.FullName));
        }

        internal static string CleanupGenericName(string name)
        {
            // Replace the CLR backtick-N generic arity notation with an underscore
            // (e.g. Dictionary`2 → Dictionary_2). The trailing _N is the arity of the
            // generic type and is the correct disambiguation token — it is unique per
            // type definition and matches what the rest of the toolchain (CRC, method
            // lookup) emits.
            string fixedName = name
                    .Replace('`', '_');

            return Regex.Replace(fixedName, @"<[^>]*>", string.Empty);
        }
    }
}
