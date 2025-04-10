// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Utility
{
    [TestClass]
    public class NativeMethodsCrcTests
    {
        [TestMethod]
        public void GetClassNameTest()
        {
            var nanoTablesContext = TestObjectHelper.GetTestNFAppNanoTablesContext();
            var typeDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllTypeDefinition(nanoTablesContext.AssemblyDefinition);

            // test
            var r = NativeMethodsCrc.GetClassName(typeDefinition);

            Assert.AreEqual("TestNFApp_OneClassOverAll", r);

            // test
            r = NativeMethodsCrc.GetClassName(null);

            Assert.AreEqual(String.Empty, r);
        }

        [TestMethod]
        public void GetMethodNameTest()
        {
            var nanoTablesContext = TestObjectHelper.GetTestNFAppNanoTablesContext();
            var typeDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllTypeDefinition(nanoTablesContext.AssemblyDefinition);
            var methodDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllDummyMethodDefinition(typeDefinition);

            // test
            var r = NativeMethodsCrc.GetMethodName(methodDefinition);

            Assert.AreEqual("DummyMethod___VOID", r);



            methodDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllDummyMethodWithParamsDefinition(typeDefinition);

            // test
            r = NativeMethodsCrc.GetMethodName(methodDefinition);

            Assert.AreEqual("DummyMethodWithParams___VOID__I4__STRING", r);



            methodDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllDummyStaticMethodDefinition(typeDefinition);

            // test
            r = NativeMethodsCrc.GetMethodName(methodDefinition);

            Assert.AreEqual("DummyStaticMethod___STATIC__VOID", r);



            methodDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllDummyStaticMethodWithParamsDefinition(typeDefinition);

            // test
            r = NativeMethodsCrc.GetMethodName(methodDefinition);

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

            DoGetNanoCLRTypeNameTest(assemblyDefinition, this.GetType(), "nanoFrameworkToolsMetadataProcessorTestsCoreUtilityNativeMethodsCrcTests");

        }

        private void DoGetNanoCLRTypeNameTest(AssemblyDefinition assemblyDefinition, Type type, string expectedNanoCLRTypeName)
        {
            var typeReference = assemblyDefinition.MainModule.ImportReference(type);

            // test
            var r = NativeMethodsCrc.GetNanoCLRTypeName(typeReference);

            Assert.AreEqual(expectedNanoCLRTypeName, r);
        }


        [TestMethod]
        public void CurrentCrcInitialTest()
        {
            var assemblyDefinition = TestObjectHelper.GetTestNFAppAssemblyDefinition();

            var iut = new NativeMethodsCrc(assemblyDefinition, new List<string>());

            // test
            Assert.AreEqual((uint)0, iut.CurrentCrc);
        }

        [TestMethod]
        public void CrcWithMethodDefinitionTest()
        {
            var assemblyDefinition = TestObjectHelper.GetTestNFAppAssemblyDefinition();
            var typeDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllTypeDefinition(assemblyDefinition);
            var nonExternMethodDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllDummyMethodDefinition(typeDefinition);
            var externMethodDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllDummyExternMethodDefinition(typeDefinition);

            var iut = new NativeMethodsCrc(assemblyDefinition, new List<string>());

            // test
            iut.UpdateCrc(nonExternMethodDefinition);

            Assert.AreEqual((uint)0, iut.CurrentCrc);


            // test
            iut.UpdateCrc(externMethodDefinition);

            Assert.AreEqual((uint)519713657, iut.CurrentCrc);

            // test
            iut.UpdateCrc(externMethodDefinition);


            Assert.AreEqual((uint)1574868855, iut.CurrentCrc);


            // test
            iut.UpdateCrc(nonExternMethodDefinition);

            Assert.AreEqual((uint)882012044, iut.CurrentCrc);
        }

        [TestMethod]
        public void CrcWithTypeDefinitionTableTest()
        {
            var context = TestObjectHelper.GetTestNFAppNanoTablesContext();

            var iut = new NativeMethodsCrc(context.AssemblyDefinition, new List<string>());

            // test
            iut.UpdateCrc(context.TypeDefinitionTable);

            Assert.AreNotEqual((uint)0, iut.CurrentCrc);
        }
    }
}
