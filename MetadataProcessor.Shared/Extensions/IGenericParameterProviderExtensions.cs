// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Mono.Cecil;

namespace nanoFramework.Tools.MetadataProcessor.Core.Extensions
{
    internal static class IGenericParameterProviderExtensions
    {
        public static ushort ToEncodedNanoTypeOrMethodDefToken(this IGenericParameterProvider value)
        {
            // implements .NET nanoFramework encoding for MethodToken
            // encodes Method to be decoded with CLR_UncompressTypeOrMethodDefToken
            // CLR tables are
            // 0: TBL_TypeDef
            // 1: TBL_MethodDef

            return nanoTokenHelpers.EncodeTableIndex(value.ToNanoCLRTable(), nanoTokenHelpers.NanoTypeOrMethodDefTokenTables);
        }

        public static NanoClrTable ToNanoCLRTable(this IGenericParameterProvider value)
        {
            if (value is TypeDefinition)
            {
                return NanoClrTable.TBL_TypeDef;
            }
            else if (value is MethodDefinition)
            {
                return NanoClrTable.TBL_MethodDef;
            }
            else
            {
                throw new ArgumentException("Unknown conversion to CLR Table.");
            }
        }
    }
}
