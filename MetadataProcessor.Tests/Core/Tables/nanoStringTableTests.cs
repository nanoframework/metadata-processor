// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Tables
{
    [TestClass]
    public class nanoStringTableTests
    {
        private const string KeptLiteral = "LiteralOfKeptMethod";
        private const string ExcludedLiteral = "LiteralOfExcludedNestedMethod";

        [TestMethod]
        public void Write_ExcludedNestedTypeWithOwnLiteral_LeavesTheLiteralOut()
        {
            // the string table is collected and frozen after minimizing: a literal reached only from an excluded
            // nested type must not be added afterwards
            nanoTablesContext context = BuildFinalAssembly(CreateFixture(","));
            Dictionary<string, ushort> strings = context.StringTable.GetItems();

            Assert.IsTrue(strings.ContainsKey(KeptLiteral), "The literal of the kept method is missing.");
            Assert.IsFalse(strings.ContainsKey(ExcludedLiteral), "The literal of the excluded nested type was written.");
        }

        [TestMethod]
        public void Write_AttributeStringAtNullSentinel_UsesALocalCopy()
        {
            // 0xFFFF is "null" for a string in an attribute blob, so the shared string with that ID needs a local copy
            string atSentinel = nanoStringsConstants.TryGetString(0xFFFF);

            Assert.IsNotNull(atSentinel, "No shared string has ID 0xFFFF.");

            nanoTablesContext context = BuildFinalAssembly(CreateFixture(atSentinel));
            ushort id = context.StringTable.GetOrCreateAttributeStringId(atSentinel);

            Assert.AreNotEqual(0xFFFF, id);
            Assert.AreEqual(atSentinel, context.StringTable.TryGetString(id));
        }

        // the assembly as it is written after minimizing it, the same sequence as the build does
        private static nanoTablesContext BuildFinalAssembly(AssemblyDefinition assemblyDefinition)
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

            return assemblyBuilder.TablesContext;
        }

        // Fixture.Outer has a method loading a literal and an attribute with a string argument; its nested type is
        // excluded with ExcludeTypeAttribute (matched by name) and loads a literal no other method uses
        private static AssemblyDefinition CreateFixture(string attributeText)
        {
            AssemblyDefinition mscorlib = TestObjectHelper.GetmscorlibAssemblyDefinition();
            var resolver = new LoadHintsAssemblyResolver(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["mscorlib"] = mscorlib.MainModule.FileName });

            AssemblyDefinition created = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition("StringTableFixture", new Version(1, 0, 0, 0)),
                "StringTableFixture",
                new ModuleParameters { Kind = ModuleKind.Dll, AssemblyResolver = resolver });
            ModuleDefinition module = created.MainModule;

            TypeReference objectType = module.ImportReference(mscorlib.MainModule.GetType("System.Object"));
            TypeReference stringType = module.ImportReference(mscorlib.MainModule.GetType("System.String"));
            TypeReference voidType = module.ImportReference(mscorlib.MainModule.GetType("System.Void"));
            TypeDefinition attribute = mscorlib.MainModule.GetType("System.Attribute");
            TypeReference attributeType = module.ImportReference(attribute);
            MethodReference attributeConstructor = module.ImportReference(attribute.Methods.First(m => m.IsConstructor && !m.HasParameters));

            var excludeType = new TypeDefinition(
                "System.Runtime.CompilerServices",
                "ExcludeTypeAttribute",
                Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Sealed | Mono.Cecil.TypeAttributes.Class,
                attributeType);
            MethodDefinition excludeTypeConstructor = Constructor(attributeConstructor, voidType);
            excludeType.Methods.Add(excludeTypeConstructor);
            module.Types.Add(excludeType);

            var textAttribute = new TypeDefinition(
                "Fixture",
                "TextAttribute",
                Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Class,
                attributeType);
            MethodDefinition textConstructor = Constructor(attributeConstructor, voidType, stringType);
            textAttribute.Methods.Add(textConstructor);
            module.Types.Add(textAttribute);

            var outer = new TypeDefinition("Fixture", "Outer", Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Class, objectType);
            outer.Methods.Add(ReturnLiteral("Kept", KeptLiteral, stringType));
            var text = new CustomAttribute(textConstructor);
            text.ConstructorArguments.Add(new CustomAttributeArgument(stringType, attributeText));
            outer.CustomAttributes.Add(text);
            module.Types.Add(outer);

            var nested = new TypeDefinition("", "Nested", Mono.Cecil.TypeAttributes.NestedPublic | Mono.Cecil.TypeAttributes.Class, objectType);
            nested.CustomAttributes.Add(new CustomAttribute(excludeTypeConstructor));
            nested.Methods.Add(ReturnLiteral("Excluded", ExcludedLiteral, stringType));
            outer.NestedTypes.Add(nested);

            // not disposed: Cecil reads the method bodies from the stream when they are first used
            var stream = new MemoryStream();
            created.Write(stream);
            stream.Position = 0;

            return AssemblyDefinition.ReadAssembly(stream, new ReaderParameters { AssemblyResolver = resolver, ReadingMode = ReadingMode.Immediate });
        }

        private static MethodDefinition Constructor(MethodReference baseConstructor, TypeReference voidType, params TypeReference[] parameters)
        {
            var constructor = new MethodDefinition(
                ".ctor",
                Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig | Mono.Cecil.MethodAttributes.SpecialName | Mono.Cecil.MethodAttributes.RTSpecialName,
                voidType);

            foreach (TypeReference parameter in parameters)
            {
                constructor.Parameters.Add(new ParameterDefinition(parameter));
            }

            ILProcessor il = constructor.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, baseConstructor);
            il.Emit(OpCodes.Ret);

            return constructor;
        }

        private static MethodDefinition ReturnLiteral(string name, string literal, TypeReference stringType)
        {
            var method = new MethodDefinition(
                name,
                Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.HideBySig,
                stringType);

            ILProcessor il = method.Body.GetILProcessor();
            il.Emit(OpCodes.Ldstr, literal);
            il.Emit(OpCodes.Ret);

            return method;
        }
    }
}
