// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Mono.Cecil;

namespace nanoFramework.Tools.MetadataProcessor.Core.Extensions
{
    internal static class MemberReferenceExtensions
    {
        public static ushort ToEncodedNanoMethodToken(this MemberReference value)
        {
            // implements .NET nanoFramework encoding for MethodToken
            // encodes Method to be decoded with CLR_UncompressMethodToken
            // CLR tables are
            // 0: TBL_MethodDef
            // 1: TBL_MethodRef
            // 3: TBL_TypeSpec
            // 4: TBL_MethodSpec

            return nanoTokenHelpers.EncodeTableIndex(value.ToNanoCLRTable(), nanoTokenHelpers.NanoMemberRefTokenTables);
        }

        public static NanoClrTable ToNanoCLRTable(this MemberReference value)
        {
            // this one has to be before the others because generic parameters are also "other" types
            if (value is MethodDefinition)
            {
                return NanoClrTable.TBL_MethodDef;
            }
            else if (value is MethodSpecification)
            {
                return NanoClrTable.TBL_MethodSpec;
            }
            else if (value.Resolve() is MethodReference)
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
            else if (value.DeclaringType is TypeSpecification)
            {
                return NanoClrTable.TBL_TypeSpec;
            }
            else
            {
                throw new ArgumentException("Unknown conversion to CLR Table.");
            }
        }
    }
}
