//
// Copyright (c) 2019 The nanoFramework project contributors
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;

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

        public static bool IsClassToExclude(this TypeDefinition value)
        {
            return nanoTablesContext.ClassNamesToExclude.Contains(value.FullName) ||
                   nanoTablesContext.ClassNamesToExclude.Contains(value.DeclaringType?.FullName);
        }
    }
}
