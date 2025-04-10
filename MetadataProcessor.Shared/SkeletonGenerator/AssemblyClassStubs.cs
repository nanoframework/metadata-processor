// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

        public string AssemblyName;

        public List<MethodStub> Functions = new List<MethodStub>();
    }
}
