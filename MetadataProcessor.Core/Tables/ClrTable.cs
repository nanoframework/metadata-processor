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
    public enum ClrTable
    {
        // this follows enum CLR_TABLESENUM @ nanoCLR_Types.h

        AssemblyRef = 0x00000000,
        TypeRef = 0x00000001,
        FieldRef = 0x00000002,
        MethodRef = 0x00000003,
        TypeDef = 0x00000004,
        FieldDef = 0x00000005,
        MethodDef = 0x00000006,
        GenericParam = 0x00000007,
        MethodSpec = 0x00000008,
        Attributes = 0x00000009,
        TypeSpec = 0x0000000A,
        Resources = 0x0000000B,
        ResourcesData = 0x0000000C,
        Strings = 0x0000000D,
        Signatures = 0x0000000E,
        ByteCode = 0x0000000F,
        ResourcesFiles = 0x00000010,
        EndOfAssembly = 0x000000011
    }
}
