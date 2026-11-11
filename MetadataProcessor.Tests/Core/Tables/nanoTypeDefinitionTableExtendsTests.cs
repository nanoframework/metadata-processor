// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

        private static nanoAssemblyBuilder BuildMinimizedBuilder()
        {
            var assemblyDefinition = TestObjectHelper.GetTestNFAppAssemblyDefinitionWithLoadHints();
            var assemblyBuilder = new nanoAssemblyBuilder(assemblyDefinition, false);

            TestObjectHelper.DoWithNanoBinaryWriter(
                bw => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw),
                (ms, bw, writer) => assemblyBuilder.Write(writer));

            // rebuilds the TypeSpec table and its signatures from what is left, and fails if a type reference was pruned
            assemblyBuilder.Minimize();

            return assemblyBuilder;
        }

        private static byte[] WriteTable(Action<nanoBinaryWriter> write)
        {
            using (var stream = new MemoryStream())
            {
                write(nanoBinaryWriter.CreateLittleEndianBinaryWriter(new BinaryWriter(stream)));

                return stream.ToArray();
            }
        }

        private const string ExternalArgumentType = "TestNFApp.DerivedFromGenericOfExternalType";

        [TestMethod]
        public void Extends_AfterMinimize_WritesTheSignatureOfTheGenericBase()
        {
            nanoTablesContext context = BuildMinimizedBuilder().TablesContext;
            ushort extends = ReadExtends(context, ExternalArgumentType);

            Assert.AreEqual(0xC000, extends & 0xC000);

            // the index is the one of the rebuilt TypeSpec table
            int typeSpecIndex = extends & 0x3FFF;
            TypeReference baseType = context.TypeSpecificationsTable.TryGetTypeReferenceByIndex((ushort)typeSpecIndex);

            Assert.IsNotNull(baseType);
            Assert.AreEqual("TestNFApp.GenericBase`1<System.Version>", baseType.FullName);

            TypeReference argument = ((GenericInstanceType)baseType).GenericArguments[0];

            Assert.IsTrue(
                context.TypeReferencesTable.TryGetTypeReferenceId(argument, out ushort argumentId),
                "The type reference used as argument of the generic base was removed.");

            // the TypeSpec row points to its signature in the written signatures
            byte[] typeSpecRows = WriteTable(w => context.TypeSpecificationsTable.Write(w));
            ushort signatureId = BitConverter.ToUInt16(typeSpecRows, typeSpecIndex * 2);
            byte[] signatures = WriteTable(w => context.SignaturesTable.Write(w));

            MethodInfo getSignature = typeof(nanoSignaturesTable).GetMethod(
                "GetSignature",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(TypeReference), typeof(bool) },
                null);
            byte[] expected = (byte[])getSignature.Invoke(context.SignaturesTable, new object[] { baseType, false });

            CollectionAssert.AreEqual(expected, signatures.Skip(signatureId).Take(expected.Length).ToArray());

            // generic instance of the base, whose last element is the argument as TypeRef (tag 1)
            Assert.AreEqual((byte)NanoCLRDataType.DATATYPE_GENERICINST, expected[0]);

            byte[] argumentToken;

            using (var stream = new MemoryStream())
            {
                nanoBinaryWriter.CreateBigEndianBinaryWriter(new BinaryWriter(stream)).WriteMetadataToken((ushort)((argumentId << 2) | 1));
                argumentToken = stream.ToArray();
            }

            CollectionAssert.AreEqual(
                argumentToken,
                expected.Skip(expected.Length - argumentToken.Length).ToArray());
        }

        [TestMethod]
        public void Extends_AfterMinimize_ReassignsTheTypeSpecIndex()
        {
            nanoTablesContext before = TestObjectHelper.GetTestNFAppNanoTablesContext();
            nanoTablesContext after = BuildMinimizedBuilder().TablesContext;

            int indexBefore = ReadExtends(before, ExternalArgumentType) & 0x3FFF;
            int indexAfter = ReadExtends(after, ExternalArgumentType) & 0x3FFF;

            Assert.AreNotEqual(indexBefore, indexAfter, "Pruning didn't move the TypeSpec of the base: this doesn't exercise the reassignment.");
        }

        [TestMethod]
        public void DependencyList_OfTypeWithGenericBase_PinsBaseAndArguments()
        {
            nanoAssemblyBuilder assemblyBuilder = new nanoAssemblyBuilder(
                TestObjectHelper.GetTestNFAppAssemblyDefinitionWithLoadHints(),
                false);
            TypeDefinition type = assemblyBuilder.TablesContext.TypeDefinitionTable.Items.First(t => t.FullName == ExternalArgumentType);

            var set = (HashSet<MetadataToken>)typeof(nanoAssemblyBuilder)
                .GetMethod("BuildDependencyList", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(MetadataToken) }, null)
                .Invoke(assemblyBuilder, new object[] { type.MetadataToken });

            var baseType = (GenericInstanceType)type.BaseType;

            Assert.IsTrue(set.Contains(baseType.MetadataToken));
            Assert.IsTrue(set.Contains(baseType.ElementType.MetadataToken));
            Assert.IsTrue(set.Contains(baseType.GenericArguments[0].MetadataToken));
        }

        [TestMethod]
        public void PinTypeSpecificationParts_FollowsTheTypeOfAModifier()
        {
            var assemblyDefinition = TestObjectHelper.GetTestNFAppAssemblyDefinitionWithLoadHints();
            var assemblyBuilder = new nanoAssemblyBuilder(assemblyDefinition, false);
            TypeReference modifier = assemblyDefinition.MainModule.GetTypeReferences().First(t => t.FullName == "System.Version");
            TypeReference element = assemblyDefinition.MainModule.GetTypeReferences()
                .First(t => t.MetadataType == MetadataType.Class && t.FullName != modifier.FullName);
            MethodInfo pin = typeof(nanoAssemblyBuilder).GetMethod("PinTypeSpecificationParts", BindingFlags.Instance | BindingFlags.NonPublic);

            foreach (TypeReference spec in new TypeReference[]
            {
                new RequiredModifierType(modifier, element),
                new OptionalModifierType(modifier, element)
            })
            {
                var set = new HashSet<MetadataToken>();

                pin.Invoke(assemblyBuilder, new object[] { spec, set });

                Assert.IsTrue(set.Contains(modifier.MetadataToken), $"{spec.GetType().Name}: modifier type not pinned.");
                Assert.IsTrue(set.Contains(element.MetadataToken), $"{spec.GetType().Name}: element type not pinned.");
            }
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
