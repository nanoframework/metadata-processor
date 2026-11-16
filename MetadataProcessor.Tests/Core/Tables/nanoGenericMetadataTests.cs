// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Tables
{
    // Generic metadata as the CLR reads it: ldtoken of a type parameter is a TypeSpec, TypeSpec rows have flags and no
    // duplicates, generic parameters carry their constraints, variance bits are written.

    [TestClass]
    public class nanoGenericMetadataTests
    {
        // replicate a CLR_RECORD_ASSEMBLY
        private const int StartOfTablesOffset = 36;
        private const int GenericParamTable = 7;
        private const int TypeSpecTable = 9;
        private const int SignaturesTable = 14;
        private const int TypeSpecRecordSize = 4;
        private const int GenericParamRecordSize = 10;

        private const ushort TS_Closed = 0x0001;
        private const ushort TS_OverOwnParameters = 0x0002;
        private const ushort TS_HasVar = 0x0004;
        private const ushort TS_DepthMask = 0x0070;
        private const ushort TS_GenericInstance = 0x0080;

        private sealed class FinalAssembly
        {
            public byte[] Bytes;
            public nanoTablesContext Context;

            private int TableStart(int table)
            {
                return (int)BitConverter.ToUInt32(Bytes, StartOfTablesOffset + (table * sizeof(uint)));
            }

            public int TypeSpecCount
            {
                get
                {
                    int count = 0;

                    while (Context.TypeSpecificationsTable.TryGetTypeReferenceByIndex((ushort)count) != null)
                    {
                        count++;
                    }

                    return count;
                }
            }

            public ushort TypeSpecSignature(int index)
            {
                return BitConverter.ToUInt16(Bytes, TableStart(TypeSpecTable) + (index * TypeSpecRecordSize));
            }

            public ushort TypeSpecFlags(int index)
            {
                return BitConverter.ToUInt16(Bytes, TableStart(TypeSpecTable) + (index * TypeSpecRecordSize) + 2);
            }

            public int FindTypeSpec(Func<TypeReference, bool> predicate)
            {
                for (int i = 0; ; i++)
                {
                    TypeReference candidate = Context.TypeSpecificationsTable.TryGetTypeReferenceByIndex((ushort)i);

                    Assert.IsNotNull(candidate, "TypeSpec not found");

                    if (predicate(candidate))
                    {
                        return i;
                    }
                }
            }

            public GenericParameter GenericParameterOf(string typeName)
            {
                return Context.TypeDefinitionTable.Items.First(t => t.FullName == typeName).GenericParameters[0];
            }

            private int GenericParamRow(GenericParameter parameter)
            {
                Assert.IsTrue(Context.GenericParamsTable.TryGetParameterId(parameter, out ushort id));

                return TableStart(GenericParamTable) + (id * GenericParamRecordSize);
            }

            public ushort GenericParamFlags(GenericParameter parameter)
            {
                return BitConverter.ToUInt16(Bytes, GenericParamRow(parameter) + 2);
            }

            public ushort GenericParamSignature(GenericParameter parameter)
            {
                return BitConverter.ToUInt16(Bytes, GenericParamRow(parameter) + 6);
            }

            public byte[] SignatureAt(ushort signatureId, int length)
            {
                return Bytes.Skip(TableStart(SignaturesTable) + signatureId).Take(length).ToArray();
            }
        }

        // writing the assembly takes long, the tests only read it
        private static FinalAssembly s_assembly;

        private static FinalAssembly BuildFinalAssembly()
        {
            return s_assembly ?? (s_assembly = BuildFinalAssemblyUncached());
        }

        // the assembly as it is written after minimizing it, the same sequence as the build does
        private static FinalAssembly BuildFinalAssemblyUncached()
        {
            var assemblyBuilder = new nanoAssemblyBuilder(TestObjectHelper.GetTestNFAppAssemblyDefinitionWithLoadHints(), false);

            TestObjectHelper.DoWithNanoBinaryWriter(
                bw => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw),
                (ms, bw, writer) => assemblyBuilder.Write(writer));

            assemblyBuilder.Minimize();

            byte[] bytes = TestObjectHelper.DoWithNanoBinaryWriter(
                bw => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw),
                (ms, bw, writer) => assemblyBuilder.Write(writer));

            return new FinalAssembly { Bytes = bytes, Context = assemblyBuilder.TablesContext };
        }

        private static void AssertLdtokenIsTypeSpec(FinalAssembly assembly, string typeName, string methodName)
        {
            MethodDefinition method = assembly.Context.MethodDefinitionTable.Items
                .First(m => m.DeclaringType.FullName == typeName && m.Name == methodName);

            Instruction ldtoken = method.Body.Instructions.First(i => i.OpCode == OpCodes.Ldtoken);

            Assert.IsInstanceOfType(ldtoken.Operand, typeof(GenericParameter));

            uint token = assembly.Context.GetMetadataToken((IMetadataTokenProvider)ldtoken.Operand);

            Assert.AreEqual(0x09u, token >> 24, "ldtoken of a type parameter has to be a TypeSpec token");

            Assert.IsTrue(assembly.Context.TypeSpecificationsTable.TryGetTypeReferenceId((GenericParameter)ldtoken.Operand, out ushort row));
            Assert.AreEqual((uint)row, token & 0xFFFF);
        }

        [TestMethod]
        public void LdtokenOfTypeParameter_IsTypeSpec()
        {
            FinalAssembly assembly = BuildFinalAssembly();

            AssertLdtokenIsTypeSpec(assembly, "TestNFApp.TypeOfFirst`1", "Get");
            AssertLdtokenIsTypeSpec(assembly, "TestNFApp.TypeOfSecond`1", "Get");
        }

        [TestMethod]
        public void LdtokenOfMethodTypeParameter_IsTypeSpec()
        {
            AssertLdtokenIsTypeSpec(BuildFinalAssembly(), "TestNFApp.TypeOfFirst`1", "Method");
        }

        [TestMethod]
        public void TypeSpecRows_HaveUniqueSignatures()
        {
            FinalAssembly assembly = BuildFinalAssembly();

            var seen = new Dictionary<ushort, int>();

            for (int i = 0; i < assembly.TypeSpecCount; i++)
            {
                ushort signature = assembly.TypeSpecSignature(i);

                Assert.IsFalse(seen.TryGetValue(signature, out int other), $"TypeSpec rows {other} and {i} have the same signature {signature}");

                seen.Add(signature, i);
            }
        }

        [TestMethod]
        public void TypeSpecRowOfTypeParameter_IsSharedByOwners()
        {
            FinalAssembly assembly = BuildFinalAssembly();

            // T of TypeOfFirst, TypeOfSecond, ConstrainedClass... are all !0
            int rows = 0;

            for (int i = 0; i < assembly.TypeSpecCount; i++)
            {
                if (assembly.Context.TypeSpecificationsTable.TryGetTypeReferenceByIndex((ushort)i) is GenericParameter parameter
                    && parameter.Type == GenericParameterType.Type
                    && parameter.Position == 0)
                {
                    rows++;
                }
            }

            Assert.AreEqual(1, rows);
        }

        [TestMethod]
        public void TypeSpecFlags_OfTypeParameter()
        {
            FinalAssembly assembly = BuildFinalAssembly();

            int row = assembly.FindTypeSpec(t => t is GenericParameter p && p.Type == GenericParameterType.Type && p.Position == 0);

            Assert.AreEqual(TS_HasVar, assembly.TypeSpecFlags(row));
        }

        [TestMethod]
        public void TypeSpecFlags_OfClosedInstance()
        {
            FinalAssembly assembly = BuildFinalAssembly();

            ushort flags = assembly.TypeSpecFlags(
                assembly.FindTypeSpec(t => t.FullName == "TestNFApp.IBoundMarker`1<System.Int32>"));

            Assert.AreEqual(TS_Closed | TS_GenericInstance | (1 << 4), flags);
        }

        [TestMethod]
        public void TypeSpecFlags_OfNestedClosedInstance_HasDepth()
        {
            FinalAssembly assembly = BuildFinalAssembly();

            ushort flags = assembly.TypeSpecFlags(
                assembly.FindTypeSpec(t => t.FullName == "TestNFApp.IBoundMarker`1<TestNFApp.IBoundMarker`1<System.Int32>>"));

            Assert.AreEqual(TS_Closed | TS_GenericInstance | (2 << 4), flags);
        }

        [TestMethod]
        public void TypeSpecFlags_OfInstanceOverOwnParameters()
        {
            FinalAssembly assembly = BuildFinalAssembly();

            ushort flags = assembly.TypeSpecFlags(
                assembly.FindTypeSpec(t => t is GenericInstanceType g
                                           && g.ElementType.FullName == "TestNFApp.IBoundMarker`1"
                                           && g.GenericArguments[0] is GenericParameter));

            Assert.AreEqual(TS_OverOwnParameters | TS_HasVar | TS_GenericInstance | (1 << 4), flags);
            Assert.AreEqual(0, flags & TS_Closed);
            Assert.AreEqual(1 << 4, flags & TS_DepthMask);
        }

        [TestMethod]
        public void GenericParameterWithoutConstraints_HasNoSignature()
        {
            FinalAssembly assembly = BuildFinalAssembly();

            Assert.AreEqual(0xFFFF, assembly.GenericParamSignature(assembly.GenericParameterOf("TestNFApp.NotConstrained`1")));
        }

        [TestMethod]
        public void GenericParameterConstraints_AreWrittenAsCountAndTypes()
        {
            FinalAssembly assembly = BuildFinalAssembly();
            GenericParameter parameter = assembly.GenericParameterOf("TestNFApp.ConstrainedClass`1");

            Assert.AreNotEqual(0xFFFF, assembly.GenericParamSignature(parameter));

            // one type constraint, then a generic instance
            byte[] blob = assembly.SignatureAt(assembly.GenericParamSignature(parameter), 2);

            Assert.AreEqual(1, blob[0]);
            Assert.AreEqual((byte)NanoCLRDataType.DATATYPE_GENERICINST, blob[1]);
        }

        [TestMethod]
        public void GenericParameterSpecialConstraints_AreInFlags()
        {
            FinalAssembly assembly = BuildFinalAssembly();

            // reference type constraint
            Assert.AreEqual(0x04, assembly.GenericParamFlags(assembly.GenericParameterOf("TestNFApp.ConstrainedClass`1")) & 0x04);

            // not nullable value type constraint
            Assert.AreEqual(0x08, assembly.GenericParamFlags(assembly.GenericParameterOf("TestNFApp.ConstrainedStruct`1")) & 0x08);
        }

        [TestMethod]
        public void GenericParameterVariance_IsInFlags()
        {
            FinalAssembly assembly = BuildFinalAssembly();

            Assert.AreEqual(0x01, assembly.GenericParamFlags(assembly.GenericParameterOf("TestNFApp.IBoundOut`1")) & 0x03);
            Assert.AreEqual(0x02, assembly.GenericParamFlags(assembly.GenericParameterOf("TestNFApp.IBoundIn`1")) & 0x03);
            Assert.AreEqual(0x00, assembly.GenericParamFlags(assembly.GenericParameterOf("TestNFApp.NotConstrained`1")) & 0x03);
        }

        [TestMethod]
        public void Assembly_IsWrittenTheSameEveryTime()
        {
            CollectionAssert.AreEqual(BuildFinalAssembly().Bytes, BuildFinalAssemblyUncached().Bytes);
        }
    }
}
