// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Tables
{
    [TestClass]
    public class nanoTypeDefinitionTableExtendsTests
    {
        private const int ExtendsOffset = 4;

        private static ushort ReadExtends(
            nanoTablesContext context,
            string typeName)
        {
            var items = context.TypeDefinitionTable.Items.ToList();
            int index = items.FindIndex(t => t.FullName == typeName);

            Assert.IsTrue(index >= 0, $"Type {typeName} not found.");

            using (var stream = new MemoryStream())
            {
                var writer = nanoBinaryWriter.CreateLittleEndianBinaryWriter(new BinaryWriter(stream));

                context.TypeDefinitionTable.Write(writer);

                byte[] bytes = stream.ToArray();
                int recordSize = bytes.Length / items.Count;

                Assert.AreEqual(0, bytes.Length % items.Count);

                return BitConverter.ToUInt16(bytes, (index * recordSize) + ExtendsOffset);
            }
        }

        private static nanoTablesContext BuildContext()
        {
            return TestObjectHelper.GetTestNFAppNanoTablesContext();
        }

        [TestMethod]
        public void Extends_PlainBase_IsEncodedAsTypeDef()
        {
            ushort extends = ReadExtends(BuildContext(), "TestNFApp.DerivedFromNonGeneric");

            Assert.AreEqual(0, extends & 0x8000, "A TypeDef base must keep bit 15 clear.");
        }

        [TestMethod]
        public void Extends_ClosedGenericBase_IsEncodedAsTypeSpec()
        {
            nanoTablesContext context = BuildContext();
            ushort extends = ReadExtends(context, "TestNFApp.DerivedFromClosedGeneric");

            Assert.AreEqual(0xC000, extends & 0xC000);

            TypeReference baseType = context.TypeSpecificationsTable.TryGetTypeReferenceByIndex((ushort)(extends & 0x3FFF));

            Assert.IsNotNull(baseType);
            Assert.AreEqual("TestNFApp.GenericBase`1<System.Int32>", baseType.FullName);
        }

        [TestMethod]
        public void Extends_OpenGenericBase_IsEncodedAsTypeSpec()
        {
            nanoTablesContext context = BuildContext();
            ushort extends = ReadExtends(context, "TestNFApp.DerivedFromOpenGeneric`1");

            Assert.AreEqual(0xC000, extends & 0xC000);

            TypeReference baseType = context.TypeSpecificationsTable.TryGetTypeReferenceByIndex((ushort)(extends & 0x3FFF));

            Assert.IsNotNull(baseType);
            Assert.IsInstanceOfType(baseType, typeof(GenericInstanceType));
            Assert.AreEqual("TestNFApp.GenericBase`1", ((GenericInstanceType)baseType).ElementType.FullName);
        }
    }
}
