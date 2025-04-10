// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace nanoFramework.Tools.MetadataProcessor.Core.Extensions
{
    internal static class ClrTableExtensions
    {
        public static uint ToNanoTokenType(this NanoClrTable value)
        {
            return ((uint)value << 24);
        }
    }
}
