//
// Copyright (c) 2019 The nanoFramework project contributors
// Original work from Oleg Rakhmatulin.
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Helper class for calculating native methods CRC value. Really caclulates CRC32 value
    /// for native method signatures (not methods itself) and signatures treated as string
    /// values, formatted by weird rules incompartible with all rest codebase.
    /// </summary>
    public sealed class NativeMethodsCrc
    {
        private readonly HashSet<string> _generatedNames = new HashSet<string>(StringComparer.Ordinal);

        private readonly byte[] _null = Encoding.ASCII.GetBytes("NULL");

        private readonly byte[] _name;

        public NativeMethodsCrc(
            AssemblyDefinition assembly)
        {
            _name = Encoding.ASCII.GetBytes(assembly.Name.Name);
        }

        public uint Current { get; private set; }

        public void UpdateCrc(MethodDefinition method)
        {
            var type = method.DeclaringType;
            if ((type.IsClass || type.IsValueType) &&
                (method.RVA == 0xFFFFFFF && !method.IsAbstract))
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

        private string GetClassName(
            TypeDefinition type)
        {
            return (type != null
                ? string.Concat(GetClassName(type.DeclaringType), type.Namespace, type.Name)
                    .Replace(".", string.Empty)
                : string.Empty);
        }

        private string GetMethodName(
            MethodDefinition method)
        {
            var name = string.Concat(method.Name, (method.IsStatic ? "___STATIC__" : "___"),
                string.Join("__", GetAllParameters(method)));

            var originalName = name.Replace(".", string.Empty);

            var index = 1;
            name = originalName;
            while (_generatedNames.Add(name))
            {
                name = string.Concat(originalName, index.ToString(CultureInfo.InvariantCulture));
                ++index;
            }

            return name;
        }

        private IEnumerable<string> GetAllParameters(
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

        private string GetParameterType(
            TypeReference parameterType)
        {
            return parameterType.Name.ToUpper();
        }
    }
}
