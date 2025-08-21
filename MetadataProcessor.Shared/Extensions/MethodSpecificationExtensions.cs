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
    }
}
