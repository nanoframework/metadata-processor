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

        public List<Method> Functions = new List<Method>();
    }
}
