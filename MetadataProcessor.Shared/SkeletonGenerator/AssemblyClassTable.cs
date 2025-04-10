// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace nanoFramework.Tools.MetadataProcessor.Core
{
    public class AssemblyClassTable
    {
        public string AssemblyName;
        public string HeaderFileName;
        public string ProjectName;
        public bool IsInterop;

        public List<Class> Classes = new List<Class>();
        public List<ClassWithStubs> ClassesWithStubs = new List<ClassWithStubs>();
    }
}
