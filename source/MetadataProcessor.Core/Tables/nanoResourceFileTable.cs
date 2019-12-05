//
// Copyright (c) 2019 The nanoFramework project contributors
// Original work from Oleg Rakhmatulin.
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Encapsulates logic for storing resource files definitions list and writing
    /// this collected list into target assembly in .NET nanoFramework format.
    /// </summary>
    public sealed class nanoResourceFileTable : InanoTable
    {
        /// <summary>
        /// Assembly tables context - contains all tables used for building target assembly.
        /// </summary>
        private readonly nanoTablesContext _context;

        /// <summary>
        /// List of resource fiels data for producing correct output records.
        /// </summary>
        private readonly IList<Tuple<ushort, uint>> _resouces = new List<Tuple<ushort, uint>>();

        /// <summary>
        /// Creates new instance of <see cref="nanoResourceFileTable"/> object.
        /// </summary>
        /// <param name="context">
        /// Assembly tables context - contains all tables used for building target assembly.
        /// </param>
        public nanoResourceFileTable(
            nanoTablesContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Adds infromation about embedded resource file in Mono.Cecil format into list.
        /// </summary>
        /// <param name="item">Embedded resouce file information in Mono.Cecil format.</param>
        /// <param name="count">Number of recource items in this resouce file.</param>
        public void AddResourceFile(
            EmbeddedResource item,
            uint count)
        {
            var correctFileName = Path.ChangeExtension(item.Name, ".tinyresources");
            var nameStringId = _context.StringTable.GetOrCreateStringId(correctFileName);

            _resouces.Add(new Tuple<ushort, uint>(nameStringId, count));
        }

        /// <inheritdoc/>
        public void Write(
            nanoBinaryWriter writer)
        {
            var offset = 0U;
            foreach (var item in _resouces)
            {
                writer.WriteUInt32(0x02); // Version
                writer.WriteUInt32(0x04); // Size of header
                writer.WriteUInt32(0x08); // Size of resouce header

                writer.WriteUInt32(item.Item2);
                
                writer.WriteUInt16(item.Item1);
                writer.WriteUInt16(0x0); // paddding

                writer.WriteUInt32(offset);
                offset += 8; // Size of resource table record
            }

        }
    }
}