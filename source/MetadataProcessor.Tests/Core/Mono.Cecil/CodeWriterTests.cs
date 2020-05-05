using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.MonoCecil
{
    [TestClass]
    public class CodeWriterTests
    {
        [TestMethod]
        public void CalculateStackSizeIntegrationTest()
        {
            var assemblyDefinition = TestObjectHelper.GetTestNFAppAssemblyDefinition();
            var oneClassOverAllTypeDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllTypeDefinition(assemblyDefinition);

            var uglyMethodDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllUglyAddMethodDefinition(oneClassOverAllTypeDefinition);

            // test
            var r = CodeWriter.CalculateStackSize(uglyMethodDefinition.Body);

            Assert.AreEqual((byte)2, r);

        }

        [TestMethod]
        public void CalculateStackSizeNullTest()
        {
            // test
            var r = CodeWriter.CalculateStackSize(null);

            Assert.AreEqual((byte)0, r);
        }

        [TestMethod]
        public void CalculateStackSizeNoBodyTest()
        {
            var assemblyDefinition = TestObjectHelper.GetTestNFAppAssemblyDefinition();
            var oneClassOverAllTypeDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllTypeDefinition(assemblyDefinition);

            var externMethodDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllDummyExternMethodDefinition(oneClassOverAllTypeDefinition);

            // test
            var r = CodeWriter.CalculateStackSize(externMethodDefinition.Body);

            Assert.AreEqual((byte)0, r);
        }

        [TestMethod]
        public void CalculateStackSizeSimpleTest()
        {
            DoCalculateStackSizeDynamicILTest((ilProcessor) =>
            {
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ldarg_1));
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ldarg_2));
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Add));
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ret));

                TestObjectHelper.AdjustMethodBodyOffsets(ilProcessor.Body);
            }, 2);
        }

        [TestMethod]
        public void CalculateStackSizeMaxTest()
        {
            DoCalculateStackSizeDynamicILTest((ilProcessor) =>
            {
                var cstrWith2Params = ilProcessor.Body.Method.DeclaringType.Methods.First(i => i.Name == ".ctor" && i.Parameters.Count == 2);
                var cstrWith3Params = ilProcessor.Body.Method.DeclaringType.Methods.First(i => i.Name == ".ctor" && i.Parameters.Count == 3);

                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ldc_I4_0));
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ldc_I4_1));
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Newobj, cstrWith2Params));

                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ldc_I4_0));
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ldc_I4_1));
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ldc_I4_2));
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Newobj, cstrWith3Params));
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ret));

                TestObjectHelper.AdjustMethodBodyOffsets(ilProcessor.Body);
            }, 4);
        }

        [TestMethod]
        public void CalculateStackSizeNewobjParametersTest()
        {
            DoCalculateStackSizeDynamicILTest((ilProcessor) =>
            {
                var cstrWith3Params = ilProcessor.Body.Method.DeclaringType.Methods.First(i => i.Name == ".ctor" && i.Parameters.Count == 3);

                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ldc_I4_0));
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ldc_I4_1));
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ldc_I4_2));
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Newobj, cstrWith3Params));
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ret));

                TestObjectHelper.AdjustMethodBodyOffsets(ilProcessor.Body);
            }, 3);
        }


        [TestMethod]
        public void CalculateStackSizeCallvirtWithParametersTest()
        {
            DoCalculateStackSizeDynamicILTest((ilProcessor) =>
            {
                var dummyMethodWithParamsMethodDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllDummyMethodWithParamsDefinition(ilProcessor.Body.Method.DeclaringType);

                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ldc_I4_0));
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ldstr, "blabla"));
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Callvirt, dummyMethodWithParamsMethodDefinition));
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ret));

                TestObjectHelper.AdjustMethodBodyOffsets(ilProcessor.Body);
            }, 2);
        }

        [TestMethod]
        public void CalculateStackSizeCallvirtWithRetvalTest()
        {
            DoCalculateStackSizeDynamicILTest((ilProcessor) =>
            {
                var dummyStaticMethodWithRetvalMethodDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllDummyStaticMethodWithRetvalDefinition(ilProcessor.Body.Method.DeclaringType);

                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Callvirt, dummyStaticMethodWithRetvalMethodDefinition));
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ret));

                TestObjectHelper.AdjustMethodBodyOffsets(ilProcessor.Body);
            }, 1);
        }

        [TestMethod]
        public void CalculateStackSizeInlineBrTargetTest()
        {
            DoCalculateStackSizeDynamicILTest((ilProcessor) =>
            {
                // dummy stack size tampering ops
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ldc_I4_0));

                var ldcOp = ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ldc_I4_0);
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Br, ldcOp));

                // dummy stack size tampering ops
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Pop));
                ilProcessor.Append(ldcOp);

                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ret));

                TestObjectHelper.AdjustMethodBodyOffsets(ilProcessor.Body);
            }, 2);
        }



        [TestMethod]
        public void PreProcessMethodIntegrationTest()
        {
            var nanoTablesContext = TestObjectHelper.GetTestNFAppNanoTablesContext();
            var oneClassOverAllTypeDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllTypeDefinition(nanoTablesContext.AssemblyDefinition);
            var methodDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllUglyAddMethodDefinition(oneClassOverAllTypeDefinition);

            // test
            var r = CodeWriter.PreProcessMethod(methodDefinition, nanoTablesContext.StringTable).ToArray();

            Assert.AreEqual("[18,16][24,20][31,25]", String.Join(String.Empty, r.Select(i=>$"[{i.Item1.ToString()},{i.Item2.ToString()}]")));

            Assert.IsTrue(nanoTablesContext.StringTable.GetItems().ContainsKey("blabla"));
        }

        [TestMethod]
        public void PreProcessMethodWithoutBodyTest()
        {
            var nanoTablesContext = TestObjectHelper.GetTestNFAppNanoTablesContext();
            var oneClassOverAllTypeDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllTypeDefinition(nanoTablesContext.AssemblyDefinition);

            var methodDefinition = new Mono.Cecil.MethodDefinition("MethodCreatedOnTheFly", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Abstract, nanoTablesContext.AssemblyDefinition.MainModule.ImportReference(typeof(void)));
            Assert.IsFalse(methodDefinition.HasBody);
            oneClassOverAllTypeDefinition.Methods.Add(methodDefinition);

            // test
            var r = CodeWriter.PreProcessMethod(methodDefinition, nanoTablesContext.StringTable).ToArray();

            Assert.IsFalse(r.Any());
        }

        [TestMethod]
        public void PreProcessMethodWithInlineBrTargetTest()
        {
            var retOp = (Mono.Cecil.Cil.Instruction)null;

            DoPreProcessMethodDynamicILTest((ilProcessor) =>
            {
                retOp = ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ret);
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Br, retOp));
                ilProcessor.Append(retOp);

                TestObjectHelper.AdjustMethodBodyOffsets(ilProcessor.Body);

                Assert.AreEqual(5, retOp.Offset);
            },
            (r) =>
            {
                Assert.AreEqual("[5,3]", String.Join(String.Empty, r.Select(i => $"[{i.Item1.ToString()},{i.Item2.ToString()}]")));
                Assert.AreEqual(3, retOp.Offset);
            });

        }


        [TestMethod]
        public void PreProcessMethodWithInlineTypeTest()
        {
            var retOp = (Mono.Cecil.Cil.Instruction)null;

            DoPreProcessMethodDynamicILTest((ilProcessor) =>
            {
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Sizeof, ilProcessor.Body.Method.DeclaringType));
                retOp = ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ret);
                ilProcessor.Append(retOp);

                TestObjectHelper.AdjustMethodBodyOffsets(ilProcessor.Body);

                Assert.AreEqual(6, retOp.Offset);
            },
            (r) =>
            {
                Assert.AreEqual("[6,4]", String.Join(String.Empty, r.Select(i => $"[{i.Item1.ToString()},{i.Item2.ToString()}]")));
                Assert.AreEqual(4, retOp.Offset);
            });

        }

        [TestMethod]
        public void PreProcessMethodWithInlineFieldTest()
        {
            var retOp = (Mono.Cecil.Cil.Instruction)null;

            DoPreProcessMethodDynamicILTest((ilProcessor) =>
            {
                var dummyFieldDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllDummyFieldDefinition(ilProcessor.Body.Method.DeclaringType);
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ldfld, dummyFieldDefinition));
                retOp = ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ret);
                ilProcessor.Append(retOp);

                TestObjectHelper.AdjustMethodBodyOffsets(ilProcessor.Body);

                Assert.AreEqual(5, retOp.Offset);
            },
            (r) =>
            {
                Assert.AreEqual("[5,3]", String.Join(String.Empty, r.Select(i => $"[{i.Item1.ToString()},{i.Item2.ToString()}]")));
                Assert.AreEqual(3, retOp.Offset);
            });

        }

        [TestMethod]
        public void PreProcessMethodWithInlineMethodTest()
        {
            var retOp = (Mono.Cecil.Cil.Instruction)null;

            DoPreProcessMethodDynamicILTest((ilProcessor) =>
            {
                var dummyMethodDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllDummyMethodDefinition(ilProcessor.Body.Method.DeclaringType);
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Callvirt, dummyMethodDefinition));
                retOp = ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ret);
                ilProcessor.Append(retOp);

                TestObjectHelper.AdjustMethodBodyOffsets(ilProcessor.Body);

                Assert.AreEqual(5, retOp.Offset);
            },
            (r) =>
            {
                Assert.AreEqual("[5,3]", String.Join(String.Empty, r.Select(i => $"[{i.Item1.ToString()},{i.Item2.ToString()}]")));
                Assert.AreEqual(3, retOp.Offset);
            });

        }


        [TestMethod]
        public void PreProcessMethodWithInlineStringTest()
        {
            var retOp = (Mono.Cecil.Cil.Instruction)null;

            DoPreProcessMethodDynamicILTest((ilProcessor) =>
            {
                var dummyMethodDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllDummyMethodDefinition(ilProcessor.Body.Method.DeclaringType);
                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ldstr, "blabla"));
                retOp = ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ret);
                ilProcessor.Append(retOp);

                TestObjectHelper.AdjustMethodBodyOffsets(ilProcessor.Body);

                Assert.AreEqual(5, retOp.Offset);
            },
            (r) =>
            {
                Assert.AreEqual("[5,3]", String.Join(String.Empty, r.Select(i => $"[{i.Item1.ToString()},{i.Item2.ToString()}]")));
                Assert.AreEqual(3, retOp.Offset);
            });

        }


        [TestMethod]
        public void PreProcessMethodWithInlineSwitchTest()
        {
            var retOp = (Mono.Cecil.Cil.Instruction)null;

            DoPreProcessMethodDynamicILTest((ilProcessor) =>
            {
                var jumpTable = new List<Mono.Cecil.Cil.Instruction>();
                jumpTable.Add(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Nop));

                ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Switch, jumpTable.ToArray()));
                ilProcessor.Append(jumpTable[0]);
                retOp = ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ret);
                ilProcessor.Append(retOp);

                TestObjectHelper.AdjustMethodBodyOffsets(ilProcessor.Body);

                Assert.AreEqual(10, retOp.Offset);
            },
            (r) =>
            {
                Assert.AreEqual("[9,4]", String.Join(String.Empty, r.Select(i => $"[{i.Item1.ToString()},{i.Item2.ToString()}]")));
                Assert.AreEqual(5, retOp.Offset);
            });

        }


        private void DoPreProcessMethodDynamicILTest(Action<Mono.Cecil.Cil.ILProcessor> ilBuilderAction, Action<IEnumerable<Tuple<uint, uint>>> preProcessMethodCallResultHandlerAction)
        {
            var nanoTablesContext = TestObjectHelper.GetTestNFAppNanoTablesContext();
            var oneClassOverAllTypeDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllTypeDefinition(nanoTablesContext.AssemblyDefinition);

            var methodDefinition = new Mono.Cecil.MethodDefinition("MethodCreatedOnTheFly", Mono.Cecil.MethodAttributes.Public, nanoTablesContext.AssemblyDefinition.MainModule.TypeSystem.Void);
            oneClassOverAllTypeDefinition.Methods.Add(methodDefinition);

            var ilProcessor = methodDefinition.Body.GetILProcessor();

            ilBuilderAction(ilProcessor);


            // test
            var r = CodeWriter.PreProcessMethod(methodDefinition, nanoTablesContext.StringTable).ToArray();

            preProcessMethodCallResultHandlerAction(r);
        }


        private void DoCalculateStackSizeDynamicILTest(Action<Mono.Cecil.Cil.ILProcessor> ilBuilderAction, byte expectedCalculateStackSizeResult)
        {
            var nanoTablesContext = TestObjectHelper.GetTestNFAppNanoTablesContext();
            var oneClassOverAllTypeDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllTypeDefinition(nanoTablesContext.AssemblyDefinition);

            var methodDefinition = new Mono.Cecil.MethodDefinition("MethodCreatedOnTheFly", Mono.Cecil.MethodAttributes.Public, nanoTablesContext.AssemblyDefinition.MainModule.TypeSystem.Void);
            oneClassOverAllTypeDefinition.Methods.Add(methodDefinition);

            var ilProcessor = methodDefinition.Body.GetILProcessor();

            ilBuilderAction(ilProcessor);

            // test
            var r = CodeWriter.CalculateStackSize(methodDefinition.Body);

            Assert.AreEqual(expectedCalculateStackSizeResult, r);
        }



        [TestMethod]
        public void WriteMethodBodySimpleTest()
        {
            var nanoTablesContext = TestObjectHelper.GetTestNFAppNanoTablesContext();
            var oneClassOverAllTypeDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllTypeDefinition(nanoTablesContext.AssemblyDefinition);

            var methodDefinition = new Mono.Cecil.MethodDefinition("MethodCreatedOnTheFly", Mono.Cecil.MethodAttributes.Public, nanoTablesContext.AssemblyDefinition.MainModule.TypeSystem.Void);
            var ilProcessor = methodDefinition.Body.GetILProcessor();
            ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ldarg_1));
            ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ldarg_2));
            ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Add));
            ilProcessor.Append(ilProcessor.Create(Mono.Cecil.Cil.OpCodes.Ret));
            TestObjectHelper.AdjustMethodBodyOffsets(methodDefinition.Body);


            oneClassOverAllTypeDefinition.Methods.Add(methodDefinition);


            var bytesWritten = TestObjectHelper.DoWithNanoBinaryWriter((bw) => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw), (ms, bw, writer) =>
            {
                var iut = new CodeWriter(methodDefinition, writer, nanoTablesContext.StringTable, nanoTablesContext);

                // test
                iut.WriteMethodBody();
            });

            CollectionAssert.AreEqual(new byte[] 
            {
                0x03,   // ldarg.1
                0x04,   // ldarg.2
                0x58,   // add
                0x2A    // ret
            }, bytesWritten, BitConverter.ToString(bytesWritten));
        }


        [TestMethod]
        public void WriteMethodBodyIntegrationTest()
        {
            var nanoTablesContext = TestObjectHelper.GetTestNFAppNanoTablesContext();
            var oneClassOverAllTypeDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllTypeDefinition(nanoTablesContext.AssemblyDefinition);
            var methodDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllUglyAddMethodDefinition(oneClassOverAllTypeDefinition);

            var bytesWritten = TestObjectHelper.DoWithNanoBinaryWriter((bw) => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw), (ms, bw, writer) =>
            {
                var iut = new CodeWriter(methodDefinition, writer, nanoTablesContext.StringTable, nanoTablesContext);

                // test
                iut.WriteMethodBody();
            });


            var expectedBytesWritten = new byte[] { 0x00, 0x16, 0x0A, 0x00, 0x03, 0x04, 0x58, 0x0A, 0x00, 0xDE, 0x18, 0x0B, 0x00, 0x72, 0x50, 0x01, 0x07, 0x73, 0x0D, 0x80, 0x0C, 0x08, 0x6F, 0x0E, 0x80, 0x0A, 0x00, 0xDE, 0x00, 0x06, 0x0D, 0x2B, 0x00, 0x09, 0x2A, 0x00, 0x00, 0x11, 0x80, 0x03, 0x00, 0x0B, 0x00, 0x0B, 0x00, 0x23, 0x00, 0x01 };
            if (!nanoTablesContext.AssemblyDefinition.CustomAttributes.Any(i=>i.AttributeType.FullName == "System.Diagnostics.DebuggableAttribute"))
            {
                // we are in release mode, NOPs are missing
                expectedBytesWritten = new byte[] { 0x16, 0x0A, 0x03, 0x04, 0x58, 0x0A, 0xDE, 0x14, 0x0B, 0x72, 0x1E-01, 0x07, 0x73, 0x0C, 0x80, 0x6F, 0x0D, 0x80, 0x0A, 0xDE, 0x00, 0x06, 0x2A, 0x00, 0x00, 0x0F, 0x80, 0x02, 0x00, 0x08, 0x00, 0x08, 0x00, 0x1C, 0x00, 0x01 };
            }

            CollectionAssert.AreEqual(expectedBytesWritten, bytesWritten, BitConverter.ToString(bytesWritten));
        }
    }
}
