// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
