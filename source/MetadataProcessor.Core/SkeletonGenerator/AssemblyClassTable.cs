//
// Copyright (c) 2019 The nanoFramework project contributors
// See LICENSE file in the project root for full license information.
//

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
