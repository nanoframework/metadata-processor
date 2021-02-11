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
    internal static class MethodSpecificationExtensions
    {
         public static ushort ToEncodedNanoMethodToken(this MethodSpecification value)
        {
            // implements .NET nanoFramework encoding for MethodToken
            // encodes Method to be decoded with CLR_UncompressMethodToken
            // CLR tables are
            // 0: TBL_MethodDef
            // 1: TBL_MethodRef

            return nanoTokenHelpers.EncodeTableIndex(value.ToNanoClrTable(), nanoTokenHelpers.NanoMethodDefOrRefTokenTables);
        }

        public static nanoClrTable ToNanoClrTable(this MethodSpecification value)
        {
            // this one has to be before the others because generic parameters are also "other" types
            if (value.Resolve() is MethodDefinition)
            {
                return nanoClrTable.TBL_MethodDef;
            }
            else if (value.Resolve() is MethodReference ||
                    value.Resolve() is MethodSpecification)
            {
                if (value.DeclaringType.Scope.MetadataScopeType == MetadataScopeType.AssemblyNameReference)
                {
                    // method ref is external
                    return nanoClrTable.TBL_MethodRef;
                }
                else
                {
                    // method ref is internal
                    return nanoClrTable.TBL_MethodDef;
                }
            }
            else
            {
                throw new ArgumentException("Unknown conversion to ClrTable.");
            }
        }
    }
}
