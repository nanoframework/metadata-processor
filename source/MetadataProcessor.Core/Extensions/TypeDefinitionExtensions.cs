//
// Copyright (c) 2019 The nanoFramework project contributors
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using System;

namespace nanoFramework.Tools.MetadataProcessor.Core.Extensions
{
    internal static class TypeDefinitionExtensions
    {
        public static bool IncludeInStub(this TypeDefinition value)
        {
            var typeDefFlags = nanoTypeDefinitionTable.GetFlags(value);

            if ( typeDefFlags.HasFlag(nanoTypeDefinitionFlags.TD_Delegate) ||
                 typeDefFlags.HasFlag(nanoTypeDefinitionFlags.TD_MulticastDelegate))
            {
                return false;
            }

            typeDefFlags = typeDefFlags & nanoTypeDefinitionFlags.TD_Semantics;

            // Only generate a stub for classes and value types.
            if  (typeDefFlags == nanoTypeDefinitionFlags.TD_Semantics_Class ||
                 typeDefFlags == nanoTypeDefinitionFlags.TD_Semantics_ValueType )
            { 
                return true;
            }

            return false;
        }

        public static bool IsToExclude(this TypeDefinition value)
        {
            return nanoTablesContext.ClassNamesToExclude.Contains(value.FullName) ||
                   nanoTablesContext.ClassNamesToExclude.Contains(value.DeclaringType?.FullName);
        }

        public static EnumDeclaration ToEnumDeclaration(this TypeDefinition source)
        {
            // sanity check (to prevent missuse)
            if(!source.IsEnum)
            {
                throw new ArgumentException("Can clone only TypeDefinition that are Enums.");
            }

            EnumDeclaration myEnum = new EnumDeclaration()
            {
                EnumName = source.Name
            };

            foreach (var f in source.Fields)
            {
                if (f.Name == "value__")
                {
                    // skip value field
                    continue;
                }
                else
                {
                    // enum items are named with the enum name followed by the enum item and respective value
                    // pattern: nnnn_yyyyy
                    var emunItem = new EnumItem()
                        {
                            Name = $"{source.Name}_{f.Name}",
                        };

                    emunItem.Value = f.Constant.ToString();

                    myEnum.Items.Add(emunItem);
                }
            }

            return myEnum;
        }
    }
}
