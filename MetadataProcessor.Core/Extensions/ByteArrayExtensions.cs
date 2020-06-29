//
// Copyright (c) 2019 The nanoFramework project contributors
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using System.Text;
using System.Linq;

namespace nanoFramework.Tools.MetadataProcessor.Core.Extensions
{
    internal static class ByteArrayExtensions
    {
        public static string BufferToHexString(this byte[] buffer)
        {
            StringBuilder output = new StringBuilder();

            foreach(byte b in buffer)
            {
                output.Append(b.ToString("X2"));
            }

            return output.ToString();
        }
    }
}
