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

        private static NanoCLRTable ToNanoCLRTable(this MethodReference value)
        {
            return nanoTokenHelpers.ConvertToNanoCLRTable(value);
        }
    }
}