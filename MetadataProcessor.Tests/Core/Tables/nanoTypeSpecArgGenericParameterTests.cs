// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Regression tests for: BuildTypeSpecArg (Pdbx/PdbxFileHelpers.cs) correctly describing a generic
// instance's own argument when that argument is itself a bare generic parameter (VAR/MVAR) rather than
// a closed type -- e.g. the T in Pair<T,int> inside the body of the generic type Container<T> that
// declares T. Before the fix, such an argument fell through to the ordinary-class branch, which calls
// TypeReference.Resolve() on the GenericParameter (returns null: it has no TypeDefinition of its own)
// and records a meaningless, unqualified ClassName ("T") with no token -- losing which parameter it is
// and whose type or method declares it.
//
// Fixture: TestNFApp/GenericParameterArgumentTypeSpecTests.cs, GenParamContainer<T>.

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Tables
{
    [TestClass]
    public class nanoTypeSpecArgGenericParameterTests
    {
        // Pdbx construction (via the Method constructor, PdbxFileHelpers.cs) reads
        // nanoTypeDefinitionTable's byte-code-offset table, which is only populated as a side effect of
        // nanoAssemblyBuilder.Write() running after Minimize() -- see nanoPdbxFileWriter's only other
        // caller, nanoAssemblyBuilder.Write(string), which always runs Write() then Minimize() then
        // Write() again before ever constructing a Pdbx. A bare TestObjectHelper.GetTestNFAppNanoTablesContext()
        // context (no builder pass run against it) throws KeyNotFoundException in GetByteCodeOffsets.
        private static nanoTablesContext BuildTestNFAppPdbxContext()
        {
            var assemblyDefinition = TestObjectHelper.GetTestNFAppAssemblyDefinitionWithLoadHints();
            var assemblyBuilder = new nanoAssemblyBuilder(assemblyDefinition, false);

            TestObjectHelper.DoWithNanoBinaryWriter(
                bw => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw),
                (ms, bw, writer) => assemblyBuilder.Write(writer));

            assemblyBuilder.Minimize();

            TestObjectHelper.DoWithNanoBinaryWriter(
                bw => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw),
                (ms, bw, writer) => assemblyBuilder.Write(writer));

            return assemblyBuilder.TablesContext;
        }

        private static TypeDefinition FindContainerType(nanoTablesContext context)
        {
            return context.AssemblyDefinition.MainModule.Types.First(t => t.Name == "GenParamContainer`1");
        }

        private static TypeSpec FindTypeSpecFor(nanoTablesContext context, Pdbx pdbx, TypeReference genericInstance)
        {
            Assert.IsTrue(
                context.TypeSpecificationsTable.TryGetTypeReferenceId(genericInstance, out ushort typeSpecId),
                "genericInstance is not registered in the TypeSpecifications table.");

            string expectedNanoToken =
                (NanoClrTable.TBL_TypeSpec.ToNanoTokenType() | typeSpecId).ToString("X8");

            TypeSpec typeSpec = pdbx.Assembly.TypeSpecs.FirstOrDefault(ts => ts.Token?.NanoCLR == expectedNanoToken);

            Assert.IsNotNull(typeSpec, "No TypeSpec entry found for the expected NanoCLR token.");

            return typeSpec;
        }

        // Type-owned case (VAR): GenParamContainer<T>.Slot is typed GenParamPair<T,int> -- T is the
        // enclosing type's own type parameter.
        [TestMethod]
        public void BuildTypeSpecArg_TypeOwnedGenericParameterArgument_IsRecordedExplicitly()
        {
            nanoTablesContext context = BuildTestNFAppPdbxContext();
            var pdbx = new Pdbx(context);

            TypeDefinition containerType = FindContainerType(context);
            FieldDefinition slotField = containerType.Fields.First(f => f.Name == "Slot");

            TypeSpec typeSpec = FindTypeSpecFor(context, pdbx, slotField.FieldType);

            Assert.IsTrue(typeSpec.IsGenericInstance);
            Assert.IsNotNull(typeSpec.GenericArguments);
            Assert.AreEqual(2, typeSpec.GenericArguments.Count);

            TypeSpecArg firstArg = typeSpec.GenericArguments[0];

            Assert.IsTrue(firstArg.IsGenericParameter, "First argument (T) should be recorded as a generic parameter.");
            Assert.IsFalse(firstArg.IsPrimitive);
            Assert.IsNull(firstArg.TypeToken);
            Assert.IsNull(firstArg.ClassName);
            Assert.IsFalse(firstArg.GenericParamIsMethodOwned, "T is declared by the type, not a method.");
            Assert.AreEqual(0, firstArg.GenericParamPosition);
            Assert.IsNotNull(firstArg.GenericParamToken, "T is declared locally, so its TBL_GenericParam token should resolve.");

            TypeSpecArg secondArg = typeSpec.GenericArguments[1];

            Assert.IsTrue(secondArg.IsPrimitive, "Second argument (int) should resolve as a primitive.");
            Assert.AreEqual(NanoCLRDataType.DATATYPE_I4.ToString(), secondArg.PrimitiveType);
            Assert.IsFalse(secondArg.IsGenericParameter);
        }

        // Method-owned case (MVAR) alongside a type-owned one (VAR) in the same TypeSpec:
        // GenParamContainer<T>.Wrap<U> returns GenParamPair<U,T>.
        [TestMethod]
        public void BuildTypeSpecArg_MethodOwnedGenericParameterArgument_IsRecordedExplicitly()
        {
            nanoTablesContext context = BuildTestNFAppPdbxContext();
            var pdbx = new Pdbx(context);

            TypeDefinition containerType = FindContainerType(context);
            MethodDefinition wrapMethod = containerType.Methods.First(m => m.Name == "Wrap");

            TypeSpec typeSpec = FindTypeSpecFor(context, pdbx, wrapMethod.ReturnType);

            Assert.IsTrue(typeSpec.IsGenericInstance);
            Assert.IsNotNull(typeSpec.GenericArguments);
            Assert.AreEqual(2, typeSpec.GenericArguments.Count);

            TypeSpecArg firstArg = typeSpec.GenericArguments[0];

            Assert.IsTrue(firstArg.IsGenericParameter, "First argument (U) should be recorded as a generic parameter.");
            Assert.IsTrue(firstArg.GenericParamIsMethodOwned, "U is declared by the Wrap<U> method.");
            Assert.AreEqual(0, firstArg.GenericParamPosition);
            Assert.IsNotNull(firstArg.GenericParamToken);

            TypeSpecArg secondArg = typeSpec.GenericArguments[1];

            Assert.IsTrue(secondArg.IsGenericParameter, "Second argument (T) should be recorded as a generic parameter.");
            Assert.IsFalse(secondArg.GenericParamIsMethodOwned, "T is declared by the enclosing type, not Wrap<U>.");
            Assert.AreEqual(0, secondArg.GenericParamPosition);
            Assert.IsNotNull(secondArg.GenericParamToken);

            // U and T are distinct declarations (different owners), so their tokens must differ even
            // though both report position 0.
            Assert.AreNotEqual(firstArg.GenericParamToken.NanoCLR, secondArg.GenericParamToken.NanoCLR);
        }
    }
}
