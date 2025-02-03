//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomAttribute = Mono.Cecil.CustomAttribute;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Tables
{
    [TestClass]
    public class nanoAttributesTableTests
    {
        [TestMethod]
        public void ConstructorTest()
        {
            var typesAttributes = new Tuple<CustomAttribute, ushort>[0];
            var fieldsAttributes = new Tuple<CustomAttribute, ushort>[0];
            var methodsAttributes = new Tuple<CustomAttribute, ushort>[0];
            var context = TestObjectHelper.GetTestNFAppNanoTablesContext();

            // test
            var iut = new nanoAttributesTable(typesAttributes, fieldsAttributes, methodsAttributes, context);

            // no op
        }

        [TestMethod]
        public void RemoveUnusedItems_TypesAttributesTest()
        {
            var nanoTablesContext = TestObjectHelper.GetTestNFAppNanoTablesContext();
            var testClassTypeDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllTypeDefinition(nanoTablesContext.AssemblyDefinition);

            Assert.IsTrue(testClassTypeDefinition.CustomAttributes.Count > 1);
            var customAttribute0 = testClassTypeDefinition.CustomAttributes[0];
            var customAttribute1 = testClassTypeDefinition.CustomAttributes[1];

            var referencedMetadataTokens = new HashSet<MetadataToken>();
            referencedMetadataTokens.Add(customAttribute1.Constructor.MetadataToken);

            var tuple0 = new Tuple<CustomAttribute, ushort>(customAttribute0, 1);
            var tuple1 = new Tuple<CustomAttribute, ushort>(customAttribute1, 2);

            var typesAttributes = new Tuple<CustomAttribute, ushort>[] { tuple0, tuple1 };
            var fieldsAttributes = new Tuple<CustomAttribute, ushort>[0];
            var methodsAttributes = new Tuple<CustomAttribute, ushort>[0];
            var context = TestObjectHelper.GetTestNFAppNanoTablesContext();

            var iut = new nanoAttributesTable(typesAttributes, fieldsAttributes, methodsAttributes, context);

            // test
            iut.RemoveUnusedItems(referencedMetadataTokens);

            var bytesWritten = TestObjectHelper.DoWithNanoBinaryWriter((bw) => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw), (ms, bw, writer) =>
            {
                // test
                iut.Write(writer);
            });

            var methodReferenceId = context.GetMethodReferenceId(customAttribute1.Constructor);
            var signatureId = context.SignaturesTable.GetOrCreateSignatureId(customAttribute1);
            CollectionAssert.AreEqual(
                new byte[]
                {
                    0x04, 0,
                    (byte)(tuple1.Item2 & 0xff), (byte)(tuple1.Item2 >> 8),
                    (byte)(methodReferenceId & 0xff), (byte)(methodReferenceId >> 8),
                    (byte)(signatureId & 0xff), (byte)(signatureId >> 8),
                },
                bytesWritten,
                String.Join(", ", bytesWritten.Select(i => i.ToString("X"))));
        }

        [TestMethod]
        public void RemoveUnusedItems_FieldAttributesTest()
        {
            var nanoTablesContext = TestObjectHelper.GetTestNFAppNanoTablesContext();
            var typeDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllTypeDefinition(nanoTablesContext.AssemblyDefinition);

            var dummyFieldDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllDummyFieldDefinition(typeDefinition);

            Assert.IsTrue(dummyFieldDefinition.CustomAttributes.Count > 1);
            var customAttribute0 = dummyFieldDefinition.CustomAttributes[0];
            var customAttribute1 = dummyFieldDefinition.CustomAttributes[1];

            var referencedMetadataTokens = new HashSet<MetadataToken>();
            referencedMetadataTokens.Add(customAttribute1.Constructor.MetadataToken);

            var tuple0 = new Tuple<CustomAttribute, ushort>(customAttribute0, 1);
            var tuple1 = new Tuple<CustomAttribute, ushort>(customAttribute1, 2);

            var typesAttributes = new Tuple<CustomAttribute, ushort>[0];
            var fieldsAttributes = new Tuple<CustomAttribute, ushort>[] { tuple0, tuple1 };
            var methodsAttributes = new Tuple<CustomAttribute, ushort>[0];
            var context = TestObjectHelper.GetTestNFAppNanoTablesContext();

            var iut = new nanoAttributesTable(typesAttributes, fieldsAttributes, methodsAttributes, context);

            // test
            iut.RemoveUnusedItems(referencedMetadataTokens);

            var bytesWritten = TestObjectHelper.DoWithNanoBinaryWriter((bw) => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw), (ms, bw, writer) =>
            {
                // test
                iut.Write(writer);
            });

            var methodReferenceId = context.GetMethodReferenceId(customAttribute1.Constructor);
            var signatureId = context.SignaturesTable.GetOrCreateSignatureId(customAttribute1);
            CollectionAssert.AreEqual(
                new byte[]
                {
                    0x05, 0,
                    (byte)(tuple1.Item2 & 0xff), (byte)(tuple1.Item2 >> 8),
                    (byte)(methodReferenceId & 0xff), (byte)(methodReferenceId >> 8),
                    (byte)(signatureId & 0xff), (byte)(signatureId >> 8),
                },
                bytesWritten,
                String.Join(", ", bytesWritten.Select(i => i.ToString("X"))));
        }

        [TestMethod]
        public void RemoveUnusedItems_MethodAttributesTest()
        {
            var nanoTablesContext = TestObjectHelper.GetTestNFAppNanoTablesContext();
            var typeDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllTypeDefinition(nanoTablesContext.AssemblyDefinition);
            var methodDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllDummyMethodDefinition(typeDefinition);
            Assert.IsTrue(methodDefinition.CustomAttributes.Count > 1);
            var customAttribute0 = methodDefinition.CustomAttributes[0];
            var customAttribute1 = methodDefinition.CustomAttributes[1];

            var referencedMetadataTokens = new HashSet<MetadataToken>();
            referencedMetadataTokens.Add(customAttribute1.Constructor.MetadataToken);

            var tuple0 = new Tuple<CustomAttribute, ushort>(customAttribute0, 1);
            var tuple1 = new Tuple<CustomAttribute, ushort>(customAttribute1, 2);

            var typesAttributes = new Tuple<CustomAttribute, ushort>[0];
            var fieldsAttributes = new Tuple<CustomAttribute, ushort>[0];
            var methodsAttributes = new Tuple<CustomAttribute, ushort>[] { tuple0, tuple1 };
            var context = TestObjectHelper.GetTestNFAppNanoTablesContext();

            var iut = new nanoAttributesTable(typesAttributes, fieldsAttributes, methodsAttributes, context);

            // test
            iut.RemoveUnusedItems(referencedMetadataTokens);

            var bytesWritten = TestObjectHelper.DoWithNanoBinaryWriter((bw) => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw), (ms, bw, writer) =>
            {
                // test
                iut.Write(writer);
            });

            var methodReferenceId = context.GetMethodReferenceId(customAttribute1.Constructor);
            var signatureId = context.SignaturesTable.GetOrCreateSignatureId(customAttribute1);
            CollectionAssert.AreEqual(
                new byte[]
                {
                    0x06, 0,
                    (byte)(tuple1.Item2 & 0xff), (byte)(tuple1.Item2 >> 8),
                    (byte)(methodReferenceId & 0xff), (byte)(methodReferenceId >> 8),
                    (byte)(signatureId & 0xff), (byte)(signatureId >> 8),
                },
                bytesWritten,
                String.Join(", ", bytesWritten.Select(i => i.ToString("X"))));
        }

        [TestMethod]
        public void TestCodeAnalysisAttributes()
        {
            var assemblyDefinition = TestObjectHelper.GetTestNFAppAssemblyDefinitionWithLoadHints();
            var assemblyBuilder = new nanoAssemblyBuilder(assemblyDefinition, new List<string>(), false);

            using (var stream = File.Open(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite))
            using (var writer = new BinaryWriter(stream))
            {
                // test
                assemblyBuilder.Write(nanoBinaryWriter.CreateLittleEndianBinaryWriter(writer));
            }

            // minimize the assembly, following the first pass
            assemblyBuilder.Minimize();

            // Assert that TypeReferencesTable doesn't contain any of the items in NameSpacesToExclude
            foreach (var item in assemblyBuilder.TablesContext.TypeReferencesTable.Items)
            {
                Assert.IsFalse(TypeReferenceExtensions.NameSpacesToExclude.Any(ns => item.FullName.StartsWith(ns)), $"TypeRef table includes {item.FullName} when it shouldn't.");
            }
        }
    }
}
