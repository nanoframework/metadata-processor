// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Tables
{
    [TestClass]
    public class nanoAssemblyReferenceTableTests
    {
        [TestMethod]
        public void ConstructorTest()
        {
            var assemblyDefinition = TestObjectHelper.GetTestNFAppAssemblyDefinition();
            var mainModule = assemblyDefinition.MainModule;
            var context = TestObjectHelper.GetTestNFAppNanoTablesContext();

            // test
            var iut = new nanoAssemblyReferenceTable(
                mainModule.AssemblyReferences,
                context);

            // no op
            Assert.IsNotNull(iut);
        }

        [TestMethod]
        public void ForEachItemsTest()
        {
            var assemblyDefinition = TestObjectHelper.GetTestNFAppAssemblyDefinition();
            var mainModule = assemblyDefinition.MainModule;
            var context = TestObjectHelper.GetTestNFAppNanoTablesContext();

            var items = assemblyDefinition.MainModule.AssemblyReferences.ToList<object>();

            // test
            var iut = new nanoAssemblyReferenceTable(
                mainModule.AssemblyReferences,
                context);

            // test
            var forEachCalledOnItems = new List<object>();
            iut.ForEachItems((idx, item) =>
            {
                forEachCalledOnItems.Add(item);
                Assert.AreEqual(items.IndexOf(item), (int)idx);
            });

            CollectionAssert.AreEqual(items.ToArray(), forEachCalledOnItems.ToArray());
        }

        [TestMethod]
        public void WriteTest()
        {
            var assemblyDefinition = TestObjectHelper.GetTestNFAppAssemblyDefinition();
            var mainModule = assemblyDefinition.MainModule;
            var context = TestObjectHelper.GetTestNFAppNanoTablesContext();

            // test
            var iut = new nanoAssemblyReferenceTable(
                mainModule.AssemblyReferences,
                context);

            // need to force this property
            context.MinimizeComplete = true;

            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms, Encoding.Default, true))
                {
                    var writer = nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw);

                    // test
                    iut.Write(writer);

                    bw.Flush();

                }

                var streamOutput = new MemoryStream();
                var writerTestOutput = new BinaryWriter(streamOutput, Encoding.Default, true);

                foreach (var a in iut.Items)
                {
                    writerTestOutput.Write(context.StringTable.GetOrCreateStringId(a.Name));

                    // version
                    writerTestOutput.Write((ushort)a.Version.Major);
                    writerTestOutput.Write((ushort)a.Version.Minor);
                    writerTestOutput.Write((ushort)a.Version.Build);
                    writerTestOutput.Write((ushort)a.Version.Revision);

                }

                var expectedByteWritten = streamOutput.ToArray();

                var bytesWritten = ms.ToArray();
                CollectionAssert.AreEqual(expectedByteWritten, bytesWritten, $"Wrote: {string.Join(", ", bytesWritten.Select(i => i.ToString("X")))}, Expected: {string.Join(", ", expectedByteWritten.Select(i => i.ToString("X")))} ");
            }
        }
    }
}
