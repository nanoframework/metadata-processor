// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Mono.Cecil;

namespace nanoFramework.Tools.MetadataProcessor.Core.Extensions
{
    internal static class TypeSpecificationExtensions
    {
        public static bool IsToExclude(this TypeSpecification value)
        {
            return nanoTablesContext.ClassNamesToExclude.Contains(value.FullName) ||
                   nanoTablesContext.ClassNamesToExclude.Contains(value.Name) ||
                   nanoTablesContext.ClassNamesToExclude.Contains(value.ElementType.FullName) ||
                   nanoTablesContext.ClassNamesToExclude.Contains(value.DeclaringType?.FullName);
        }
    }
}
