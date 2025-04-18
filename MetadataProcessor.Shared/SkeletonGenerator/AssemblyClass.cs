﻿//
// Copyright (c) .NET Foundation and Contributors
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

        public bool IsCoreLib = false;

        public List<Class> Classes = new List<Class>();

        public List<EnumDeclaration> Enums = new List<EnumDeclaration>();
    }

    public class Class
    {
        public string Name;
        public string AssemblyName;
        public string ShortNameUpper;

        public List<StaticField> StaticFields = new List<StaticField>();
        public List<InstanceField> InstanceFields = new List<InstanceField>();
        public List<MethodStub> Methods = new List<MethodStub>();
    }

    public class StaticField
    {
        public string Name;
        public int ReferenceIndex;
        public string FieldWarning;
    }

    public class InstanceField
    {
        public string Name;
        public int ReferenceIndex;

        public string FieldWarning;
    }

    public class MethodStub
    {
        public string Declaration;
        public string DeclarationForUserCode;
        public string CallFromMarshalling;
        public string ReturnType;
        public string MarshallingReturnType;
        public bool HasReturnType;
        public bool IsStatic = false;

        public List<ParameterDeclaration> ParameterDeclaration = new List<ParameterDeclaration>();
    }

    public class ParameterDeclaration
    {
        public string Index;
        public string Type;
        public string Name;
        public string MarshallingDeclaration;
        public string Declaration;
    }

    public class ClassWithStubs
    {
        public string Name;
    }

    public class EnumDeclaration
    {
        public string EnumName;
        public string FullName;
        public string Name;

        public List<EnumItem> Items = new List<EnumItem>();
    }

    public class EnumItem
    {
        public string Name;
        public string Value;
    }

}
