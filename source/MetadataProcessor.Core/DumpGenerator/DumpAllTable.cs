//
// Copyright (c) 2020 The nanoFramework project contributors
// See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;

namespace nanoFramework.Tools.MetadataProcessor.Core
{
    public class DumpAllTable
    {
        public List<AssemblyRef> AssemblyReferences = new List<AssemblyRef>();
        public List<TypeRef> TypeReferences = new List<TypeRef>();
        public List<TypeDef> TypeDefinitions = new List<TypeDef>();
        public List<AttributeCustom> Attributes = new List<AttributeCustom>();
        public List<UserString> UserStrings = new List<UserString>();
    }
}
