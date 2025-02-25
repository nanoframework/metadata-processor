// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoFramework.Tools.MetadataProcessor.Core;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Utility
{
    [TestClass]
    public class DumperTests
    {
        [TestMethod]
        public void DumpAssemblyTest()
        {
            nanoTablesContext nanoTablesContext = TestObjectHelper.GetTestNFAppNanoTablesContext();

            string fileNameBase = $"{DateTime.UtcNow.ToShortDateString()}_{DateTime.UtcNow.ToLongTimeString()}";
            fileNameBase = fileNameBase.Replace(':', '_').Replace('/', '_');

            string dumpFileName = Path.Combine(TestObjectHelper.TestExecutionLocation, $"{fileNameBase}_dump.txt");

            nanoDumperGenerator dumper = new nanoDumperGenerator(
                nanoTablesContext,
                dumpFileName);
            dumper.DumpAll();

            // read back file
            string dumpFileContent = File.ReadAllText(dumpFileName);

            // output for debugging, if needed
            Console.WriteLine($">>>>>>>\r\n{dumpFileContent}\r\n>>>>>>>");

            // search for several bits

            // AssemblyRefs
            Assert.IsTrue(dumpFileContent.Contains("AssemblyRefProps [23000001]: Flags: 00000000 'mscorlib'"), "Wrong entry for mscorlib in assembly ref");
            Assert.IsTrue(dumpFileContent.Contains("AssemblyRefProps [23000002]: Flags: 00000000 'TestNFClassLibrary'"), "Wrong entry for TestNFClassLibrary in assembly ref");

            // TypeRefs
            Assert.IsTrue(dumpFileContent.Contains("TypeRefProps [01000001]: Scope: 23000001 'System.Diagnostics.DebuggableAttribute'"), "Wrong entry for System.Diagnostics.DebuggableAttribute in type ref");
            Assert.IsTrue(dumpFileContent.Contains(": Scope: 23000002 'TestNFClassLibrary.ClassOnAnotherAssembly'"), "Wrong entry for TestNFClassLibrary.ClassOnAnotherAssembly in type ref");

            Assert.IsTrue(dumpFileContent.Contains(": Flags: 00001001 Extends: 0100000f Enclosed: 02000000 'TestNFApp.DummyCustomAttribute1'"), "Wrong entry for TestNFApp.DummyCustomAttribute1 in type ref");

            Assert.IsTrue(dumpFileContent.Contains(": Flags: 00001001 Extends: 0100000f Enclosed: 02000000 'TestNFApp.DummyCustomAttribute2'"), "Wrong entry for TestNFApp.DummyCustomAttribute2 in type ref");

            Assert.IsTrue(dumpFileContent.Contains(": Flags: 00001061 Extends: 01000000 Enclosed: 02000000 'TestNFApp.IOneClassOverAll'"), "Wrong entry for TestNFApp.IOneClassOverAll in type ref");
            Assert.IsTrue(dumpFileContent.Contains(": Flags: 000007c6 Impl: 00000000 RVA: 00000000 'get_DummyProperty' [I4( )]"), "Wrong entry for get_DummyProperty in type ref");
            Assert.IsTrue(dumpFileContent.Contains(": Flags: 000007c6 Impl: 00000000 RVA: 00000000 'set_DummyProperty' [VOID(I4)]"), "Wrong entry for set_DummyProperty in type ref");
            Assert.IsTrue(dumpFileContent.Contains(": Flags: 000003c6 Impl: 00000000 RVA: 00000000 'DummyMethod' [VOID( )]"), "Wrong entry for DummyMethod in type ref");

            Assert.IsTrue(dumpFileContent.Contains("Enclosed: 02000000 'TestNFApp.OneClassOverAll'"), "Wrong entry for TestNFApp.OneClassOverAll in type ref");
            Assert.IsTrue(dumpFileContent.Contains(": Attr: 00000001 Flags: 00000001 'dummyField' [STRING]"), "Wrong entry for dummyField in type ref");
            Assert.IsTrue(dumpFileContent.Contains(": Attr: 00000001 Flags: 00000001 '<DummyProperty>k__BackingField' [I4]"), "Wrong entry for <DummyProperty>k__BackingField in type ref");
            Assert.IsTrue(dumpFileContent.Contains(": Flags: 00000086 Impl: 00000000 RVA: 00000000 'DummyExternMethod' [VOID( )]"), "Wrong entry for DummyExternMethod in type ref");

            Assert.IsTrue(dumpFileContent.Contains("'TestNFApp.Program'"), "Wrong entry for TestNFApp.Program in type ref");

            Assert.IsTrue(dumpFileContent.Contains("'SubClass'"), "Wrong entry for SubClass in type ref");

            // skip this assert to locals listing as the debug build will differ as it not optimized
#if !DEBUG
            Assert.IsTrue(Regex.IsMatch(dumpFileContent, @"Locals \( \[0\] I4, \r\n\s+\[1\] CLASS System\.Exception \[\d{8}\], \r\n\s+\[2\] CLASS System\.ApplicationException \[\d{8}\] \)"), "Wrong listing of locals in UglyAdd method");
#endif
            Assert.IsTrue(dumpFileContent.Contains("callvirt    System.Void TestNFApp.TestingDelegates/SimpleDelegate::Invoke(System.String)"), "Wrong reference to callvirt in DelegateTests body");
            Assert.IsTrue(dumpFileContent.Contains("ldstr       \"          DataRowAttribute.Arg[{0}] has: {1}\""), "Wrong reference to ldstr in ReflectionTests body");

            // UserStrings
            Assert.IsTrue(dumpFileContent.Contains(": 'TestNFClassLibrary'"), "Wrong entry for TestNFClassLibrary in user string");
            Assert.IsTrue(dumpFileContent.Contains(": 'get_DummyProperty'"), "Wrong entry for get_DummyProperty in user string");
            Assert.IsTrue(dumpFileContent.Contains(": 'blabla'"), "Wrong entry for blabla in user string");
        }
    }
}
