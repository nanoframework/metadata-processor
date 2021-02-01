//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// .NET nanoFramework PE tables index.
    /// </summary>
    public enum nanoClrTable
    {
        //////////////////////////////////////////////////////////////////////////////////////
        // !!! KEEP IN SYNC WITH enum nanoClrTable (in nanoCLR_TypeSystem VS extension) !!! //
        // !!! KEEP IN SYNC WITH enum nanoClrTable (in nanoCLRT_Types.h in CLR)         !!! //
        //////////////////////////////////////////////////////////////////////////////////////

        TBL_AssemblyRef = 0x00000000,
        TBL_TypeRef = 0x00000001,
        TBL_FieldRef = 0x00000002,
        TBL_MethodRef = 0x00000003,
        TBL_TypeDef = 0x00000004,
        TBL_FieldDef = 0x00000005,
        TBL_MethodDef = 0x00000006,
        TBL_MemberRef = 0x00000007,
        TBL_GenericParam = 0x00000008,
        TBL_MethodSpec = 0x00000009,
        TBL_TypeSpec = 0x0000000A,
        TBL_Attributes = 0x0000000B,
        TBL_Resources = 0x0000000C,
        TBL_ResourcesData = 0x0000000D,
        TBL_Strings = 0x0000000E,
        TBL_Signatures = 0x0000000F,
        TBL_ByteCode = 0x00000010,
        TBL_ResourcesFiles = 0x00000011,
        TBL_EndOfAssembly = 0x000000012
    }
}
