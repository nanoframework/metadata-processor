//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using System;
using System.Linq;
using System.Text;

namespace nanoFramework.Tools.MetadataProcessor.Core.Extensions
{
    internal static class IGenericParameterProviderExtensionsExtensions
    {
         public static ushort ToEncodedNanoTypeOrMethodDefToken(this IGenericParameterProvider value)
        {
            // implements .NET nanoFramework encoding for MethodToken
            // encodes Method to be decoded with CLR_UncompressTypeOrMethodDefToken
            // CLR tables are
            // 0: TBL_TypeDef
            // 1: TBL_MethodDef

            return nanoTokenHelpers.EncodeTableIndex(value.ToNanoClrTable(), nanoTokenHelpers.NanoTypeOrMethodDefTokenTables);
        }

        public static ClrTable ToNanoClrTable(this IGenericParameterProvider value)
        {
            if (value is TypeDefinition)
            {
                return ClrTable.TypeDef;
            }
            else if (value is MethodDefinition)
            {
                return ClrTable.MethodDef;
            }
            else
            {
                throw new ArgumentException("Unknown conversion to ClrTable.");
            }
        }
    }
}
