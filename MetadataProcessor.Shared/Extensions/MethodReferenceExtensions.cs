// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Mono.Cecil;

namespace nanoFramework.Tools.MetadataProcessor.Core.Extensions
{
    internal static class MethodReferenceExtensions
    {
        public static ushort ToEncodedNanoMethodToken(this MethodReference value)
        {
            // implements .NET nanoFramework encoding for MethodToken
            // encodes Method to be decoded with CLR_UncompressMethodToken
            // CLR tables are
            // 0: TBL_MethodDef
            // 1: TBL_MethodRef

            return nanoTokenHelpers.EncodeTableIndex(value.ToNanoCLRTable(), nanoTokenHelpers.NanoMemberRefTokenTables);
        }

        private static NanoClrTable ToNanoCLRTable(this MethodReference value)
        {
            return nanoTokenHelpers.ConvertToNanoCLRTable(value);
        }
    }
}
