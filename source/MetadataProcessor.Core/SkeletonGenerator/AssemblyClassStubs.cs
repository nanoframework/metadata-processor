//
// Copyright (c) 2019 The nanoFramework project contributors
// See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;

namespace nanoFramework.Tools.MetadataProcessor.Core
{
    public class AssemblyClassStubs
    {
        public string HeaderFileName;

        public string ClassHeaderFileName;

        public string ClassName;

        public string ShortNameUpper;

        public string RootNamespace;

        public string ProjectName;

        public List<MethodStub> Functions = new List<MethodStub>();
    }
}
