//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using nanoFramework.Tools.MetadataProcessor.Core;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;
using System;
using System.IO;
using System.Linq;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Utility
{
    [TestClass]
    public class DumperTests
    {
        private ushort refId;

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

            // TestNFApp.DummyCustomAttribute1
            string typeName = "TestNFApp.DummyCustomAttribute1";
            TypeDefinition type1 = nanoTablesContext.TypeDefinitionTable.Items.FirstOrDefault(t => t.FullName == typeName);
            nanoTablesContext.TypeDefinitionTable.TryGetTypeReferenceId(type1, out ushort typeRefId);
            nanoTablesContext.TypeReferencesTable.TryGetTypeReferenceId(type1.BaseType, out ushort baseTypeReferenceId);
            uint typeFlags = (uint)nanoTypeDefinitionTable.GetFlags(type1, nanoTablesContext.MethodDefinitionTable);

            Assert.IsTrue(dumpFileContent.Contains($"TypeDef {nanoDumperGenerator.TypeDefRefIdToString(type1, typeRefId)}\r\n-------------------------------------------------------\r\n    '{typeName}'\r\n    Flags: {typeFlags:X8}\r\n    Extends: {nanoDumperGenerator.TypeDefExtendsTypeToString(type1.BaseType, baseTypeReferenceId)}\r\n    Enclosed: (none)"));

            // TestNFApp.DummyCustomAttribute2
            typeName = "TestNFApp.DummyCustomAttribute2";
            type1 = nanoTablesContext.TypeDefinitionTable.Items.FirstOrDefault(t => t.FullName == typeName);
            nanoTablesContext.TypeDefinitionTable.TryGetTypeReferenceId(type1, out typeRefId);
            nanoTablesContext.TypeReferencesTable.TryGetTypeReferenceId(type1.BaseType, out baseTypeReferenceId);
            typeFlags = (uint)nanoTypeDefinitionTable.GetFlags(type1, nanoTablesContext.MethodDefinitionTable);

            Assert.IsTrue(dumpFileContent.Contains($"TypeDef {nanoDumperGenerator.TypeDefRefIdToString(type1, typeRefId)}\r\n-------------------------------------------------------\r\n    '{typeName}'\r\n    Flags: {typeFlags:X8}\r\n    Extends: {nanoDumperGenerator.TypeDefExtendsTypeToString(type1.BaseType, baseTypeReferenceId)}\r\n    Enclosed: (none)"));

            // TestNFApp.IOneClassOverAll
            typeName = "TestNFApp.IOneClassOverAll";
            type1 = nanoTablesContext.TypeDefinitionTable.Items.FirstOrDefault(t => t.FullName == typeName);
            nanoTablesContext.TypeDefinitionTable.TryGetTypeReferenceId(type1, out typeRefId);
            typeFlags = (uint)nanoTypeDefinitionTable.GetFlags(type1, nanoTablesContext.MethodDefinitionTable);

            Assert.IsTrue(dumpFileContent.Contains($"TypeDef {nanoDumperGenerator.TypeDefRefIdToString(type1, typeRefId)}\r\n-------------------------------------------------------\r\n    '{typeName}'\r\n    Flags: {typeFlags:X8}\r\n    Extends: (none)\r\n    Enclosed: (none)"));

            foreach (var m in type1.Methods)
            {
                _ = nanoTablesContext.MethodDefinitionTable.TryGetMethodReferenceId(m, out ushort methodReferenceId);

                Assert.IsTrue(dumpFileContent.Contains($"    MethodDef {nanoDumperGenerator.MethodRefIdToString(m, methodReferenceId)}\r\n    -------------------------------------------------------\r\n        '{m.FullName()}'\r\n        Flags: {nanoMethodDefinitionTable.GetFlags(m):X8}\r\n        Impl: 00000000\r\n        RVA: 0000FFFF\r\n        [{nanoDumperGenerator.PrintSignatureForMethod(m)}]"));
            }

            // TestNFApp.ComplexAttribute
            typeName = "TestNFApp.ComplexAttribute";
            type1 = nanoTablesContext.TypeDefinitionTable.Items.FirstOrDefault(t => t.FullName == typeName);
            nanoTablesContext.TypeDefinitionTable.TryGetTypeReferenceId(type1, out typeRefId);
            nanoTablesContext.TypeReferencesTable.TryGetTypeReferenceId(type1.BaseType, out baseTypeReferenceId);
            typeFlags = (uint)nanoTypeDefinitionTable.GetFlags(type1, nanoTablesContext.MethodDefinitionTable);

            Assert.IsTrue(dumpFileContent.Contains($"TypeDef {nanoDumperGenerator.TypeDefRefIdToString(type1, typeRefId)}\r\n-------------------------------------------------------\r\n    '{typeName}'\r\n    Flags: {typeFlags:X8}\r\n    Extends: {nanoDumperGenerator.TypeDefExtendsTypeToString(type1.BaseType, baseTypeReferenceId)}\r\n    Enclosed: (none)"));
            Assert.IsTrue(dumpFileContent.Contains("    FieldDef [01000000] /*04000004*/\r\n    -------------------------------------------------------\r\n    Attr: 00000021\r\n    Flags: 00000021\r\n    '_max'\r\n    [U4]\r\n"));
            Assert.IsTrue(dumpFileContent.Contains("    FieldDef [01000000] /*04000005*/\r\n    -------------------------------------------------------\r\n    Attr: 00000021\r\n    Flags: 00000021\r\n    '_s'\r\n    [STRING]\r\n"));
            Assert.IsTrue(dumpFileContent.Contains("    FieldDef [01000000] /*04000006*/\r\n    -------------------------------------------------------\r\n    Attr: 00000021\r\n    Flags: 00000021\r\n    '_b'\r\n    [BOOLEAN]\r\n"));

            foreach (var m in type1.Methods)
            {
                _ = nanoTablesContext.MethodDefinitionTable.TryGetMethodReferenceId(m, out ushort methodReferenceId);

                Assert.IsTrue(dumpFileContent.Contains($"    MethodDef {nanoDumperGenerator.MethodRefIdToString(m, methodReferenceId)}\r\n    -------------------------------------------------------\r\n        '{m.FullName()}'\r\n        Flags: {nanoMethodDefinitionTable.GetFlags(m):X8}\r\n        Impl: 00000000\r\n        RVA: 0000FFFF\r\n        [{nanoDumperGenerator.PrintSignatureForMethod(m)}]"));
            }

            // TestNFApp.Program
            typeName = "TestNFApp.Program";
            type1 = nanoTablesContext.TypeDefinitionTable.Items.FirstOrDefault(t => t.FullName == typeName);
            nanoTablesContext.TypeDefinitionTable.TryGetTypeReferenceId(type1, out typeRefId);
            nanoTablesContext.TypeReferencesTable.TryGetTypeReferenceId(type1.BaseType, out baseTypeReferenceId);
            typeFlags = (uint)nanoTypeDefinitionTable.GetFlags(type1, nanoTablesContext.MethodDefinitionTable);

            Assert.IsTrue(dumpFileContent.Contains($"TypeDef {nanoDumperGenerator.TypeDefRefIdToString(type1, typeRefId)}\r\n-------------------------------------------------------\r\n    '{typeName}'\r\n    Flags: {typeFlags:X8}\r\n    Extends: {nanoDumperGenerator.TypeDefExtendsTypeToString(type1.BaseType, baseTypeReferenceId)}\r\n    Enclosed: (none)"));

            // OneClassOverAll/SubClass
            typeName = "TestNFApp.OneClassOverAll/SubClass";
            type1 = nanoTablesContext.TypeDefinitionTable.Items.FirstOrDefault(t => t.FullName == typeName);
            nanoTablesContext.TypeDefinitionTable.TryGetTypeReferenceId(type1, out typeRefId);
            nanoTablesContext.TypeReferencesTable.TryGetTypeReferenceId(type1.BaseType, out baseTypeReferenceId);
            typeFlags = (uint)nanoTypeDefinitionTable.GetFlags(type1, nanoTablesContext.MethodDefinitionTable);

            Assert.IsTrue(dumpFileContent.Contains($"TypeDef {nanoDumperGenerator.TypeDefRefIdToString(type1, typeRefId)}\r\n-------------------------------------------------------\r\n    '{type1.Name}'\r\n    Flags: {typeFlags:X8}\r\n    Extends: {nanoDumperGenerator.TypeDefExtendsTypeToString(type1.BaseType, baseTypeReferenceId)}\r\n    Enclosed: TestNFApp.OneClassOverAll[04000000] /*02000019*/"));

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
            var lastStringEntry = nanoTablesContext.StringTable.GetItems().OrderBy(i => i.Value).Last();
            Assert.IsTrue(dumpFileContent.Contains($"{new nanoMetadataToken(NanoCLRTable.TBL_Strings, nanoTablesContext.StringTable.GetOrCreateStringId(lastStringEntry.Key, true))} : ({lastStringEntry.Key.Length:x2}) \"{lastStringEntry.Key}\""));
        }
    }
}
