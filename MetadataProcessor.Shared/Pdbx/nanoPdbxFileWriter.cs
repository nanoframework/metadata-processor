// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace nanoFramework.Tools.MetadataProcessor
{
    internal sealed class nanoPdbxFileWriter
    {
        private readonly nanoTablesContext _context;

        public nanoPdbxFileWriter(
            nanoTablesContext context)
        {
            _context = context;
        }

        internal void Write(string fileName)
        {
            Pdbx pdbxFile = new Pdbx(_context);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var pdbxContent = JsonSerializer.SerializeToUtf8Bytes(pdbxFile, options);
            File.WriteAllBytes(fileName, pdbxContent);
        }
    }
}
