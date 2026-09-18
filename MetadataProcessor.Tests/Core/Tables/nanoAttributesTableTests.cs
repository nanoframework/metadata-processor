// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;
using CustomAttribute = Mono.Cecil.CustomAttribute;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Tables
{
    [TestClass]
    public class nanoAttributesTableTests
    {
        [TestMethod]
        public void ConstructorTest()
        {
            var typesAttributes = new Tuple<CustomAttribute, ICustomAttributeProvider>[0];
            var fieldsAttributes = new Tuple<CustomAttribute, ICustomAttributeProvider>[0];
            var methodsAttributes = new Tuple<CustomAttribute, ICustomAttributeProvider>[0];
            var context = TestObjectHelper.GetTestNFAppNanoTablesContext();

            // test
            var iut = new nanoAttributesTable(typesAttributes, fieldsAttributes, methodsAttributes, context);

            // no op
        }

        [TestMethod]
        public void PropertyAttributesAreCascadedToAccessorsWithoutDuplicates()
        {
            var context = TestObjectHelper.GetTestNFAppNanoTablesContext();
            var typeDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllTypeDefinition(context.AssemblyDefinition);
            var property = typeDefinition.Properties.Single(item => item.Name == "DummyProperty");

            Assert.IsTrue(context.MethodDefinitionTable.TryGetMethodReferenceId(property.GetMethod, out var getterId));
            Assert.IsTrue(context.MethodDefinitionTable.TryGetMethodReferenceId(property.SetMethod, out var setterId));

            var bytesWritten = TestObjectHelper.DoWithNanoBinaryWriter((bw) => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw), (ms, bw, writer) =>
            {
                context.AttributesTable.Write(writer);
            });

            // each attribute record is: owner table (2), owner index (2), constructor ref id (2), signature id (2)
            var methodAttributes = Enumerable.Range(0, bytesWritten.Length / 8)
                .Where(index => BitConverter.ToUInt16(bytesWritten, index * 8) == 0x0006)
                .Select(index => new
                {
                    Target = BitConverter.ToUInt16(bytesWritten, index * 8 + 2),
                    Constructor = BitConverter.ToUInt16(bytesWritten, index * 8 + 4)
                })
                .ToList();

            // expected constructors are the ones of the attributes declared on the property
            var expectedConstructors = property.CustomAttributes
                .Select(attribute => context.GetMethodReferenceId(attribute.Constructor))
                .OrderBy(id => id)
                .ToList();

            Assert.AreEqual(2, expectedConstructors.Count);
            Assert.AreEqual(2, expectedConstructors.Distinct().Count());

            // getter has [DummyCustomAttribute1] declared on it, so it must not be duplicated
            CollectionAssert.AreEqual(
                expectedConstructors,
                methodAttributes.Where(a => a.Target == getterId).Select(a => a.Constructor).OrderBy(id => id).ToList());

            CollectionAssert.AreEqual(
                expectedConstructors,
                methodAttributes.Where(a => a.Target == setterId).Select(a => a.Constructor).OrderBy(id => id).ToList());
        }

        // see TestNFApp.PropertyAttributesTestClass for the declarations
        [DataTestMethod]
        [DataRow("GetOnlyProperty", "DummyCustomAttribute1,DummyCustomAttribute2", null)]
        [DataRow("SetOnlyProperty", null, "DummyCustomAttribute1,DummyCustomAttribute2")]
        [DataRow("PropertyWithSetterAttribute", "DummyCustomAttribute1,DummyCustomAttribute2", "DummyCustomAttribute1,DummyCustomAttribute2")]
        [DataRow("PropertyWithOtherAttributeOnSetter", "DummyCustomAttribute1", "DummyCustomAttribute1,DummyCustomAttribute2")]
        [DataRow("SetterOnlyAttribute", "", "DummyCustomAttribute1")]
        [DataRow("GetterOnlyAttribute", "DummyCustomAttribute1", "")]
        [DataRow("SetOnlyPropertyWithSetterAttribute", null, "DummyCustomAttribute1,DummyCustomAttribute2")]
        public void PropertyAttributesAreCascadedToAccessorsVariations(
            string propertyName,
            string expectedGetterAttributes,
            string expectedSetterAttributes)
        {
            var context = TestObjectHelper.GetTestNFAppNanoTablesContext();
            var typeDefinition = context.AssemblyDefinition.MainModule.GetType("TestNFApp.PropertyAttributesTestClass");
            var property = typeDefinition.Properties.Single(item => item.Name == propertyName);

            AssertAccessorAttributes(property.GetMethod, expectedGetterAttributes, $"{propertyName} getter");
            AssertAccessorAttributes(property.SetMethod, expectedSetterAttributes, $"{propertyName} setter");
        }

        private static void AssertAccessorAttributes(
            MethodDefinition accessor,
            string expectedAttributes,
            string accessorDescription)
        {
            if (expectedAttributes == null)
            {
                Assert.IsNull(accessor, $"{accessorDescription} isn't expected to exist");
                return;
            }

            Assert.IsNotNull(accessor, $"{accessorDescription} is expected to exist");

            var expected = expectedAttributes
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .OrderBy(name => name)
                .ToList();

            var actual = accessor.CustomAttributes
                .Select(attribute => attribute.AttributeType.Name)
                .OrderBy(name => name)
                .ToList();

            CollectionAssert.AreEqual(expected, actual, $"{accessorDescription} has [{string.Join(", ", actual)}], expected [{string.Join(", ", expected)}]");
        }

        [TestMethod]
        public void PropertyAttributesAreCascadedIdempotently()
        {
            var context = TestObjectHelper.GetTestNFAppNanoTablesContext();
            var typeDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllTypeDefinition(context.AssemblyDefinition);
            var property = typeDefinition.Properties.Single(item => item.Name == "DummyProperty");

            int getterCount = property.GetMethod.CustomAttributes.Count;
            int setterCount = property.SetMethod.CustomAttributes.Count;

            Assert.AreEqual(2, getterCount);
            Assert.AreEqual(2, setterCount);

            // building a second context over the same (already mutated) assembly definition must not add anything
            _ = new nanoTablesContext(
                context.AssemblyDefinition,
                null,
                null,
                false,
                false,
                false);

            Assert.AreEqual(getterCount, property.GetMethod.CustomAttributes.Count);
            Assert.AreEqual(setterCount, property.SetMethod.CustomAttributes.Count);
        }

        [TestMethod]
        public void IsSameAttribute_DistinguishesSameNamedAttributesFromDifferentAssemblies()
        {
            var module = TestObjectHelper.GetTestNFAppAssemblyDefinition().MainModule;

            var assemblyA = new AssemblyNameReference("AssemblyA", new Version(1, 0));
            var assemblyB = new AssemblyNameReference("AssemblyB", new Version(1, 0));

            var constructorA = CreateAttributeConstructor(module, assemblyA, 1);
            var constructorB = CreateAttributeConstructor(module, assemblyB, 2);

            // same name, different assemblies...
            Assert.AreEqual(constructorA.FullName, constructorB.FullName);

            // ...but the metadata token is not!
            Assert.AreNotEqual(constructorA.MetadataToken, constructorB.MetadataToken);

            // custom attribute blob: prolog 0x0001, no fixed arguments, 0 named arguments
            byte[] emptyBlob = { 0x01, 0x00, 0x00, 0x00 };

            var attributeA = new CustomAttribute(constructorA, emptyBlob);
            var attributeB = new CustomAttribute(constructorB, emptyBlob);

            Assert.IsFalse(nanoTablesContext.IsSameAttribute(attributeA, attributeB), "Same named attributes from different assemblies must not be considered the same.");

            // same constructor, same arguments
            var attributeA2 = new CustomAttribute(constructorA, (byte[])emptyBlob.Clone());
            Assert.IsTrue(nanoTablesContext.IsSameAttribute(attributeA, attributeA2));

            // same constructor, different arguments (one named argument)
            byte[] otherBlob = { 0x01, 0x00, 0x01, 0x00 };
            var attributeA3 = new CustomAttribute(constructorA, otherBlob);
            Assert.IsFalse(nanoTablesContext.IsSameAttribute(attributeA, attributeA3));
        }

        private static MethodReference CreateAttributeConstructor(
            ModuleDefinition module,
            AssemblyNameReference scope,
            uint rid)
        {
            var attributeType = new TypeReference("TestNamespace", "SameNameAttribute", module, scope);

            return new MethodReference(".ctor", module.TypeSystem.Void, attributeType)
            {
                HasThis = true,
                MetadataToken = new MetadataToken(TokenType.MemberRef, rid)
            };
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

            var tuple0 = new Tuple<CustomAttribute, ICustomAttributeProvider>(customAttribute0, testClassTypeDefinition);
            var tuple1 = new Tuple<CustomAttribute, ICustomAttributeProvider>(customAttribute1, testClassTypeDefinition);

            var typesAttributes = new Tuple<CustomAttribute, ICustomAttributeProvider>[] { tuple0, tuple1 };
            var fieldsAttributes = new Tuple<CustomAttribute, ICustomAttributeProvider>[0];
            var methodsAttributes = new Tuple<CustomAttribute, ICustomAttributeProvider>[0];
            var context = TestObjectHelper.GetTestNFAppNanoTablesContext();

            var iut = new nanoAttributesTable(typesAttributes, fieldsAttributes, methodsAttributes, context);

            // test
            iut.RemoveUnusedItems(referencedMetadataTokens);

            var bytesWritten = TestObjectHelper.DoWithNanoBinaryWriter((bw) => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw), (ms, bw, writer) =>
            {
                // test
                iut.Write(writer);
            });

            // Get the expected reference ID for the type definition
            ushort expectedTypeRefId = 0;
            context.TypeDefinitionTable.TryGetTypeReferenceId(testClassTypeDefinition, out expectedTypeRefId);

            var methodReferenceId = context.GetMethodReferenceId(customAttribute1.Constructor);
            var signatureId = context.SignaturesTable.GetOrCreateSignatureId(customAttribute1);
            CollectionAssert.AreEqual(
                new byte[]
                {
                    0x04, 0,
                    (byte)(expectedTypeRefId & 0xff), (byte)(expectedTypeRefId >> 8),
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

            var tuple0 = new Tuple<CustomAttribute, ICustomAttributeProvider>(customAttribute0, dummyFieldDefinition);
            var tuple1 = new Tuple<CustomAttribute, ICustomAttributeProvider>(customAttribute1, dummyFieldDefinition);

            var typesAttributes = new Tuple<CustomAttribute, ICustomAttributeProvider>[0];
            var fieldsAttributes = new Tuple<CustomAttribute, ICustomAttributeProvider>[] { tuple0, tuple1 };
            var methodsAttributes = new Tuple<CustomAttribute, ICustomAttributeProvider>[0];
            var context = TestObjectHelper.GetTestNFAppNanoTablesContext();

            var iut = new nanoAttributesTable(typesAttributes, fieldsAttributes, methodsAttributes, context);

            // test
            iut.RemoveUnusedItems(referencedMetadataTokens);

            var bytesWritten = TestObjectHelper.DoWithNanoBinaryWriter((bw) => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw), (ms, bw, writer) =>
            {
                // test
                iut.Write(writer);
            });

            // Get the expected reference ID for the field definition
            ushort expectedFieldRefId = 0;
            context.FieldsTable.TryGetFieldDefinitionId(dummyFieldDefinition, false, out expectedFieldRefId);

            var methodReferenceId = context.GetMethodReferenceId(customAttribute1.Constructor);
            var signatureId = context.SignaturesTable.GetOrCreateSignatureId(customAttribute1);
            CollectionAssert.AreEqual(
                new byte[]
                {
                    0x05, 0,
                    (byte)(expectedFieldRefId & 0xff), (byte)(expectedFieldRefId >> 8),
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

            var tuple0 = new Tuple<CustomAttribute, ICustomAttributeProvider>(customAttribute0, methodDefinition);
            var tuple1 = new Tuple<CustomAttribute, ICustomAttributeProvider>(customAttribute1, methodDefinition);

            var typesAttributes = new Tuple<CustomAttribute, ICustomAttributeProvider>[0];
            var fieldsAttributes = new Tuple<CustomAttribute, ICustomAttributeProvider>[0];
            var methodsAttributes = new Tuple<CustomAttribute, ICustomAttributeProvider>[] { tuple0, tuple1 };
            var context = TestObjectHelper.GetTestNFAppNanoTablesContext();

            var iut = new nanoAttributesTable(typesAttributes, fieldsAttributes, methodsAttributes, context);

            // test
            iut.RemoveUnusedItems(referencedMetadataTokens);

            var bytesWritten = TestObjectHelper.DoWithNanoBinaryWriter((bw) => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw), (ms, bw, writer) =>
            {
                // test
                iut.Write(writer);
            });

            // Get the expected reference ID for the method definition
            ushort expectedMethodRefId = 0;
            context.MethodDefinitionTable.TryGetMethodReferenceId(methodDefinition, out expectedMethodRefId);

            var methodReferenceId = context.GetMethodReferenceId(customAttribute1.Constructor);
            var signatureId = context.SignaturesTable.GetOrCreateSignatureId(customAttribute1);
            CollectionAssert.AreEqual(
                new byte[]
                {
                    0x06, 0,
                    (byte)(expectedMethodRefId & 0xff), (byte)(expectedMethodRefId >> 8),
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
            var assemblyBuilder = new nanoAssemblyBuilder(assemblyDefinition, false);

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
