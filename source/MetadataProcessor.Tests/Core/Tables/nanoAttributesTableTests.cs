using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using System.Reflection;
using System.IO;
using System.Text;

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

            var dummyFieldDefinition = typeDefinition.Fields.First(i=>i.Name == "dummyField");

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
    }


}
