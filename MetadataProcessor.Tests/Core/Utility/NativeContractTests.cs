// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Utility
{
    [TestClass]
    public class NativeContractTests
    {
        private const string StubsAppNamespace = "StubsGenerationTestNFApp";

        // must match CLR_RECORD_METHODDEF::MD_Native in nf-interpreter (nanoCLR_Types.h)
        private const uint MD_Native = 0x00008000;

        // used to assign unique tokens to members added to the assemblies under test
        private static int s_fakeRid = 0x00F000;

        #region safe names

        [TestMethod]
        public void GetClassNameTest()
        {
            var nanoTablesContext = TestObjectHelper.GetTestNFAppNanoTablesContext();
            var typeDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllTypeDefinition(nanoTablesContext.AssemblyDefinition);

            // test
            var r = NativeContract.GetSafeClassName(typeDefinition);

            Assert.AreEqual("TestNFApp_OneClassOverAll", r);

            // test
            r = NativeContract.GetSafeClassName(null);

            Assert.AreEqual(String.Empty, r);
        }

        [TestMethod]
        public void GetClassNameWithGenericsTest()
        {
            nanoTablesContext nanoTablesContext = TestObjectHelper.GetTestNFAppNanoTablesContext();
            TypeDefinition genericTypeDefinition = TestObjectHelper.GetTestNFAppGenericClassTypeDefinition(nanoTablesContext.AssemblyDefinition);
            TypeDefinition anotherGenericTypeDefinition = TestObjectHelper.GetTestNFAppAnotherGenericClassTypeDefinition(nanoTablesContext.AssemblyDefinition);

            // test
            Assert.AreEqual("TestNFApp_GenericClass_1", NativeContract.GetSafeClassName(genericTypeDefinition));
            Assert.AreEqual("TestNFApp_AnotherGenericClass_2", NativeContract.GetSafeClassName(anotherGenericTypeDefinition));

            Assert.AreEqual(String.Empty, NativeContract.GetSafeClassName(null));
        }

        [TestMethod]
        public void GetMethodNameTest()
        {
            var nanoTablesContext = TestObjectHelper.GetTestNFAppNanoTablesContext();
            var typeDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllTypeDefinition(nanoTablesContext.AssemblyDefinition);
            var methodDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllDummyMethodDefinition(typeDefinition);

            // test
            var r = NativeContract.GetSafeMethodName(methodDefinition);

            Assert.AreEqual("DummyMethod___VOID", r);

            methodDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllDummyMethodWithParamsDefinition(typeDefinition);

            // test
            r = NativeContract.GetSafeMethodName(methodDefinition);

            Assert.AreEqual("DummyMethodWithParams___VOID__I4__STRING", r);

            methodDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllDummyStaticMethodDefinition(typeDefinition);

            // test
            r = NativeContract.GetSafeMethodName(methodDefinition);

            Assert.AreEqual("DummyStaticMethod___STATIC__VOID", r);

            methodDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllDummyStaticMethodWithParamsDefinition(typeDefinition);

            // test
            r = NativeContract.GetSafeMethodName(methodDefinition);

            Assert.AreEqual("DummyStaticMethodWithParams___STATIC__VOID__I8__SystemDateTime", r);
        }

        [TestMethod]
        public void GetNanoCLRTypeNameTest()
        {
            var assemblyDefinition = TestObjectHelper.GetTestNFAppAssemblyDefinition();

            DoGetNanoCLRTypeNameTest(assemblyDefinition, typeof(void), "DATATYPE_VOID");
            DoGetNanoCLRTypeNameTest(assemblyDefinition, typeof(sbyte), "DATATYPE_I1");
            DoGetNanoCLRTypeNameTest(assemblyDefinition, typeof(short), "DATATYPE_I2");
            DoGetNanoCLRTypeNameTest(assemblyDefinition, typeof(int), "DATATYPE_I4");
            DoGetNanoCLRTypeNameTest(assemblyDefinition, typeof(long), "DATATYPE_I8");

            DoGetNanoCLRTypeNameTest(assemblyDefinition, typeof(byte), "DATATYPE_U1");
            DoGetNanoCLRTypeNameTest(assemblyDefinition, typeof(ushort), "DATATYPE_U2");
            DoGetNanoCLRTypeNameTest(assemblyDefinition, typeof(uint), "DATATYPE_U4");
            DoGetNanoCLRTypeNameTest(assemblyDefinition, typeof(ulong), "DATATYPE_U8");

            DoGetNanoCLRTypeNameTest(assemblyDefinition, typeof(float), "DATATYPE_R4");
            DoGetNanoCLRTypeNameTest(assemblyDefinition, typeof(double), "DATATYPE_R8");

            DoGetNanoCLRTypeNameTest(assemblyDefinition, typeof(char), "DATATYPE_CHAR");
            DoGetNanoCLRTypeNameTest(assemblyDefinition, typeof(string), "DATATYPE_STRING");
            DoGetNanoCLRTypeNameTest(assemblyDefinition, typeof(bool), "DATATYPE_BOOLEAN");

            DoGetNanoCLRTypeNameTest(assemblyDefinition, typeof(object), "DATATYPE_OBJECT");
            DoGetNanoCLRTypeNameTest(assemblyDefinition, typeof(IntPtr), "DATATYPE_I4");
            DoGetNanoCLRTypeNameTest(assemblyDefinition, typeof(UIntPtr), "DATATYPE_U4");

            DoGetNanoCLRTypeNameTest(assemblyDefinition, typeof(System.WeakReference), "DATATYPE_WEAKCLASS");

            DoGetNanoCLRTypeNameTest(assemblyDefinition, this.GetType(), "nanoFrameworkToolsMetadataProcessorTestsCoreUtilityNativeContractTests");
        }

        private void DoGetNanoCLRTypeNameTest(AssemblyDefinition assemblyDefinition, Type type, string expectedNanoCLRTypeName)
        {
            var typeReference = assemblyDefinition.MainModule.ImportReference(type);

            // test
            var r = NativeContract.GetNanoCLRTypeName(typeReference);

            Assert.AreEqual(expectedNanoCLRTypeName, r);
        }

        #endregion

        #region native slots and method definitions

        [TestMethod]
        public void NativeSlotsAreCanonicallySortedTest()
        {
            nanoTablesContext context = BuildContext(LoadStubsApp());
            NativeContract contract = context.NativeContract;

            // 5 in NativeMethodGeneration, 4 in NativeMethodGenerationGenerics, 2 in NativeWithFields, 1 in NativeException
            Assert.AreEqual(12, contract.Methods.Count);

            List<NativeMethodSlot> expectedOrder = contract.Methods
                .OrderBy(m => m.SafeClassName, StringComparer.Ordinal)
                .ThenBy(m => m.SafeMethodName, StringComparer.Ordinal)
                .ToList();

            for (int i = 0; i < contract.Methods.Count; i++)
            {
                Assert.AreEqual(i, (int)contract.Methods[i].Slot, "Slot must match position in the list");
                Assert.AreSame(expectedOrder[i], contract.Methods[i], "Slots must be sorted by (safe class name, safe method name)");

                Assert.IsTrue(contract.TryGetSlot(contract.Methods[i].Method, out ushort slot));
                Assert.AreEqual(i, (int)slot);
            }

            // every native method has a slot, every other method doesn't
            foreach (TypeDefinition t in context.TypeDefinitionTable.Items)
            {
                foreach (MethodDefinition m in t.Methods)
                {
                    bool expectedNative = NativeContract.IsStubIncludedType(t) && !m.HasBody && !m.IsAbstract;

                    Assert.AreEqual(expectedNative, contract.TryGetSlot(m, out _), $"Unexpected native slot state for {m.FullName}");
                }
            }
        }

        [TestMethod]
        public void NativeSlotsAndHashIndependentOfTypeOrderTest()
        {
            nanoTablesContext context = BuildContext(LoadStubsApp());

            // reverse the type order
            AssemblyDefinition reversedAssembly = LoadStubsApp();
            List<string> reversedOrder = context.TypeDefinitionTable.Items
                .Select(t => t.FullName)
                .Reverse()
                .ToList();

            nanoTablesContext reversedContext = new nanoTablesContext(
                reversedAssembly,
                reversedOrder,
                null,
                false,
                false,
                false);

            // sanity check: order is really different
            CollectionAssert.AreNotEqual(
                context.TypeDefinitionTable.Items.Select(t => t.FullName).ToList(),
                reversedContext.TypeDefinitionTable.Items.Select(t => t.FullName).ToList());

            CollectionAssert.AreEqual(
                context.NativeContract.Methods.Select(m => $"{m.Slot}:{m.SafeClassName}::{m.SafeMethodName}").ToList(),
                reversedContext.NativeContract.Methods.Select(m => $"{m.Slot}:{m.SafeClassName}::{m.SafeMethodName}").ToList());
        }

        [TestMethod]
        public void NativeMethodDefinitionHasNativeFlagAndSlotAsRvaTest()
        {
            nanoAssemblyBuilder builder = CompileAndMinimize(LoadStubsApp());
            nanoTablesContext context = builder.TablesContext;

            int nativeCount = 0;

            foreach (MethodDefinition m in context.MethodDefinitionTable.Items)
            {
                uint flags = nanoMethodDefinitionTable.GetFlags(m, context);
                ushort rva = nanoMethodDefinitionTable.GetRva(context, m);

                if (context.NativeContract.TryGetSlot(m, out ushort slot))
                {
                    nativeCount++;

                    Assert.AreEqual(slot, rva, $"RVA of native method {m.FullName} must be the native slot");
                    Assert.AreEqual(MD_Native, flags & MD_Native, $"MD_Native flag missing for {m.FullName}");
                }
                else
                {
                    Assert.AreEqual(0u, flags & MD_Native, $"MD_Native flag set for {m.FullName}");
                    Assert.AreEqual(context.ByteCodeTable.GetMethodRva(m), rva);

                    if (!m.HasBody)
                    {
                        Assert.AreEqual((ushort)0xFFFF, rva);
                    }
                }

                // flags without context never include MD_Native
                Assert.AreEqual(0u, nanoMethodDefinitionTable.GetFlags(m) & MD_Native);
            }

            Assert.AreEqual(context.NativeContract.Methods.Count, nativeCount);
        }

        [TestMethod]
        public void NonNativeBodilessMethodsUntouchedTest()
        {
            nanoTablesContext context = TestObjectHelper.GetTestNFAppNanoTablesContext();

            int checkedMethods = 0;

            foreach (TypeDefinition t in context.TypeDefinitionTable.Items)
            {
                foreach (MethodDefinition m in t.Methods.Where(m => !m.HasBody))
                {
                    // abstract methods (interfaces, abstract classes) and delegate runtime methods
                    if (m.IsAbstract || !NativeContract.IsStubIncludedType(t))
                    {
                        checkedMethods++;

                        Assert.IsFalse(context.NativeContract.TryGetSlot(m, out _), $"{m.FullName} must not have a native slot");
                        Assert.AreEqual((ushort)0xFFFF, nanoMethodDefinitionTable.GetRva(context, m));
                        Assert.AreEqual(0u, nanoMethodDefinitionTable.GetFlags(m, context) & MD_Native);
                    }
                }
            }

            Assert.IsTrue(checkedMethods > 0, "Expecting abstract and delegate methods in TestNFApp");
        }

        [TestMethod]
        public void DuplicateNativeMethodNameThrowsTest()
        {
            AssemblyDefinition assembly = LoadStubsApp();
            TypeDefinition type = GetStubsAppType(assembly, "NativeWithFields");
            TypeDefinition genericType = GetStubsAppType(assembly, "NativeMethodGenerationGenerics`1");
            ModuleDefinition module = assembly.MainModule;

            // two overloads that differ only by generic instantiation map to the same native name
            var intInstance = new GenericInstanceType(genericType);
            intInstance.GenericArguments.Add(module.TypeSystem.Int32);

            var stringInstance = new GenericInstanceType(genericType);
            stringInstance.GenericArguments.Add(module.TypeSystem.String);

            MethodDefinition first = AddNativeMethod(type, "NativeDuplicate", module.TypeSystem.Void);
            first.Parameters.Add(new ParameterDefinition("p", ParameterAttributes.None, intInstance));

            MethodDefinition second = AddNativeMethod(type, "NativeDuplicate", module.TypeSystem.Void);
            second.Parameters.Add(new ParameterDefinition("p", ParameterAttributes.None, stringInstance));

            Assert.AreEqual(NativeContract.GetSafeMethodName(first), NativeContract.GetSafeMethodName(second));

            Assert.ThrowsException<ArgumentException>(() => BuildContext(assembly));
        }

        #endregion

        #region contract types and field layouts

        [TestMethod]
        public void ContractTypesTest()
        {
            AssemblyDefinition assembly = LoadStubsApp();
            nanoTablesContext context = BuildContext(assembly);

            List<string> contractTypes = context.NativeContract.Types.Select(t => t.Type.FullName).ToList();

            // types with native methods
            CollectionAssert.Contains(contractTypes, $"{StubsAppNamespace}.NativeMethodGeneration");
            CollectionAssert.Contains(contractTypes, $"{StubsAppNamespace}.NativeMethodGenerationGenerics`1");
            CollectionAssert.Contains(contractTypes, $"{StubsAppNamespace}.NativeWithFields");
            CollectionAssert.Contains(contractTypes, $"{StubsAppNamespace}.NativeException");

            // managed-only types are part of the contract too
            CollectionAssert.Contains(contractTypes, $"{StubsAppNamespace}.ManagedOnlyWithFields");
            CollectionAssert.Contains(contractTypes, $"{StubsAppNamespace}.CompilerGeneratedTypesHost");
            CollectionAssert.Contains(contractTypes, $"{StubsAppNamespace}.Program");

            // delegates aren't
            CollectionAssert.DoesNotContain(contractTypes, $"{StubsAppNamespace}.IntProvider");

            // compiler-generated types (closure display classes) exist but aren't part of the contract
            List<TypeDefinition> compilerGenerated = context.TypeDefinitionTable.Items
                .Where(t => t.DeclaringType?.Name == "CompilerGeneratedTypesHost")
                .ToList();

            Assert.IsTrue(compilerGenerated.Count >= 2, "Expecting closure compiler-generated types");

            foreach (TypeDefinition t in compilerGenerated)
            {
                Assert.IsTrue(NativeContract.IsCompilerGenerated(t), $"{t.FullName} should be detected as compiler-generated");
                Assert.IsTrue(t.HasFields, $"{t.FullName} is expected to have fields");
                CollectionAssert.DoesNotContain(contractTypes, t.FullName);
            }

            Assert.IsFalse(context.NativeContract.Types.Any(t => NativeContract.IsCompilerGenerated(t.Type)));

            // field constants
            NativeTypeLayout withFields = context.NativeContract.Types.Single(t => t.Type.Name == "NativeWithFields");
            Assert.AreEqual(1, withFields.StaticFields.Count);
            Assert.AreEqual("s_counter", withFields.StaticFields[0].Name);
            Assert.AreEqual("I4", withFields.StaticFields[0].TypeName);
            CollectionAssert.AreEqual(new[] { "_value", "_flag" }, withFields.InstanceFields.Select(f => f.Name).ToList());
            CollectionAssert.AreEqual(new[] { 1, 2 }, withFields.InstanceFields.Select(f => f.Index).ToList());
            CollectionAssert.AreEqual(new[] { "I4", "U1" }, withFields.InstanceFields.Select(f => f.TypeName).ToList());

            NativeTypeLayout managedOnly = context.NativeContract.Types.Single(t => t.Type.Name == "ManagedOnlyWithFields");
            Assert.IsFalse(managedOnly.HasNativeMethods);
            CollectionAssert.AreEqual(new[] { "_managedValue", "_managedName" }, managedOnly.InstanceFields.Select(f => f.Name).ToList());
            CollectionAssert.AreEqual(new[] { "I4", "STRING" }, managedOnly.InstanceFields.Select(f => f.TypeName).ToList());

            // cross-assembly base: own field index comes after the inherited ones
            TypeDefinition exceptionType = GetStubsAppType(assembly, "NativeException").BaseType.Resolve();
            int exceptionInstanceFields = CountInstanceFieldsInHierarchy(exceptionType);
            Assert.IsTrue(exceptionInstanceFields > 0, "Test requires System.Exception to have instance fields");

            NativeTypeLayout nativeException = context.NativeContract.Types.Single(t => t.Type.Name == "NativeException");
            Assert.AreEqual(exceptionInstanceFields + 1, nativeException.InstanceFields.Single().Index);
        }

        [TestMethod]
        public void CompilerGeneratedDetectionTest()
        {
            AssemblyDefinition assembly = LoadStubsApp();

            Assert.IsFalse(NativeContract.IsCompilerGenerated(GetStubsAppType(assembly, "ManagedOnlyWithFields")));

            // detection is by attribute only, a synthesized-looking name alone doesn't count
            TypeDefinition nameOnly = AddCompilerGeneratedType(GetStubsAppType(assembly, "ManagedOnlyWithFields"), "<>c__DisplayClass42_0", null);
            Assert.IsFalse(NativeContract.IsCompilerGenerated(nameOnly));

            // inline array types synthesized for collection expressions carry InlineArrayAttribute instead
            TypeDefinition inlineArray = AddCompilerGeneratedType(GetStubsAppType(assembly, "ManagedOnlyWithFields"), "<>y__InlineArray3", InlineArrayAttributeName);
            Assert.IsTrue(NativeContract.IsCompilerGenerated(inlineArray));

            // by attribute (and nested in compiler-generated type)
            TypeDefinition byAttribute = AddCompilerGeneratedType(GetStubsAppType(assembly, "ManagedOnlyWithFields"), "PlainName", CompilerGeneratedAttributeName);
            Assert.IsTrue(NativeContract.IsCompilerGenerated(byAttribute));

            var nested = new TypeDefinition(string.Empty, "Inner", TypeAttributes.NestedPrivate | TypeAttributes.Class, assembly.MainModule.TypeSystem.Object);
            byAttribute.NestedTypes.Add(nested);
            Assert.IsTrue(NativeContract.IsCompilerGenerated(nested));
        }

        #endregion

        #region field names

        [TestMethod]
        // valid identifiers are used as is
        [DataRow("_value", "_value", null)]
        [DataRow("s_counter", "s_counter", null)]
        [DataRow("Ünïcode", "Ünïcode", null)]
        // auto-property backing fields
        [DataRow("<Name>k__BackingField", "Name", "backing field")]
        [DataRow("<class>k__BackingField", "class", "backing field")]
        // explicit interface implementations: namespace dropped
        [DataRow("<Probe.IFoo.Bar>k__BackingField", "IFoo_Bar", "backing field")]
        [DataRow("<IFoo.Bar>k__BackingField", "IFoo_Bar", "backing field")]
        [DataRow("<Probe.Outer.IInner.Bar>k__BackingField", "IInner_Bar", "backing field")]
        [DataRow("<Probe.IGen<System.Int32>.Item>k__BackingField", "IGen_of_Int32_Item", "backing field")]
        [DataRow("<Probe.IDict<System.String,System.Int32>.Item>k__BackingField", "IDict_of_String_Int32_Item", "backing field")]
        [DataRow("<Probe.IDict<System.String, System.Int32>.Item>k__BackingField", "IDict_of_String_Int32_Item", "backing field")]
        [DataRow("<Probe.IGen<System.Collections.Generic.List<System.Int32>>.Item>k__BackingField", "IGen_of_List_of_Int32_Item", "backing field")]
        [DataRow("<Probe.IGen<Probe.Outer.Inner>.Item>k__BackingField", "IGen_of_Inner_Item", "backing field")]
        [DataRow("<Probe.IGen`1.Item>k__BackingField", "IGen_1_Item", "backing field")]
        // primary constructor captured parameters
        [DataRow("<width>P", "width", "primary constructor parameter field")]
        // fallback
        [DataRow("<>4__this", "4__this", "field")]
        [DataRow("weird-name", "weird_name", "field")]
        [DataRow("a.b", "a_b", "field")]
        [DataRow("List<Int32>", "List_of_Int32", "field")]
        [DataRow("trailing!", "trailing", "field")]
        [DataRow("many$$$chars", "many_chars", "field")]
        public void SanitizeFieldNameTest(string name, string expected, string expectedRenameKind)
        {
            string result = NativeContract.SanitizeFieldName(name, out string renameKind);

            Assert.AreEqual(expected, result);
            Assert.AreEqual(expectedRenameKind, renameKind);
            Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(result, @"^[\p{L}\p{Nd}_]+$"), $"'{result}' isn't a valid identifier");
        }

        [TestMethod]
        public void SanitizeFieldNameThrowsWhenNoIdentifierPossibleTest()
        {
            Assert.ThrowsException<ArgumentException>(() => NativeContract.SanitizeFieldName("<>", out _));
            Assert.ThrowsException<ArgumentException>(() => NativeContract.SanitizeFieldName("$$$", out _));
        }

        [TestMethod]
        public void FixFieldNameCommentTest()
        {
            AssemblyDefinition assembly = LoadStubsApp();
            TypeDefinition type = GetStubsAppType(assembly, "PrimaryConstructorHolder");
            FieldDefinition field = type.Fields.Single();

            Assert.AreEqual("<width>P", field.Name);

            NativeContract.FixFieldName(field, out string fixedName, out string warning);

            Assert.AreEqual("width", fixedName);
            Assert.AreEqual("// renamed primary constructor parameter field '<width>P'", warning);

            // not renamed
            NativeContract.FixFieldName(GetStubsAppType(assembly, "NativeWithFields").Fields.Single(f => f.Name == "_value"), out fixedName, out warning);

            Assert.AreEqual(string.Empty, fixedName);
            Assert.AreEqual(string.Empty, warning);
        }

        [TestMethod]
        public void FieldNamesFromRoslynTest()
        {
            nanoTablesContext context = BuildContext(LoadStubsApp());

            NativeTypeLayout explicitProperties = context.NativeContract.Types.Single(t => t.Type.Name == "ExplicitInterfaceProperties");
            CollectionAssert.AreEqual(
                new[] { "IFoo_Bar", "IGen_of_Int32_Item", "IDict_of_String_Int32_Item", "class" },
                explicitProperties.InstanceFields.Select(f => f.Name).ToList());

            NativeTypeLayout primaryConstructor = context.NativeContract.Types.Single(t => t.Type.Name == "PrimaryConstructorHolder");
            CollectionAssert.AreEqual(new[] { "width" }, primaryConstructor.InstanceFields.Select(f => f.Name).ToList());

            // overloaded static method group conversions: delegate cache container <>O is compiler-generated and excluded
            TypeDefinition cacheContainer = context.TypeDefinitionTable.Items.Single(t => t.DeclaringType?.Name == "MethodGroupConversions");
            Assert.AreEqual("<>O", cacheContainer.Name);
            CollectionAssert.AreEquivalent(new[] { "<0>__SWork", "<1>__SWork" }, cacheContainer.Fields.Select(f => f.Name).ToList());
            Assert.IsTrue(NativeContract.IsCompilerGenerated(cacheContainer));
            Assert.IsFalse(context.NativeContract.Types.Any(t => t.Type == cacheContainer));

            // every constant is a valid identifier
            foreach (NativeTypeLayout type in context.NativeContract.Types)
            {
                foreach (NativeFieldConstant f in type.StaticFields.Concat(type.InstanceFields))
                {
                    Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(f.Name, @"^[\p{L}\p{Nd}_]+$"), $"'{f.Name}' of {type.Type.FullName} isn't a valid identifier");
                }
            }
        }

        [TestMethod]
        public void DuplicateFieldConstantThrowsTest()
        {
            AssemblyDefinition assembly = LoadStubsApp();
            TypeDefinition type = GetStubsAppType(assembly, "PrimaryConstructorHolder");

            // '<width>P' is renamed to 'width', which clashes with this one
            FieldDefinition clash = new FieldDefinition("width", FieldAttributes.Private, assembly.MainModule.TypeSystem.Int32);
            SetFakeToken(clash, TokenType.Field);
            type.Fields.Add(clash);

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => BuildContext(assembly));

            StringAssert.Contains(exception.Message, "StubsGenerationTestNFApp.PrimaryConstructorHolder");
            StringAssert.Contains(exception.Message, "'<width>P'");
            StringAssert.Contains(exception.Message, "'width'");
            StringAssert.Contains(exception.Message, "FIELD__width");
        }

        [TestMethod]
        public void DuplicateStaticFieldConstantThrowsTest()
        {
            AssemblyDefinition assembly = LoadStubsApp();
            TypeDefinition type = GetStubsAppType(assembly, "NativeWithFields");

            FieldDefinition first = new FieldDefinition("a.b", FieldAttributes.Private | FieldAttributes.Static, assembly.MainModule.TypeSystem.Int32);
            SetFakeToken(first, TokenType.Field);
            type.Fields.Add(first);

            FieldDefinition second = new FieldDefinition("a_b", FieldAttributes.Private | FieldAttributes.Static, assembly.MainModule.TypeSystem.Int32);
            SetFakeToken(second, TokenType.Field);
            type.Fields.Add(second);

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => BuildContext(assembly));

            StringAssert.Contains(exception.Message, "FIELD_STATIC__a_b");
        }

        [TestMethod]
        public void SameNameStaticAndInstanceDontClashTest()
        {
            AssemblyDefinition assembly = LoadStubsApp();
            TypeDefinition type = GetStubsAppType(assembly, "PrimaryConstructorHolder");

            // FIELD_STATIC__width and FIELD__width are different constants
            FieldDefinition staticField = new FieldDefinition("width", FieldAttributes.Private | FieldAttributes.Static, assembly.MainModule.TypeSystem.Int32);
            SetFakeToken(staticField, TokenType.Field);
            type.Fields.Add(staticField);

            nanoTablesContext context = BuildContext(assembly);

            NativeTypeLayout layout = context.NativeContract.Types.Single(t => t.Type.Name == "PrimaryConstructorHolder");
            Assert.AreEqual("width", layout.StaticFields.Single().Name);
            Assert.AreEqual("width", layout.InstanceFields.Single().Name);
        }

        #endregion

        #region contract hash

        [TestMethod]
        public void HashIsZeroWhenNoNativeMethodsTest()
        {
            AssemblyDefinition assembly = LoadStubsApp();
            var removed = new HashSet<string>(StringComparer.Ordinal);

            foreach (TypeDefinition t in assembly.MainModule.GetTypes())
            {
                foreach (MethodDefinition m in t.Methods.Where(m => !m.HasBody && !m.IsAbstract && !IsDelegate(t)).ToList())
                {
                    removed.Add(m.Name);
                    t.Methods.Remove(m);
                }
            }

            // strip the calls to the removed methods (by name, over-stripping is harmless; only void methods in the test app),
            // so the assembly can be minimized
            foreach (MethodDefinition m in assembly.MainModule.GetTypes().SelectMany(t => t.Methods).Where(m => m.HasBody).ToList())
            {
                if (m.Body.Instructions.Any(i => i.Operand is MethodReference mr && removed.Contains(mr.Name)))
                {
                    Assert.AreEqual("System.Void", m.ReturnType.FullName);

                    m.Body = new MethodBody(m);
                    m.Body.GetILProcessor().Emit(OpCodes.Ret);
                }
            }

            // types with fields remain, but fields alone don't make a native contract
            Assert.IsTrue(GetStubsAppType(assembly, "ManagedOnlyWithFields").HasFields);

            nanoAssemblyBuilder builder = CompileAndMinimize(assembly);
            NativeContract contract = builder.TablesContext.NativeContract;

            Assert.IsTrue(contract.IsEmpty);
            Assert.AreEqual(0u, contract.Hash);
            Assert.AreEqual(0, contract.Types.Count);

            // PE header gets 0
            Assert.AreEqual(0u, builder.LastNativeMethodsChecksum);
            Assert.AreEqual("0x00000000", builder.GetNativeContractHash());

            // and no stubs are generated
            string stubsPath = Path.Combine(TestObjectHelper.TestExecutionLocation, "NoNativeStubs");

            if (Directory.Exists(stubsPath))
            {
                Directory.Delete(stubsPath, true);
            }

            new nanoFramework.Tools.MetadataProcessor.Core.nanoSkeletonGenerator(
                builder.TablesContext,
                stubsPath,
                "noNative",
                "NoNative",
                false,
                false).GenerateSkeleton();

            Assert.IsFalse(Directory.Exists(stubsPath) && Directory.EnumerateFileSystemEntries(stubsPath).Any(), "No stubs should be generated");
        }

        [TestMethod]
        public void HashIsNotZeroWithNativeMethodsTest()
        {
            nanoTablesContext context = BuildContext(LoadStubsApp());

            Assert.IsFalse(context.NativeContract.IsEmpty);
            Assert.AreNotEqual(0u, context.NativeContract.Hash);
        }

        [TestMethod]
        public void HashIsStableAcrossMinimizeTest()
        {
            uint hashBefore = BuildContext(LoadStubsApp()).NativeContract.Hash;

            nanoAssemblyBuilder builder = CompileAndMinimize(LoadStubsApp());

            Assert.AreEqual(hashBefore, builder.TablesContext.NativeContract.Hash);
            Assert.AreEqual($"0x{hashBefore:X8}", builder.GetNativeContractHash());
            Assert.AreEqual(hashBefore, builder.LastNativeMethodsChecksum);
        }

        [TestMethod]
        public void HashUnchangedWhenAddingManagedOnlyMethodTest()
        {
            uint baseline = GetBaselineHash();

            AssemblyDefinition assembly = LoadStubsApp();

            // add managed methods to a type with native methods and to a managed-only type
            AddManagedMethod(GetStubsAppType(assembly, "NativeWithFields"), "ManagedOnlyAdded");
            AddManagedMethod(GetStubsAppType(assembly, "ManagedOnlyWithFields"), "ManagedOnlyAdded");

            Assert.AreEqual(baseline, BuildContext(assembly).NativeContract.Hash);
        }

        [TestMethod]
        public void HashUnchangedWhenAddingManagedOnlyClassWithoutFieldsTest()
        {
            uint baseline = GetBaselineHash();

            AssemblyDefinition assembly = LoadStubsApp();
            ModuleDefinition module = assembly.MainModule;

            // alphabetically first, so it is placed before all other types
            var newType = new TypeDefinition(
                StubsAppNamespace,
                "AaaManagedOnlyAdded",
                TypeAttributes.Class | TypeAttributes.NotPublic | TypeAttributes.BeforeFieldInit,
                module.TypeSystem.Object);
            SetFakeToken(newType, TokenType.TypeDef);

            module.Types.Add(newType);
            AddManagedMethod(newType, "ManagedMethod");

            nanoTablesContext context = BuildContext(assembly);

            // sanity check: the new type is there
            Assert.IsTrue(context.TypeDefinitionTable.Items.Any(t => t.Name == "AaaManagedOnlyAdded"));

            Assert.AreEqual(baseline, context.NativeContract.Hash);
        }

        [TestMethod]
        public void HashUnchangedWhenAddingCompilerGeneratedTypesTest()
        {
            uint baseline = GetBaselineHash();

            AssemblyDefinition assembly = LoadStubsApp();

            // simulate adding a closure (display class), an iterator (state machine) and a collection expression
            // inline array, all with instance fields, to a managed-only type and to a type with native methods
            AddCompilerGeneratedType(GetStubsAppType(assembly, "ManagedOnlyWithFields"), "<>c__DisplayClass7_0", CompilerGeneratedAttributeName);
            AddCompilerGeneratedType(GetStubsAppType(assembly, "NativeWithFields"), "<GetValues>d__3", CompilerGeneratedAttributeName);
            AddCompilerGeneratedType(GetStubsAppType(assembly, "Program"), "<>y__InlineArray2", InlineArrayAttributeName);

            nanoTablesContext context = BuildContext(assembly);

            // sanity check: the new types are there
            Assert.IsTrue(context.TypeDefinitionTable.Items.Any(t => t.Name == "<>c__DisplayClass7_0"));
            Assert.IsTrue(context.TypeDefinitionTable.Items.Any(t => t.Name == "<GetValues>d__3"));
            Assert.IsTrue(context.TypeDefinitionTable.Items.Any(t => t.Name == "<>y__InlineArray2"));

            Assert.AreEqual(baseline, context.NativeContract.Hash);
        }

        [TestMethod]
        public void HashUnchangedWhenMethodBodyChangesTypeOrderTest()
        {
            // variant A: helper method added (managed only)
            AssemblyDefinition assemblyA = LoadStubsApp();
            AddTouchMethod(assemblyA);

            nanoTablesContext contextA = BuildContext(assemblyA);

            // variant B: same + a call to the helper in a method body, which pulls NativeWithFields
            // before NativeMethodGeneration in the type order
            AssemblyDefinition assemblyB = LoadStubsApp();
            MethodDefinition touch = AddTouchMethod(assemblyB);

            MethodDefinition method = GetStubsAppType(assemblyB, "NativeMethodGeneration").Methods.Single(m => m.Name == "Method");
            ILProcessor il = method.Body.GetILProcessor();
            Instruction first = method.Body.Instructions.First();
            il.InsertBefore(first, il.Create(OpCodes.Ldnull));
            il.InsertBefore(first, il.Create(OpCodes.Call, touch));

            nanoTablesContext contextB = BuildContext(assemblyB);

            // sanity check: the type order is different
            List<string> orderA = contextA.TypeDefinitionTable.Items.Select(t => t.Name).ToList();
            List<string> orderB = contextB.TypeDefinitionTable.Items.Select(t => t.Name).ToList();

            Assert.IsTrue(orderA.IndexOf("NativeMethodGeneration") < orderA.IndexOf("NativeWithFields"));
            Assert.IsTrue(orderB.IndexOf("NativeMethodGeneration") > orderB.IndexOf("NativeWithFields"));

            Assert.AreEqual(contextA.NativeContract.Hash, contextB.NativeContract.Hash);

            // and the hash is the same as the unmodified assembly
            Assert.AreEqual(GetBaselineHash(), contextA.NativeContract.Hash);
        }

        [TestMethod]
        public void HashChangesWhenNativeMethodSignatureChangesTest()
        {
            uint baseline = GetBaselineHash();

            AssemblyDefinition assembly = LoadStubsApp();
            MethodDefinition method = GetStubsAppType(assembly, "NativeMethodGeneration").Methods.Single(m => m.Name == "NativeStaticMethodReturningByte");

            // char -> int
            method.Parameters[0].ParameterType = assembly.MainModule.TypeSystem.Int32;

            Assert.AreNotEqual(baseline, BuildContext(assembly).NativeContract.Hash);
        }

        [TestMethod]
        public void HashChangesWhenNativeMethodAddedTest()
        {
            uint baseline = GetBaselineHash();

            AssemblyDefinition assembly = LoadStubsApp();
            AddNativeMethod(GetStubsAppType(assembly, "NativeWithFields"), "NativeAdded", assembly.MainModule.TypeSystem.Void);

            nanoTablesContext context = BuildContext(assembly);

            Assert.AreEqual(13, context.NativeContract.Methods.Count);
            Assert.AreNotEqual(baseline, context.NativeContract.Hash);
        }

        [TestMethod]
        public void HashChangesWhenNativeMethodRemovedTest()
        {
            uint baseline = GetBaselineHash();

            AssemblyDefinition assembly = LoadStubsApp();
            TypeDefinition type = GetStubsAppType(assembly, "NativeMethodGeneration");
            type.Methods.Remove(type.Methods.Single(m => m.Name == "MethodWithPointerParm"));

            nanoTablesContext context = BuildContext(assembly);

            Assert.AreEqual(11, context.NativeContract.Methods.Count);
            Assert.AreNotEqual(baseline, context.NativeContract.Hash);
        }

        [TestMethod]
        public void HashChangesWhenFieldAddedToNativeTypeTest()
        {
            uint baseline = GetBaselineHash();

            AssemblyDefinition assembly = LoadStubsApp();
            TypeDefinition type = GetStubsAppType(assembly, "NativeWithFields");

            FieldDefinition field = new FieldDefinition("_added", FieldAttributes.Private, assembly.MainModule.TypeSystem.Int32);
            SetFakeToken(field, TokenType.Field);
            type.Fields.Add(field);

            Assert.AreNotEqual(baseline, BuildContext(assembly).NativeContract.Hash);
        }

        [TestMethod]
        public void HashChangesWhenFieldsReorderedInNativeTypeTest()
        {
            uint baseline = GetBaselineHash();

            AssemblyDefinition assembly = LoadStubsApp();
            TypeDefinition type = GetStubsAppType(assembly, "NativeWithFields");

            FieldDefinition flag = type.Fields.Single(f => f.Name == "_flag");
            type.Fields.Remove(flag);
            type.Fields.Insert(type.Fields.IndexOf(type.Fields.Single(f => f.Name == "_value")), flag);

            Assert.AreNotEqual(baseline, BuildContext(assembly).NativeContract.Hash);
        }

        [TestMethod]
        public void HashChangesWhenFieldAddedToManagedOnlyTypeTest()
        {
            uint baseline = GetBaselineHash();

            AssemblyDefinition assembly = LoadStubsApp();
            TypeDefinition type = GetStubsAppType(assembly, "ManagedOnlyWithFields");

            FieldDefinition field = new FieldDefinition("_anotherManagedValue", FieldAttributes.Private, assembly.MainModule.TypeSystem.Int32);
            SetFakeToken(field, TokenType.Field);
            type.Fields.Add(field);

            Assert.AreNotEqual(baseline, BuildContext(assembly).NativeContract.Hash);
        }

        [TestMethod]
        public void HashChangesWhenFieldRemovedFromManagedOnlyTypeTest()
        {
            uint baseline = GetBaselineHash();

            AssemblyDefinition assembly = LoadStubsApp();
            TypeDefinition type = GetStubsAppType(assembly, "ManagedOnlyWithFields");

            // remove the last field so no other index changes: only the removal itself is detected
            type.Fields.Remove(type.Fields.Single(f => f.Name == "_managedName"));

            Assert.AreNotEqual(baseline, BuildContext(assembly).NativeContract.Hash);
        }

        [TestMethod]
        public void HashChangesWhenFieldsReorderedInManagedOnlyTypeTest()
        {
            uint baseline = GetBaselineHash();

            AssemblyDefinition assembly = LoadStubsApp();
            TypeDefinition type = GetStubsAppType(assembly, "ManagedOnlyWithFields");

            FieldDefinition name = type.Fields.Single(f => f.Name == "_managedName");
            type.Fields.Remove(name);
            type.Fields.Insert(0, name);

            Assert.AreNotEqual(baseline, BuildContext(assembly).NativeContract.Hash);
        }

        [TestMethod]
        public void HashChangesWhenFieldRenamedInManagedOnlyTypeTest()
        {
            uint baseline = GetBaselineHash();

            AssemblyDefinition assembly = LoadStubsApp();
            TypeDefinition type = GetStubsAppType(assembly, "ManagedOnlyWithFields");

            type.Fields.Single(f => f.Name == "_managedName").Name = "_renamedName";

            Assert.AreNotEqual(baseline, BuildContext(assembly).NativeContract.Hash);
        }

        [TestMethod]
        public void HashChangesWhenFieldRetypedInManagedOnlyTypeTest()
        {
            uint baseline = GetBaselineHash();

            AssemblyDefinition assembly = LoadStubsApp();
            TypeDefinition type = GetStubsAppType(assembly, "ManagedOnlyWithFields");

            // same name and index, different type
            type.Fields.Single(f => f.Name == "_managedValue").FieldType = assembly.MainModule.TypeSystem.Int64;

            Assert.AreNotEqual(baseline, BuildContext(assembly).NativeContract.Hash);
        }

        #endregion

        #region helpers

        private static uint GetBaselineHash()
        {
            return BuildContext(LoadStubsApp()).NativeContract.Hash;
        }

        internal static AssemblyDefinition LoadStubsApp()
        {
            string path = TestObjectHelper.StubsGenerationTestNFAppFullPath;

            var loadHints = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["mscorlib"] = Path.Combine(Directory.GetParent(path).FullName, "mscorlib.dll")
            };

            return AssemblyDefinition.ReadAssembly(
                path,
                new ReaderParameters { AssemblyResolver = new LoadHintsAssemblyResolver(loadHints) });
        }

        private static nanoTablesContext BuildContext(AssemblyDefinition assembly)
        {
            return new nanoTablesContext(
                assembly,
                null,
                null,
                false,
                false,
                false);
        }

        private static nanoAssemblyBuilder CompileAndMinimize(AssemblyDefinition assembly)
        {
            var builder = new nanoAssemblyBuilder(assembly, false);

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                builder.Write(nanoBinaryWriter.CreateLittleEndianBinaryWriter(writer));
            }

            builder.Minimize();

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                builder.Write(nanoBinaryWriter.CreateLittleEndianBinaryWriter(writer));
            }

            return builder;
        }

        private static TypeDefinition GetStubsAppType(AssemblyDefinition assembly, string name)
        {
            return assembly.MainModule.GetTypes().Single(t => t.Namespace == StubsAppNamespace && t.Name == name);
        }

        private static void SetFakeToken(IMetadataTokenProvider item, TokenType tokenType)
        {
            item.MetadataToken = new MetadataToken(tokenType, s_fakeRid++);
        }

        private const string CompilerGeneratedAttributeName = "CompilerGeneratedAttribute";
        private const string InlineArrayAttributeName = "InlineArrayAttribute";

        private static TypeDefinition AddCompilerGeneratedType(TypeDefinition declaringType, string name, string markerAttribute)
        {
            ModuleDefinition module = declaringType.Module;

            var type = new TypeDefinition(
                string.Empty,
                name,
                TypeAttributes.NestedPrivate | TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
                module.TypeSystem.Object);
            SetFakeToken(type, TokenType.TypeDef);

            if (markerAttribute != null)
            {
                // make up a reference to the marker attribute, so the test doesn't depend on the core library version
                var attributeType = new TypeReference("System.Runtime.CompilerServices", markerAttribute, module, module.TypeSystem.CoreLibrary);
                var ctor = new MethodReference(".ctor", module.TypeSystem.Void, attributeType) { HasThis = true };
                type.CustomAttributes.Add(new CustomAttribute(ctor));
            }

            FieldDefinition stateField = new FieldDefinition("<>1__state", FieldAttributes.Public, module.TypeSystem.Int32);
            SetFakeToken(stateField, TokenType.Field);
            type.Fields.Add(stateField);

            FieldDefinition capturedField = new FieldDefinition("captured", FieldAttributes.Public, module.TypeSystem.String);
            SetFakeToken(capturedField, TokenType.Field);
            type.Fields.Add(capturedField);

            declaringType.NestedTypes.Add(type);

            return type;
        }

        private static MethodDefinition AddManagedMethod(TypeDefinition type, string name)
        {
            var method = new MethodDefinition(
                name,
                MethodAttributes.Public | MethodAttributes.HideBySig,
                type.Module.TypeSystem.Void);
            SetFakeToken(method, TokenType.Method);

            method.Body.GetILProcessor().Emit(OpCodes.Ret);

            type.Methods.Add(method);

            return method;
        }

        private static MethodDefinition AddNativeMethod(TypeDefinition type, string name, TypeReference returnType)
        {
            var method = new MethodDefinition(
                name,
                MethodAttributes.Private | MethodAttributes.HideBySig,
                returnType)
            {
                ImplAttributes = MethodImplAttributes.InternalCall
            };
            SetFakeToken(method, TokenType.Method);

            Assert.IsFalse(method.HasBody);

            type.Methods.Add(method);

            return method;
        }

        /// <summary>
        /// Adds a static managed method to Program taking a NativeWithFields parameter.
        /// </summary>
        private static MethodDefinition AddTouchMethod(AssemblyDefinition assembly)
        {
            TypeDefinition program = GetStubsAppType(assembly, "Program");

            var method = new MethodDefinition(
                "Touch",
                MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
                assembly.MainModule.TypeSystem.Void);
            SetFakeToken(method, TokenType.Method);

            method.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, GetStubsAppType(assembly, "NativeWithFields")));
            method.Body.GetILProcessor().Emit(OpCodes.Ret);

            program.Methods.Add(method);

            return method;
        }

        private static bool IsDelegate(TypeDefinition type)
        {
            return type.BaseType != null && type.BaseType.FullName == "System.MulticastDelegate";
        }

        private static int CountInstanceFieldsInHierarchy(TypeDefinition type)
        {
            int count = type.Fields.Count(f => !f.IsStatic && !f.IsLiteral);

            if (type.BaseType != null && type.BaseType.FullName != "System.Object")
            {
                count += CountInstanceFieldsInHierarchy(type.BaseType.Resolve());
            }

            return count;
        }

        #endregion
    }
}
