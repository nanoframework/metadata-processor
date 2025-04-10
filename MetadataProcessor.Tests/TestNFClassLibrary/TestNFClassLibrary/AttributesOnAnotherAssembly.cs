// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
