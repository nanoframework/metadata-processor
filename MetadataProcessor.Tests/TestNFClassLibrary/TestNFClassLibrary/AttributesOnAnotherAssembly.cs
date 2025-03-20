//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System;

namespace TestNFClassLibrary
{
    [AttributeUsage(AttributeTargets.All)]
    public class Attribute1OnAnotherAssemblyAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.All)]
    public class Attribute2OnAnotherAssemblyAttribute : Attribute
    {
    }
}
