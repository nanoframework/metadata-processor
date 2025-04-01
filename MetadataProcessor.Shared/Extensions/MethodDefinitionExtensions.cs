// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Text;
using Mono.Cecil;

namespace nanoFramework.Tools.MetadataProcessor.Core.Extensions
{
    internal static class MethodDefinitionExtensions
    {
        public static string FullName(this MethodDefinition value)
        {
            if (value.GenericParameters.Count == 0)
            {
                return value.Name;
            }
            else
            {
                StringBuilder name = new StringBuilder(value.Name);
                name.Append("<");

                foreach (var p in value.GenericParameters)
                {
                    name.Append(p.Name);
                    if (!p.Equals(value.GenericParameters.Last()))
                    {
                        name.Append(",");
                    }
                }

                name.Append(">");

                return name.ToString();
            }
        }

        /// <summary>
        /// Fixed full name with simplified type names.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string FixedFullName(this MethodDefinition value)
        {
            return nanoHelpers.FixTypeNames(value.FullName);
        }

        /// <summary>
        /// Fixed name with simplified type names.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string FixedName(this MethodDefinition value)
        {
            return nanoHelpers.FixTypeNames(value.Name);
        }
    }
}
