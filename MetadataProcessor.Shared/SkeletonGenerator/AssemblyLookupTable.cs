// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace nanoFramework.Tools.MetadataProcessor.Core
{
    public class AssemblyLookupTable
    {
        public bool IsCoreLib;

        public string Name;
        public string AssemblyName;
        public string HeaderFileName;
        public string NativeCRC32;

        public Version NativeVersion;

        public List<MethodStub> LookupTable = new List<MethodStub>();
    }
}
