﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// .NET nanoFramework PE tables index.
    /// </summary>
    public enum NanoClrTable
    {
        //////////////////////////////////////////////////////////////////////////////////////
        // !!! KEEP IN SYNC WITH enum NanoCLRTable (in NanoCLR_TypeSystem VS extension) !!! //
        // !!! KEEP IN SYNC WITH enum NanoCLRTable (in NanoCLR_Types.h in CLR)         !!! //
        //////////////////////////////////////////////////////////////////////////////////////

        TBL_AssemblyRef = 0x00000000,
        TBL_TypeRef = 0x00000001,
        TBL_FieldRef = 0x00000002,
        TBL_MethodRef = 0x00000003,
        TBL_TypeDef = 0x00000004,
        TBL_FieldDef = 0x00000005,
        TBL_MethodDef = 0x00000006,
        TBL_GenericParam = 0x00000007,
        TBL_MethodSpec = 0x00000008,
        TBL_TypeSpec = 0x00000009,
        TBL_Attributes = 0x0000000A,
        TBL_Resources = 0x0000000B,
        TBL_ResourcesData = 0x0000000C,
        TBL_Strings = 0x0000000D,
        TBL_Signatures = 0x0000000E,
        TBL_ByteCode = 0x000000F,
        TBL_ResourcesFiles = 0x00000010,
        TBL_EndOfAssembly = 0x000000011
    }
}
