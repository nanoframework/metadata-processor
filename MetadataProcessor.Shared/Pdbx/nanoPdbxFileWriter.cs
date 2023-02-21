//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using nanoFramework.Tools.MetadataProcessor.Core;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml;

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
