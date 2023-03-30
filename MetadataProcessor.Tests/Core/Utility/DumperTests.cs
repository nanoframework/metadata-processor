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
            Assert.IsTrue(dumpFileContent.Contains("AssemblyRef [00000000] /*23000001*/\r\n-------------------------------------------------------\r\n'mscorlib'"));
            Assert.IsTrue(dumpFileContent.Contains("AssemblyRef [00000001] /*23000002*/\r\n-------------------------------------------------------\r\n'TestNFClassLibrary'"));

            // TypeRefs
            Assert.IsTrue(dumpFileContent.Contains("TypeRef [01000000] /*01000001*/\r\n-------------------------------------------------------\r\nScope: [0000] /*01000001*/\r\n    'System.Diagnostics.DebuggableAttribute'"));
            Assert.IsTrue(dumpFileContent.Contains("TypeRef [01000016] /*01000017*/\r\n-------------------------------------------------------\r\nScope: [0001] /*01000017*/\r\n    'TestNFClassLibrary.ClassOnAnotherAssembly'"));

            Assert.IsTrue(dumpFileContent.Contains("TypeDef [0400000E] /*02000004*/\r\n-------------------------------------------------------\r\n    'TestNFApp.DummyCustomAttribute1'\r\n    Flags: 00001001\r\n    Extends: System.Attribute[0100000C] /*0100000D*/\r\n    Enclosed: (none)"));

            Assert.IsTrue(dumpFileContent.Contains("TypeDef [0400000F] /*02000005*/\r\n-------------------------------------------------------\r\n    'TestNFApp.DummyCustomAttribute2'\r\n    Flags: 00001001\r\n    Extends: System.Attribute[0100000C] /*0100000D*/\r\n    Enclosed: (none)"));

            Assert.IsTrue(dumpFileContent.Contains("TypeDef [04000013] /*02000007*/\r\n-------------------------------------------------------\r\n    'TestNFApp.IOneClassOverAll'\r\n    Flags: 00000061\r\n    Extends: (none)\r\n    Enclosed: (none)"));
            Assert.IsTrue(dumpFileContent.Contains("    MethodDef [0300003A] /*0600003B*/\r\n    -------------------------------------------------------\r\n        'get_DummyProperty'\r\n        Flags: 000005E6\r\n        Impl: 00000000\r\n        RVA: 0000FFFF\r\n        [I4(   )]\r\n"));
            Assert.IsTrue(dumpFileContent.Contains("    MethodDef [0300003B] /*0600003C*/\r\n    -------------------------------------------------------\r\n        'set_DummyProperty'\r\n        Flags: 000005E6\r\n        Impl: 00000000\r\n        RVA: 0000FFFF\r\n        [VOID( I4 )]"));
            Assert.IsTrue(dumpFileContent.Contains("    MethodDef [0300003C] /*0600003D*/\r\n    -------------------------------------------------------\r\n        'DummyMethod'\r\n        Flags: 800001E6\r\n        Impl: 00000000\r\n        RVA: 0000FFFF\r\n        [VOID(   )]"));

            Assert.IsTrue(dumpFileContent.Contains("TypeDef [04000013] /*02000007*/\r\n-------------------------------------------------------\r\n    'TestNFApp.IOneClassOverAll'\r\n    Flags: 00000061\r\n    Extends: (none)\r\n    Enclosed: (none)"));
            Assert.IsTrue(dumpFileContent.Contains("    FieldDef [01000000] /*0400000D*/\r\n    -------------------------------------------------------\r\n    Attr: 00000001\r\n    Flags: 00000001\r\n    'dummyField'\r\n    [STRING]\r\n"));
            Assert.IsTrue(dumpFileContent.Contains("    FieldDef [01000000] /*0400000C*/\r\n    -------------------------------------------------------\r\n    Attr: 00000001\r\n    Flags: 00000001\r\n    '<DummyProperty>k__BackingField'\r\n    [I4]\r\n"));
            Assert.IsTrue(dumpFileContent.Contains("    MethodDef [0300003F] /*06000043*/\r\n    -------------------------------------------------------\r\n        'DummyExternMethod'\r\n        Flags: 00000086\r\n        Impl: 00000000\r\n        RVA: 0000FFFF\r\n        [VOID(   )]\r\n"));

            Assert.IsTrue(dumpFileContent.Contains("TypeDef [04000018] /*02000019*/\r\n-------------------------------------------------------\r\n    'TestNFApp.Program'\r\n    Flags: 00001001\r\n    Extends: System.Object[01000011] /*01000012*/\r\n    Enclosed: (none)\r\n"));

            Assert.IsTrue(dumpFileContent.Contains("TypeDef [04000017] /*0200001A*/\r\n-------------------------------------------------------\r\n    'SubClass'\r\n    Flags: 00001002\r\n    Extends: System.Object[01000011] /*01000012*/\r\n    Enclosed: TestNFApp.OneClassOverAll[04000000] /*02000018*/\r\n"));

            // String heap
            foreach (var stringKey in nanoTablesContext.StringTable.GetItems().Keys)
            {
                // skip initial empty string
                if (string.IsNullOrEmpty(stringKey))
                {
                    continue;
                }

                var expectedString = $"{nanoTablesContext.StringTable.GetItems()[stringKey]:X8}: {stringKey}";

                Assert.IsTrue(dumpFileContent.Contains(expectedString));
            }

            // UserStrings
            Assert.IsTrue(dumpFileContent.Contains("0D0002D8 : (14) \"++ Generics Tests ++\""));
            Assert.IsTrue(dumpFileContent.Contains("0D00036F : (11) \"Exiting TestNFApp\""));
            Assert.IsTrue(dumpFileContent.Contains("0D00061A : (1c) \"+++ReflectionTests completed\""));
        }
    }
}
