// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace nanoFramework.Tools.MetadataProcessor.Core.Extensions
{
    internal static class ByteArrayExtensions
    {
        public static string BufferToHexString(this byte[] buffer)
        {
            StringBuilder output = new StringBuilder();

            foreach (byte b in buffer)
            {
                output.Append(b.ToString("X2"));
            }

            return output.ToString();
        }
    }
}
