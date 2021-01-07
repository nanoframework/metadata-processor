//
// Copyright (c) .NET Foundation and Contributors
// Original work from Oleg Rakhmatulin.
// See LICENSE file in the project root for full license information.
//

using System.IO;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Encapsulates logic for assembly definition (header) writing.
    /// </summary>
    public sealed class nanoAssemblyDefinition
    {
        /// <summary>
        /// .NET nanoFramework assembly marker V1.
        /// </summary>
        /// <remarks>
        /// Not to be used. Kept for historical reasons.
        /// </remarks>
        [System.Obsolete("Not to be used. Kept for historical reasons. Use V2 instead.")]
        private const string c_NFAssemblyMarker_v1 = "NFMRK1";

        /// <summary>
        /// .NET nanoFramework assembly marker V2.
        /// </summary>
        private const string c_NFAssemblyMarker_v2 = "NFMRK2";

        /// <summary>
        /// Position of Assembly CRC32 in the PE file.
        /// </summary>
        private const int c_AssemblyCrc32Position = 0x0C;

        /// <summary>
        /// Position of the header CRC32 in the PE file.
        /// </summary>
        private const int c_HeaderCrc32Position = 0x08;

        /// <summary>
        /// Assembly tables context - contains all tables used for building target assembly.
        /// </summary>
        private readonly nanoTablesContext _context;

        /// <summary>
        /// Flag for core libraries.
        /// </summary>
        private readonly bool _isCoreLibrary = false;

        /// <summary>
        /// Offset for current table address writing.
        /// </summary>
        private long _tablesOffset;

        /// <summary>
        /// Offset for current table padding writing.
        /// </summary>
        private long _paddingsOffset;

        /// <summary>
        /// Size of the PE file header
        /// </summary>
        private long _headerSize;

        /// <summary>
        /// Creates new instance of <see cref="nanoAssemblyDefinition"/> object.
        /// </summary>
        /// <param name="context">
        /// Assembly tables context - contains all tables used for building target assembly.
        /// </param>
        public nanoAssemblyDefinition(
            nanoTablesContext context,
            bool isCoreLibrary)
        {
            _context = context;
            _isCoreLibrary = isCoreLibrary;
        }

        /// <summary>
        /// Writes header information into output stream (w/o CRC and table offsets/paddings).
        /// </summary>
        /// <param name="writer">Binary writer with correct endianness.</param>
        /// <param name="isPreAllocationCall">If true no assembly name will be written.</param>
        public void Write(
            nanoBinaryWriter writer,
            bool isPreAllocationCall)
        {
            // this follows the struct CLR_RECORD_ASSEMBLY

            // marker
            writer.WriteString(c_NFAssemblyMarker_v2);

            // need to set position because marker could be shorter
            writer.BaseStream.Seek(c_HeaderCrc32Position, SeekOrigin.Begin);

            // header CRC32
            writer.WriteUInt32(0);

            // assembly CRC32
            writer.WriteUInt32(0);

            // flags
            writer.WriteUInt32(0);

            // nativeMethodsChecksum
            writer.WriteUInt32(_context.NativeMethodsCrc.CurrentCrc);

            // version
            writer.WriteVersion(_context.AssemblyDefinition.Name.Version);

            // assemblyName
            writer.WriteUInt16(isPreAllocationCall
                ? (ushort) 0x0000
                : _context.StringTable.GetOrCreateStringId(_context.AssemblyDefinition.Name.Name));

            // string table version
            writer.WriteUInt16(1);

            //For every table, a number of bytes that were padded to the end of the table
            //to align to unsigned long.  Each table starts at a unsigned long boundary, and ends 
            //at a unsigned long boundary.  Some of these tables will, by construction, have
            //no padding, and all will have values in the range [0-3].  This isn't the most
            //compact form to hold this information, but it only costs 16 bytes/assembly.
            //Trying to only align some of the tables is just much more hassle than it's worth.
            //And, of course, this field also has to be unsigned long-aligned.

            // startOfTables
            // paddingOfTables

            if (isPreAllocationCall)
            {
                _tablesOffset = writer.BaseStream.Position;
                for (var i = 0; i < nanoAssemblyBuilder.TablesCount; ++i)
                {
                    writer.WriteUInt32(0);
                }

                _paddingsOffset = writer.BaseStream.Position;
                for (var i = 0; i < nanoAssemblyBuilder.TablesCount; ++i)
                {
                    writer.WriteByte(0);
                }

                // store the header size which is required to compute the CRC32 ahead
                _headerSize = (writer.BaseStream.Position + 3) & 0xFFFFFFFC;

                // check if we need to write any padding bytes
                writer.WriteBytes(new byte[_headerSize - writer.BaseStream.Position]);
            }
            else
            {
                // order matters!
                // need to compute Assembly CRC32 before header CRC32

                // set writer position at Assembly CRC32 position
                writer.BaseStream.Seek(c_AssemblyCrc32Position, SeekOrigin.Begin);

                var assemblyCrc32 = ComputeCrc32(
                    writer.BaseStream,
                    _headerSize, 
                    writer.BaseStream.Length - _headerSize);
                writer.WriteUInt32(assemblyCrc32);

                // set writer position at Header CRC32 position
                writer.BaseStream.Seek(c_HeaderCrc32Position, SeekOrigin.Begin);

                var headerCrc32 = ComputeCrc32(
                    writer.BaseStream, 
                    0,
                    _headerSize);
                writer.WriteUInt32(headerCrc32);
            }
        }

        /// <summary>
        /// Updates tables offset value and padding value for current table and
        /// advance writing position for next method call (filling tables info).
        /// </summary>
        /// <param name="writer">Binary writer.</param>
        /// <param name="tableBegin">Table beginning address (offset).</param>
        /// <param name="padding">Table padding value.</param>
        public void UpdateTableOffset(
            nanoBinaryWriter writer,
            long tableBegin,
            long padding)
        {
            writer.BaseStream.Seek(_tablesOffset, SeekOrigin.Begin);
            writer.WriteUInt32((uint)tableBegin);
            _tablesOffset += sizeof(int);

            writer.BaseStream.Seek(_paddingsOffset, SeekOrigin.Begin);
            writer.WriteByte((byte)padding);
            _paddingsOffset += sizeof(byte);

            writer.BaseStream.Seek(0, SeekOrigin.End);
        }

        private static uint ComputeCrc32(
            Stream outputStream,
            long startOffset,
            long size)
        {
            var currentPosition = outputStream.Position;
            outputStream.Seek(startOffset, SeekOrigin.Begin);

            var buffer = new byte[size];
            outputStream.Read(buffer, 0, buffer.Length);

            outputStream.Seek(currentPosition, SeekOrigin.Begin);

            return Crc32.Compute(buffer);
        }
    }
}
