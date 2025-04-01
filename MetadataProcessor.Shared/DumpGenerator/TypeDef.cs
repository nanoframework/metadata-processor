// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace nanoFramework.Tools.MetadataProcessor.Core
{
    public class TypeDef
    {
        public string ReferenceId;

        public string Flags;

        public string ExtendsType;

        public string EnclosedType;

        public string Name;

        public List<GenericParam> GenericParameters = new List<GenericParam>();
        public List<FieldDef> FieldDefinitions = new List<FieldDef>();
        public List<MethodDef> MethodDefinitions = new List<MethodDef>();
        public List<InterfaceDef> InterfaceDefinitions = new List<InterfaceDef>();

    }
}
