//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoFramework.Tools.MetadataProcessor.Core;
using System;
using System.IO;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Utility
{
    [TestClass]
    public class DumperTests
    {
        [TestMethod]
        public void DumpAssemblyTest()
        {
            var nanoTablesContext = TestObjectHelper.GetTestNFAppNanoTablesContext();

            var fileNameBase = $"{DateTime.UtcNow.ToShortDateString()}_{DateTime.UtcNow.ToLongTimeString()}";
            fileNameBase = fileNameBase.Replace(':', '_').Replace('/', '_');

            var dumpFileName = Path.Combine(TestObjectHelper.TestExecutionLocation, $"{fileNameBase}_dump.txt");

            nanoDumperGenerator dumper = new nanoDumperGenerator(
                nanoTablesContext,
                dumpFileName);
            dumper.DumpAll();

            // read back file
            var dumpFileContent = File.ReadAllText(dumpFileName);

            // search for bits

            // AssemblyRefs
            Assert.IsTrue(dumpFileContent.Contains("AssemblyRefProps [23000001]: Flags: 00000000 'mscorlib'"));
            Assert.IsTrue(dumpFileContent.Contains("AssemblyRefProps [23000002]: Flags: 00000000 'TestNFClassLibrary'"));

            // TypeRefs
            Assert.IsTrue(dumpFileContent.Contains("TypeRefProps [01000001]: Scope: 23000001 'System.Diagnostics.DebuggableAttribute'"));
            Assert.IsTrue(dumpFileContent.Contains(": Scope: 23000002 'TestNFClassLibrary.ClassOnAnotherAssembly'"));

            Assert.IsTrue(dumpFileContent.Contains(": Flags: 00001001 Extends: 0100000d Enclosed: 02000000 'TestNFApp.DummyCustomAttribute1'"));

            Assert.IsTrue(dumpFileContent.Contains(": Flags: 00001001 Extends: 0100000d Enclosed: 02000000 'TestNFApp.DummyCustomAttribute2'"));

            Assert.IsTrue(dumpFileContent.Contains(": Flags: 00001061 Extends: 01000000 Enclosed: 02000000 'TestNFApp.IOneClassOverAll'"));
            Assert.IsTrue(dumpFileContent.Contains(": Flags: 000007c6 Impl: 00000000 RVA: 00000000 'get_DummyProperty' [I4(   )]"));
            Assert.IsTrue(dumpFileContent.Contains(": Flags: 000007c6 Impl: 00000000 RVA: 00000000 'set_DummyProperty' [VOID( I4 )]"));
            Assert.IsTrue(dumpFileContent.Contains(": Flags: 000003c6 Impl: 00000000 RVA: 00000000 'DummyMethod' [VOID(   )]"));

            Assert.IsTrue(dumpFileContent.Contains("Enclosed: 02000000 'TestNFApp.OneClassOverAll'"));
            Assert.IsTrue(dumpFileContent.Contains(": Attr: 00000001 Flags: 00000001 'dummyField' [STRING]"));
            Assert.IsTrue(dumpFileContent.Contains(": Attr: 00000001 Flags: 00000001 '<DummyProperty>k__BackingField' [I4]"));
            Assert.IsTrue(dumpFileContent.Contains(": Flags: 00000086 Impl: 00000000 RVA: 00000000 'DummyExternMethod' [VOID(   )]"));

            Assert.IsTrue(dumpFileContent.Contains("'TestNFApp.Program'"));

            Assert.IsTrue(dumpFileContent.Contains("'SubClass'"));

            // UserStrings
            Assert.IsTrue(dumpFileContent.Contains(": 'TestNFClassLibrary'"));
            Assert.IsTrue(dumpFileContent.Contains(": 'get_DummyProperty'"));
            Assert.IsTrue(dumpFileContent.Contains(": 'blabla'"));
        }
    }
}
