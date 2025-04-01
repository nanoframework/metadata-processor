// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            Assert.IsTrue(dumpFileContent.Contains("AssemblyRef [00000000] /*23000001*/\r\n-------------------------------------------------------\r\n'mscorlib'"));
            Assert.IsTrue(dumpFileContent.Contains("AssemblyRef [00000001] /*23000002*/\r\n-------------------------------------------------------\r\n'TestNFClassLibrary'"));

            // TypeRefs
            Assert.IsTrue(dumpFileContent.Contains("TypeRef [01000000] /*01000001*/\r\n-------------------------------------------------------\r\nScope: [0000] /*01000001*/\r\n    'System.Diagnostics.DebuggableAttribute'"));
            Assert.IsTrue(dumpFileContent.Contains("TypeRef [0100001E] /*0100001F*/\r\n-------------------------------------------------------\r\nScope: [0001] /*0100001F*/\r\n    'TestNFClassLibrary.ClassOnAnotherAssembly'"));

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

            foreach (MethodDefinition m in type1.Methods)
            {
                _ = nanoTablesContext.MethodDefinitionTable.TryGetMethodReferenceId(m, out ushort methodReferenceId);

                Assert.IsTrue(dumpFileContent.Contains($"    MethodDef {nanoDumperGenerator.MethodDefIdToString(m, methodReferenceId)}\r\n    -------------------------------------------------------\r\n        '{m.FullName()}'\r\n        Flags: {nanoMethodDefinitionTable.GetFlags(m):X8}\r\n        Impl: 00000000\r\n        RVA: 0000FFFF\r\n        [{nanoDumperGenerator.PrintSignatureForMethod(m)}]"));
            }

            // TestNFApp.ComplexAttribute
            typeName = "TestNFApp.ComplexAttribute";
            type1 = nanoTablesContext.TypeDefinitionTable.Items.FirstOrDefault(t => t.FullName == typeName);
            nanoTablesContext.TypeDefinitionTable.TryGetTypeReferenceId(type1, out typeRefId);
            nanoTablesContext.TypeReferencesTable.TryGetTypeReferenceId(type1.BaseType, out baseTypeReferenceId);

            FieldDefinition maxField = nanoTablesContext.FieldsTable.Items.FirstOrDefault(f => f.Name == "_max");
            string maxFieldRealToken = maxField.MetadataToken.ToInt32().ToString("X8");
            nanoTablesContext.FieldsTable.TryGetFieldReferenceId(maxField, false, out ushort maxFieldReferenceId);

            FieldDefinition sField = nanoTablesContext.FieldsTable.Items.FirstOrDefault(f => f.Name == "_s");
            string sFieldRealToken = sField.MetadataToken.ToInt32().ToString("X8");
            nanoTablesContext.FieldsTable.TryGetFieldReferenceId(sField, false, out ushort sFieldReferenceId);

            FieldDefinition bField = nanoTablesContext.FieldsTable.Items.FirstOrDefault(f => f.Name == "_b");
            string bFieldRealToken = bField.MetadataToken.ToInt32().ToString("X8");
            nanoTablesContext.FieldsTable.TryGetFieldReferenceId(bField, false, out ushort bFieldReferenceId);


            typeFlags = (uint)nanoTypeDefinitionTable.GetFlags(type1, nanoTablesContext.MethodDefinitionTable);

            Assert.IsTrue(dumpFileContent.Contains($"TypeDef {nanoDumperGenerator.TypeDefRefIdToString(type1, typeRefId)}\r\n-------------------------------------------------------\r\n    '{typeName}'\r\n    Flags: {typeFlags:X8}\r\n    Extends: {nanoDumperGenerator.TypeDefExtendsTypeToString(type1.BaseType, baseTypeReferenceId)}\r\n    Enclosed: (none)"));
            Assert.IsTrue(dumpFileContent.Contains($"    FieldDef [{new nanoMetadataToken(maxField.MetadataToken, maxFieldReferenceId)}] /*{maxFieldRealToken}*/\r\n    -------------------------------------------------------\r\n    Attr: 00000021\r\n    Flags: 00000021\r\n    '_max'\r\n    [U4]\r\n"));
            Assert.IsTrue(dumpFileContent.Contains($"    FieldDef [{new nanoMetadataToken(sField.MetadataToken, sFieldReferenceId)}] /*{sFieldRealToken}*/\r\n    -------------------------------------------------------\r\n    Attr: 00000021\r\n    Flags: 00000021\r\n    '_s'\r\n    [STRING]\r\n"));
            Assert.IsTrue(dumpFileContent.Contains($"    FieldDef [{new nanoMetadataToken(bField.MetadataToken, bFieldReferenceId)}] /*{bFieldRealToken}*/\r\n    -------------------------------------------------------\r\n    Attr: 00000021\r\n    Flags: 00000021\r\n    '_b'\r\n    [BOOLEAN]\r\n"));

            foreach (MethodDefinition m in type1.Methods)
            {
                _ = nanoTablesContext.MethodDefinitionTable.TryGetMethodReferenceId(m, out ushort methodReferenceId);

                Assert.IsTrue(dumpFileContent.Contains($"    MethodDef {nanoDumperGenerator.MethodDefIdToString(m, methodReferenceId)}\r\n    -------------------------------------------------------\r\n        '{m.FullName()}'\r\n        Flags: {nanoMethodDefinitionTable.GetFlags(m):X8}\r\n        Impl: 00000000\r\n        RVA: 0000FFFF\r\n        [{nanoDumperGenerator.PrintSignatureForMethod(m)}]"));
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

            Assert.IsTrue(dumpFileContent.Contains($"TypeDef {nanoDumperGenerator.TypeDefRefIdToString(type1, typeRefId)}\r\n-------------------------------------------------------\r\n    '{type1.Name}'\r\n    Flags: {typeFlags:X8}\r\n    Extends: {nanoDumperGenerator.TypeDefExtendsTypeToString(type1.BaseType, baseTypeReferenceId)}\r\n    Enclosed: TestNFApp.OneClassOverAll[04000000] /*0200001B*/"));

            // String heap
            foreach (string stringKey in nanoTablesContext.StringTable.GetItems().Keys)
            {
                // skip initial empty string
                if (string.IsNullOrEmpty(stringKey))
                {
                    continue;
                }

                string expectedString = $"{nanoTablesContext.StringTable.GetItems()[stringKey]:X8}: {stringKey}";

                Assert.IsTrue(dumpFileContent.Contains(expectedString));
            }

            // UserStrings
            System.Collections.Generic.KeyValuePair<string, ushort> lastStringEntry = nanoTablesContext.StringTable.GetItems().OrderBy(i => i.Value).Last();
            Assert.IsTrue(dumpFileContent.Contains($"{new nanoMetadataToken(NanoCLRTable.TBL_Strings, nanoTablesContext.StringTable.GetOrCreateStringId(lastStringEntry.Key, true))} : ({lastStringEntry.Key.Length:x2}) \"{lastStringEntry.Key}\""));
        }
    }
}
