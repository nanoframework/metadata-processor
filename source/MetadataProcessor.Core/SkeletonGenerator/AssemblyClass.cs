//
// Copyright (c) 2019 The nanoFramework project contributors
// See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;

namespace nanoFramework.Tools.MetadataProcessor.Core
{
    public class AssemblyDeclaration
    {
        public string Name;
        public string ShortName;
        public string ShortNameUpper;

        public List<Class> Classes = new List<Class>();
    }

    public class Class
    {
        public string Name;
        public string AssemblyName;

        public List<StaticField> StaticFields = new List<StaticField>();
        public List<InstanceField> InstanceFields = new List<InstanceField>();
        public List<Method> Methods = new List<Method>();
    }

    public class StaticField
    {
        public string Name;
        public int ReferenceIndex;
    }

    public class InstanceField
    {
        public string Name;
        public int ReferenceIndex;

        public string FieldWarning;
    }

    public class Method
    {
        public string Declaration;
    }
}
