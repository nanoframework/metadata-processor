//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

namespace nanoFramework.Tools.MetadataProcessor.Core.Extensions
{
    internal static class ClrTableExtensions
    {
         public static uint ToNanoTokenType(this ClrTable value)
         {
            return ((uint)value << 24);
         }
    }
}
