// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Mono.Cecil;

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

            return nanoTokenHelpers.EncodeTableIndex(value.ToNanoCLRTable(), nanoTokenHelpers.NanoMethodDefOrRefTokenTables);
        }

        public static NanoClrTable ToNanoCLRTable(this MethodSpecification value)
        {
            // this one has to be before the others because generic parameters are also "other" types
            if (value.Resolve() is MethodDefinition)
            {
                return NanoClrTable.TBL_MethodDef;
            }
            else if (value.Resolve() is MethodReference ||
                    value.Resolve() is MethodSpecification)
            {
                if (value.DeclaringType.Scope.MetadataScopeType == MetadataScopeType.AssemblyNameReference)
                {
                    // method ref is external
                    return NanoClrTable.TBL_MethodRef;
                }
                else
                {
                    // method ref is internal
                    return NanoClrTable.TBL_MethodDef;
                }
            }
            else
            {
                throw new ArgumentException("Unknown conversion to CLR Table.");
            }
        }
    }
}
