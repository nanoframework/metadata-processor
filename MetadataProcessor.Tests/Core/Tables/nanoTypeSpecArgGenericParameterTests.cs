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

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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

        // Matches by structure (GenericTypeDefName + argument shape), not by re-deriving a Cecil
        // TypeReference (e.g. a field's FieldType) and asking nanoTypeSpecificationsTable whether it's a
        // registered key: that table populates itself once, at nanoTablesContext construction time, using
        // TypeSpecification equality based on a signature ID computed at that moment (see
        // TypeReferenceEqualityComparer). A TypeReference read fresh off the assembly after
        // Write/Minimize/Write is not guaranteed to hash/compare equal to what was cached then, so
        // re-querying the table from a test is timing-fragile. The already-built Pdbx model has no such
        // dependency -- this is also how a real consumer (CorDebugTypeParameter) finds its data, from the
        // materialized model, never by re-deriving a Cecil reference.
        private static TypeSpec FindTypeSpec(Pdbx pdbx, Func<TypeSpec, bool> predicate, string description)
        {
            int matchCount = pdbx.Assembly.TypeSpecs.Count(predicate);

            Assert.AreEqual(1, matchCount, $"Expected exactly one TypeSpec matching {description}, found {matchCount}.");

            return pdbx.Assembly.TypeSpecs.First(predicate);
        }

        // Type-owned case (VAR): GenParamContainer<T>.Slot is typed GenParamPair<T,int> -- T is the
        // enclosing type's own type parameter.
        [TestMethod]
        public void BuildTypeSpecArg_TypeOwnedGenericParameterArgument_IsRecordedExplicitly()
        {
            nanoTablesContext context = BuildTestNFAppPdbxContext();
            var pdbx = new Pdbx(context);

            TypeSpec typeSpec = FindTypeSpec(
                pdbx,
                ts => ts.IsGenericInstance
                    && ts.GenericTypeDefName != null && ts.GenericTypeDefName.EndsWith("GenParamPair`2")
                    && ts.GenericArguments?.Count == 2
                    && ts.GenericArguments[0].IsGenericParameter && !ts.GenericArguments[0].GenericParamIsMethodOwned
                    && ts.GenericArguments[1].IsPrimitive,
                "GenParamPair<T,int> (Slot field type)");

            TypeSpecArg firstArg = typeSpec.GenericArguments[0];

            Assert.IsFalse(firstArg.IsPrimitive);
            Assert.IsNull(firstArg.TypeToken);
            Assert.IsNull(firstArg.ClassName);
            Assert.AreEqual(0, firstArg.GenericParamPosition);
            Assert.IsNotNull(firstArg.GenericParamToken, "T is declared locally, so its TBL_GenericParam token should resolve.");

            TypeSpecArg secondArg = typeSpec.GenericArguments[1];

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

            TypeSpec typeSpec = FindTypeSpec(
                pdbx,
                ts => ts.IsGenericInstance
                    && ts.GenericTypeDefName != null && ts.GenericTypeDefName.EndsWith("GenParamPair`2")
                    && ts.GenericArguments?.Count == 2
                    && ts.GenericArguments[0].IsGenericParameter && ts.GenericArguments[0].GenericParamIsMethodOwned
                    && ts.GenericArguments[1].IsGenericParameter && !ts.GenericArguments[1].GenericParamIsMethodOwned,
                "GenParamPair<U,T> (Wrap<U> return type)");

            TypeSpecArg firstArg = typeSpec.GenericArguments[0];

            Assert.AreEqual(0, firstArg.GenericParamPosition);
            Assert.IsNotNull(firstArg.GenericParamToken);

            TypeSpecArg secondArg = typeSpec.GenericArguments[1];

            Assert.AreEqual(0, secondArg.GenericParamPosition);
            Assert.IsNotNull(secondArg.GenericParamToken);

            // U and T are distinct declarations (different owners), so their tokens must differ even
            // though both report position 0.
            Assert.AreNotEqual(firstArg.GenericParamToken.NanoCLR, secondArg.GenericParamToken.NanoCLR);
        }
    }
}
