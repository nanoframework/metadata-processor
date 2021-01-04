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
        public void GetnanoClrTypeNameTest()
        {
            var assemblyDefinition = TestObjectHelper.GetTestNFAppAssemblyDefinition();

            DoGetnanoClrTypeNameTest(assemblyDefinition, typeof(void), "DATATYPE_VOID");
            DoGetnanoClrTypeNameTest(assemblyDefinition, typeof(sbyte), "DATATYPE_I1");
            DoGetnanoClrTypeNameTest(assemblyDefinition, typeof(short), "DATATYPE_I2");
            DoGetnanoClrTypeNameTest(assemblyDefinition, typeof(int), "DATATYPE_I4");
            DoGetnanoClrTypeNameTest(assemblyDefinition, typeof(long), "DATATYPE_I8");

            DoGetnanoClrTypeNameTest(assemblyDefinition, typeof(byte), "DATATYPE_U1");
            DoGetnanoClrTypeNameTest(assemblyDefinition, typeof(ushort), "DATATYPE_U2");
            DoGetnanoClrTypeNameTest(assemblyDefinition, typeof(uint), "DATATYPE_U4");
            DoGetnanoClrTypeNameTest(assemblyDefinition, typeof(ulong), "DATATYPE_U8");

            DoGetnanoClrTypeNameTest(assemblyDefinition, typeof(float), "DATATYPE_R4");
            DoGetnanoClrTypeNameTest(assemblyDefinition, typeof(double), "DATATYPE_R8");

            DoGetnanoClrTypeNameTest(assemblyDefinition, typeof(char), "DATATYPE_CHAR");
            DoGetnanoClrTypeNameTest(assemblyDefinition, typeof(string), "DATATYPE_STRING");
            DoGetnanoClrTypeNameTest(assemblyDefinition, typeof(bool), "DATATYPE_BOOLEAN");

            DoGetnanoClrTypeNameTest(assemblyDefinition, typeof(object), "DATATYPE_OBJECT");
            DoGetnanoClrTypeNameTest(assemblyDefinition, typeof(IntPtr), "DATATYPE_I4");
            DoGetnanoClrTypeNameTest(assemblyDefinition, typeof(UIntPtr), "DATATYPE_U4");

            DoGetnanoClrTypeNameTest(assemblyDefinition, typeof(System.WeakReference), "DATATYPE_WEAKCLASS");

            DoGetnanoClrTypeNameTest(assemblyDefinition, this.GetType(), "nanoFrameworkToolsMetadataProcessorTestsCoreUtilityNativeMethodsCrcTests");

        }

        private void DoGetnanoClrTypeNameTest(AssemblyDefinition assemblyDefinition, Type type, string expectedNanoClrTypeName)
        {
            var typeReference = assemblyDefinition.MainModule.ImportReference(type);

            // test
            var r = NativeMethodsCrc.GetnanoClrTypeName(typeReference);

            Assert.AreEqual(expectedNanoClrTypeName, r);
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

            Assert.AreEqual((uint)2748897355, iut.CurrentCrc);
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
