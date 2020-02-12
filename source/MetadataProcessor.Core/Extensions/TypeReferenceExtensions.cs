//
// Copyright (c) 2019 The nanoFramework project contributors
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;

namespace nanoFramework.Tools.MetadataProcessor.Core.Extensions
{
    internal static class TypeReferenceExtensions
    {
        public static bool IsToInclude(this TypeReference value)
        {
            return !nanoTablesContext.IgnoringAttributes.Contains(value.FullName);
        }
    }
}
