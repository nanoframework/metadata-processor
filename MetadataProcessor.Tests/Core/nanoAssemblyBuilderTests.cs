// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core
{
    [TestClass]
    public class nanoAssemblyBuilderTests
    {
        [TestMethod]
        public void AssemblyTablesCountTest()
        {
            var nanoTablesContext = TestObjectHelper.GetTestNFAppNanoTablesContext();

            Assert.IsTrue(
                nanoAssemblyBuilder.GetTables(nanoTablesContext).Count() == nanoAssemblyBuilder.TablesCount,
                "Tables count from context doesn't match the nanoAssemblyBuilder.TablesCount property.");

            Assert.IsTrue(
                nanoAssemblyBuilder.GetTables(nanoTablesContext).Count() == Enum.GetNames(typeof(NanoClrTable)).Length,
                "Tables count from context doesn't match number of items in CLR Tables enum.");
        }

        [TestMethod]
        public void WriteHandlesPartialCrcReads()
        {
            using (var normalStream = new MemoryStream())
            using (var partialStream = new PartialReadStream(3))
            {
                nanoAssemblyBuilder normalBuilder = WriteAssembly(normalStream);
                nanoAssemblyBuilder partialBuilder = WriteAssembly(partialStream);

                CollectionAssert.AreEqual(normalStream.ToArray(), partialStream.ToArray());
                Assert.AreEqual(normalBuilder.LastAssemblyCrc32, partialBuilder.LastAssemblyCrc32);
                Assert.AreEqual(normalBuilder.LastHeaderCrc32, partialBuilder.LastHeaderCrc32);
                Assert.AreEqual(normalStream.Position, partialStream.Position);
            }
        }

        [TestMethod]
        public void WriteFailsWhenCrcReadEndsEarly()
        {
            using (var stream = new PartialReadStream(2, 2))
            {
                Assert.ThrowsExactly<EndOfStreamException>(() => WriteAssembly(stream));
            }
        }

        private static nanoAssemblyBuilder WriteAssembly(Stream stream)
        {
            var builder = new nanoAssemblyBuilder(TestObjectHelper.GetTestNFAppAssemblyDefinitionWithLoadHints(), false);
            using (var binaryWriter = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                builder.Write(nanoBinaryWriter.CreateLittleEndianBinaryWriter(binaryWriter));
            }

            return builder;
        }

        private sealed class PartialReadStream : MemoryStream
        {
            private readonly int _maxReadSize;
            private int _remainingBytes;

            public PartialReadStream(int maxReadSize, int remainingBytes = int.MaxValue)
            {
                _maxReadSize = maxReadSize;
                _remainingBytes = remainingBytes;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int bytesRead = base.Read(buffer, offset, Math.Min(count, Math.Min(_maxReadSize, _remainingBytes)));
                _remainingBytes -= bytesRead;
                return bytesRead;
            }
        }
    }
}
