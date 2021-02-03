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

            return nanoTokenHelpers.EncodeTableIndex(value.ToNanoClrTable(), nanoTokenHelpers.NanoMemberRefTokenTables);
        }

        public static nanoClrTable ToNanoClrTable(this MemberReference value)
        {
            // this one has to be before the others because generic parameters are also "other" types
            if (value is MethodDefinition)
            {
                return nanoClrTable.TBL_MethodDef;
            }
            else if (value is MethodSpecification)
            {
                return nanoClrTable.TBL_MethodSpec;
            }
            else if (value.DeclaringType is TypeSpecification)
            {
                return nanoClrTable.TBL_TypeSpec;
            }
            else if (value.Resolve() is MethodReference)
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
