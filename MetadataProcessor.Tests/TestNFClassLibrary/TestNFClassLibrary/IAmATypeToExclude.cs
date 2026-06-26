// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace TestNFClassLibrary
{
    // this type does nothing; it is used to test type exclusion via ExcludeTypeAttribute.
    [ExcludeType]
    public class IAmATypeToExclude
    {
    }
}
