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
        MemberRef = 0x00000007,
        GenericParam = 0x00000008,
        MethodSpec = 0x00000009,
        Attributes = 0x0000000A,
        TypeSpec = 0x0000000B,
        Resources = 0x0000000C,
        ResourcesData = 0x0000000D,
        Strings = 0x0000000E,
        Signatures = 0x0000000F,
        ByteCode = 0x00000010,
        ResourcesFiles = 0x00000011,
        EndOfAssembly = 0x000000012
    }
}
