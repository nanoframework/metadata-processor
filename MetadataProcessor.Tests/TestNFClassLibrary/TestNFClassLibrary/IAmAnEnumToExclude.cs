﻿//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System.Runtime.CompilerServices;

namespace TestNFClassLibrary
{
    // this type does nothing, its here to test the ExcludeType attribute
    [ExcludeType]
    public enum IAmAnEnumToExclude
    {
        None = 0,
        Test = 1,
        Test2 = 2
    }
}
