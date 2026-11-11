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
        private const string ExternalArgumentType = "TestNFApp.DerivedFromGenericOfExternalType";

        // CLR_RECORD_ASSEMBLY: marker, header CRC, assembly CRC, flags, native contract hash, version, assembly name,
        // string table version, then the start offset of each table (CLR_TABLESENUM order)
        private const int StartOfTablesOffset = 36;
        private const int TypeDefTable = 4;
        private const int TypeSpecTable = 9;
        private const int SignaturesTable = 14;

        // sizes of CLR_RECORD_TYPEDEF and CLR_RECORD_TYPESPEC; the extends field is after name and namespace
        private const int TypeDefRecordSize = 27;
        private const int ExtendsOffset = 4;
        private const int TypeSpecRecordSize = 2;

        private sealed class FinalAssembly
        {
            public byte[] Bytes;
            public nanoTablesContext Context;

            private int TableStart(int table)
            {
                return (int)BitConverter.ToUInt32(Bytes, StartOfTablesOffset + (table * sizeof(uint)));
            }

            public ushort Extends(string typeName)
            {
                TypeDefinition type = Context.TypeDefinitionTable.Items.First(t => t.FullName == typeName);

                Assert.IsTrue(Context.TypeDefinitionTable.TryGetTypeReferenceId(type, out ushort typeId));

                return BitConverter.ToUInt16(Bytes, TableStart(TypeDefTable) + (typeId * TypeDefRecordSize) + ExtendsOffset);
            }

            public ushort SignatureIdOfTypeSpec(int typeSpecIndex)
            {
                return BitConverter.ToUInt16(Bytes, TableStart(TypeSpecTable) + (typeSpecIndex * TypeSpecRecordSize));
            }

            public byte[] SignatureAt(int signatureId, int length)
            {
                return Bytes.Skip(TableStart(SignaturesTable) + signatureId).Take(length).ToArray();
            }
        }

        // the assembly as it is written after minimizing it, the same sequence as the build does
        private static FinalAssembly BuildFinalAssembly(AssemblyDefinition assemblyDefinition)
        {
            var assemblyBuilder = new nanoAssemblyBuilder(assemblyDefinition, false);

            TestObjectHelper.DoWithNanoBinaryWriter(
                bw => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw),
                (ms, bw, writer) => assemblyBuilder.Write(writer));

            assemblyBuilder.Minimize();

            byte[] bytes = TestObjectHelper.DoWithNanoBinaryWriter(
                bw => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw),
                (ms, bw, writer) => assemblyBuilder.Write(writer));

            Assert.AreEqual("NFMRK2", System.Text.Encoding.ASCII.GetString(bytes, 0, 6));

            return new FinalAssembly { Bytes = bytes, Context = assemblyBuilder.TablesContext };
        }

        private static AssemblyDefinition LoadAssembly()
        {
            return TestObjectHelper.GetTestNFAppAssemblyDefinitionWithLoadHints();
        }

        // C# can't write a modifier in the argument of a base type: build the assembly with Cecil, and read it back so
        // its metadata has the tokens of a compiled one. The modifier is a type reference that nothing else uses.
        private static AssemblyDefinition CreateAssemblyWithModifiedGenericBase(bool required)
        {
            AssemblyDefinition mscorlib = TestObjectHelper.GetmscorlibAssemblyDefinition();
            var resolver = new LoadHintsAssemblyResolver(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["mscorlib"] = mscorlib.MainModule.FileName });

            AssemblyDefinition created = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition("ModifiedGenericBase", new Version(1, 0, 0, 0)),
                "ModifiedGenericBase",
                new ModuleParameters { Kind = ModuleKind.Dll, AssemblyResolver = resolver });
            ModuleDefinition module = created.MainModule;

            TypeReference objectType = module.ImportReference(mscorlib.MainModule.GetType("System.Object"));
            TypeReference intType = module.ImportReference(mscorlib.MainModule.GetType("System.Int32"));
            TypeReference modifier = module.ImportReference(mscorlib.MainModule.GetType("System.Version"));

            var genericBase = new TypeDefinition("Fixture", "GenericBase`1", Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Class, objectType);
            genericBase.GenericParameters.Add(new GenericParameter("T", genericBase));
            module.Types.Add(genericBase);

            var baseType = new GenericInstanceType(genericBase);
            baseType.GenericArguments.Add(required
                ? (TypeReference)new RequiredModifierType(modifier, intType)
                : new OptionalModifierType(modifier, intType));

            module.Types.Add(new TypeDefinition("Fixture", "ModifiedBase", Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Class, baseType));

            using (var stream = new MemoryStream())
            {
                created.Write(stream);
                stream.Position = 0;

                return AssemblyDefinition.ReadAssembly(stream, new ReaderParameters { AssemblyResolver = resolver, ReadingMode = ReadingMode.Immediate });
            }
        }

        private static void AssertModifiedGenericBaseIsWritten(bool required)
        {
            // the modifier type is a type reference that only the signature of the base needs: minimizing prunes it
            // unless the dependencies of the base include it, and writing the signature fails without it
            FinalAssembly assembly = BuildFinalAssembly(CreateAssemblyWithModifiedGenericBase(required));
            ushort extends = assembly.Extends("Fixture.ModifiedBase");

            Assert.AreEqual(0xC000, extends & 0xC000);

            int typeSpecIndex = extends & 0x3FFF;
            var baseType = (GenericInstanceType)assembly.Context.TypeSpecificationsTable.TryGetTypeReferenceByIndex((ushort)typeSpecIndex);
            var argument = (IModifierType)baseType.GenericArguments[0];

            Assert.IsInstanceOfType(argument, required ? typeof(RequiredModifierType) : typeof(OptionalModifierType));
            Assert.IsTrue(
                assembly.Context.TypeReferencesTable.TryGetTypeReferenceId(argument.ModifierType, out ushort modifierId),
                "The type reference of the modifier was removed.");

            MethodInfo getSignature = typeof(nanoSignaturesTable).GetMethod(
                "GetSignature",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(TypeReference), typeof(bool) },
                null);
            byte[] expected = (byte[])getSignature.Invoke(assembly.Context.SignaturesTable, new object[] { baseType, false });
            byte[] written = assembly.SignatureAt(assembly.SignatureIdOfTypeSpec(typeSpecIndex), expected.Length);

            CollectionAssert.AreEqual(expected, written);

            // the modifier marker is followed by the type reference of the modifier
            byte marker = (byte)(required ? nanoSerializationType.ELEMENT_TYPE_CMOD_REQD : nanoSerializationType.ELEMENT_TYPE_CMOD_OPT);
            int markerAt = Array.IndexOf(written, marker);
            byte[] token = ArgumentToken(modifierId);

            Assert.IsTrue(markerAt >= 0, "The signature has no modifier.");
            CollectionAssert.AreEqual(token, written.Skip(markerAt + 1).Take(token.Length).ToArray());
        }

        [TestMethod]
        public void Extends_GenericBaseWithRequiredModifier_SurvivesMinimizationAndSerialization()
        {
            AssertModifiedGenericBaseIsWritten(true);
        }

        [TestMethod]
        public void Extends_GenericBaseWithOptionalModifier_SurvivesMinimizationAndSerialization()
        {
            AssertModifiedGenericBaseIsWritten(false);
        }

        private static byte[] ArgumentToken(ushort typeRefId)
        {
            using (var stream = new MemoryStream())
            {
                // TypeDefOrRef coded index: TypeRef tag is 1, in 2 bits
                nanoBinaryWriter.CreateBigEndianBinaryWriter(new BinaryWriter(stream)).WriteMetadataToken((ushort)((typeRefId << 2) | 1));

                return stream.ToArray();
            }
        }

        private static void AssertGenericBaseIsWritten(FinalAssembly assembly, string typeName, string baseName)
        {
            ushort extends = assembly.Extends(typeName);

            Assert.AreEqual(0xC000, extends & 0xC000);

            // the index is the one of the rebuilt TypeSpec table
            int typeSpecIndex = extends & 0x3FFF;
            TypeReference baseType = assembly.Context.TypeSpecificationsTable.TryGetTypeReferenceByIndex((ushort)typeSpecIndex);

            Assert.IsNotNull(baseType);
            Assert.AreEqual(baseName, baseType.FullName);

            TypeReference argument = ((GenericInstanceType)baseType).GenericArguments[0];

            Assert.IsTrue(
                assembly.Context.TypeReferencesTable.TryGetTypeReferenceId(argument, out ushort argumentId),
                "The type reference used as argument of the generic base was removed.");

            // the TypeSpec row of the written assembly points to its signature
            MethodInfo getSignature = typeof(nanoSignaturesTable).GetMethod(
                "GetSignature",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(TypeReference), typeof(bool) },
                null);
            byte[] expected = (byte[])getSignature.Invoke(assembly.Context.SignaturesTable, new object[] { baseType, false });
            byte[] written = assembly.SignatureAt(assembly.SignatureIdOfTypeSpec(typeSpecIndex), expected.Length);

            CollectionAssert.AreEqual(expected, written);

            // generic instance of the base, whose last element is the argument as TypeRef
            Assert.AreEqual((byte)NanoCLRDataType.DATATYPE_GENERICINST, written[0]);

            byte[] argumentToken = ArgumentToken(argumentId);

            CollectionAssert.AreEqual(argumentToken, written.Skip(written.Length - argumentToken.Length).ToArray());
        }

        [TestMethod]
        public void Extends_MinimizedAssembly_WritesTheSignatureOfTheGenericBase()
        {
            AssertGenericBaseIsWritten(
                BuildFinalAssembly(LoadAssembly()),
                ExternalArgumentType,
                "TestNFApp.GenericBase`1<System.Version>");
        }

        // the generic definition is in another assembly, so no member of it is kept to retain the TypeSpec: it is kept by
        // the extends of the type only, that is what the check of the base type is for
        [TestMethod]
        public void Extends_ExternalGenericBaseAsOnlyReference_KeepsTheTypeSpecAndItsArguments()
        {
            const string typeName = "TestNFApp.DerivedFromExternalGeneric";
            const string baseName = "TestNFClassLibrary.ExternalGenericBase`1<System.Version>";

            AssemblyDefinition assemblyDefinition = LoadAssembly();

            // the implicit constructor calls the constructor of the base, a reference that keeps the TypeSpec by itself
            assemblyDefinition.MainModule.GetType(typeName).Methods.Clear();

            FinalAssembly assembly = BuildFinalAssembly(assemblyDefinition);
            nanoTablesContext context = assembly.Context;

            // once minimized nothing but the extends of the type refers to the TypeSpec: the member references of the
            // module list the call to the constructor of the base, but without a caller they are pruned
            Assert.IsFalse(context.MethodReferencesTable.Items.Any(m => m.DeclaringType.FullName == baseName));
            Assert.IsFalse(context.FieldReferencesTable.Items.Any(f => f.DeclaringType.FullName == baseName));
            Assert.IsFalse(context.MethodSpecificationTable.Items.Any(m => m.DeclaringType != null && m.DeclaringType.FullName == baseName));
            Assert.AreEqual(1, context.TypeDefinitionTable.Items.Count(t => t.BaseType != null && t.BaseType.FullName == baseName));

            AssertGenericBaseIsWritten(assembly, typeName, baseName);
        }
        [TestMethod]
        public void Extends_AfterMinimize_ReassignsTheTypeSpecIndex()
        {
            nanoTablesContext before = TestObjectHelper.GetTestNFAppNanoTablesContext();
            FinalAssembly after = BuildFinalAssembly(LoadAssembly());

            TypeDefinition type = before.TypeDefinitionTable.Items.First(t => t.FullName == ExternalArgumentType);

            Assert.IsTrue(before.TypeDefinitionTable.TryGetTypeReferenceId(type, out ushort typeId));

            // the type definition table of a fresh context, written once: the assembly before minimizing
            byte[] table = TestObjectHelper.DoWithNanoBinaryWriter(
                bw => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw),
                (ms, bw, writer) => before.TypeDefinitionTable.Write(writer));

            int indexBefore = BitConverter.ToUInt16(table, (typeId * TypeDefRecordSize) + ExtendsOffset) & 0x3FFF;
            int indexAfter = after.Extends(ExternalArgumentType) & 0x3FFF;

            Assert.AreNotEqual(indexBefore, indexAfter, "Pruning didn't move the TypeSpec of the base: this doesn't exercise the reassignment.");
        }

        [TestMethod]
        public void DependencyList_OfTypeWithGenericBase_PinsBaseAndArguments()
        {
            nanoAssemblyBuilder assemblyBuilder = new nanoAssemblyBuilder(LoadAssembly(), false);
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
            var assemblyDefinition = LoadAssembly();
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
            ushort extends = BuildFinalAssembly(LoadAssembly()).Extends("TestNFApp.DerivedFromNonGeneric");

            Assert.AreEqual(0, extends & 0x8000, "A TypeDef base must keep bit 15 clear.");
        }

        [TestMethod]
        public void Extends_ClosedGenericBase_IsEncodedAsTypeSpec()
        {
            FinalAssembly assembly = BuildFinalAssembly(LoadAssembly());
            ushort extends = assembly.Extends("TestNFApp.DerivedFromClosedGeneric");

            Assert.AreEqual(0xC000, extends & 0xC000);

            TypeReference baseType = assembly.Context.TypeSpecificationsTable.TryGetTypeReferenceByIndex((ushort)(extends & 0x3FFF));

            Assert.IsNotNull(baseType);
            Assert.AreEqual("TestNFApp.GenericBase`1<System.Int32>", baseType.FullName);
        }

        [TestMethod]
        public void Extends_OpenGenericBase_IsEncodedAsTypeSpec()
        {
            FinalAssembly assembly = BuildFinalAssembly(LoadAssembly());
            ushort extends = assembly.Extends("TestNFApp.DerivedFromOpenGeneric`1");

            Assert.AreEqual(0xC000, extends & 0xC000);

            TypeReference baseType = assembly.Context.TypeSpecificationsTable.TryGetTypeReferenceByIndex((ushort)(extends & 0x3FFF));

            Assert.IsNotNull(baseType);
            Assert.IsInstanceOfType(baseType, typeof(GenericInstanceType));
            Assert.AreEqual("TestNFApp.GenericBase`1", ((GenericInstanceType)baseType).ElementType.FullName);
        }
    }
}
