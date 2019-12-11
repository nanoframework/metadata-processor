﻿//
// Copyright (c) 2019 The nanoFramework project contributors
// See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;

namespace nanoFramework.Tools.MetadataProcessor.Core
{
    public class AssemblyLookupTable
    {
        public string Name;
        public string AssemblyName;
        public string HeaderFileName;
        public string NativeCRC32;

        public Version NativeVersion;

        public List<Method> LookupTable = new List<Method>();
    }
}