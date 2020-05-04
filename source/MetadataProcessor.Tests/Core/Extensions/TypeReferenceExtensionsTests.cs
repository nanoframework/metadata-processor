using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Extensions
{
    [TestClass]
    public class TypeReferenceExtensionsTests
    {
        [TestMethod]
        public void IsToIncludeTest()
        {
            var nanoTablesContext = TestObjectHelper.GetTestNFAppNanoTablesContext();

            var ignorableAttributeTypeReference = nanoTablesContext.TypeReferencesTable.Items.First(i => i.FullName == typeof(System.Diagnostics.DebuggableAttribute).FullName);

            // WARNING: leaking abstraction!!!!
            Assert.IsTrue(nanoTablesContext.IgnoringAttributes.Contains(ignorableAttributeTypeReference.FullName));

            // test
            var r = ignorableAttributeTypeReference.IsToInclude();

            Assert.IsFalse(r);


            var notIgnorableAttributeTypeReference = nanoTablesContext.TypeReferencesTable.Items.First(i => i.FullName == typeof(System.Reflection.AssemblyTitleAttribute).FullName);

            // WARNING: leaking abstraction!!!!
            Assert.IsFalse(nanoTablesContext.IgnoringAttributes.Contains(notIgnorableAttributeTypeReference.FullName));

            // test
            r = notIgnorableAttributeTypeReference.IsToInclude();

            Assert.IsTrue(r);
        }

        [TestMethod]
        public void TypeSignatureAsStringTest()
        {
            var mscorlibAssemblyDefinition = TestObjectHelper.GetmscorlibAssemblyDefinition();

            DoTypeSignatureAsStringTest(mscorlibAssemblyDefinition, typeof(IntPtr), tr => "I");
            DoTypeSignatureAsStringTest(mscorlibAssemblyDefinition, typeof(UIntPtr), tr => "U");
            DoTypeSignatureAsStringTest(mscorlibAssemblyDefinition, typeof(void), tr => "VOID");
            DoTypeSignatureAsStringTest(mscorlibAssemblyDefinition, typeof(bool), tr => "BOOLEAN");
            DoTypeSignatureAsStringTest(mscorlibAssemblyDefinition, typeof(char), tr => "CHAR");
            DoTypeSignatureAsStringTest(mscorlibAssemblyDefinition, typeof(byte), tr => "U1");
            DoTypeSignatureAsStringTest(mscorlibAssemblyDefinition, typeof(sbyte), tr => "I1");
            DoTypeSignatureAsStringTest(mscorlibAssemblyDefinition, typeof(ushort), tr => "U2");
            DoTypeSignatureAsStringTest(mscorlibAssemblyDefinition, typeof(short), tr => "I2");
            DoTypeSignatureAsStringTest(mscorlibAssemblyDefinition, typeof(uint), tr => "U4");
            DoTypeSignatureAsStringTest(mscorlibAssemblyDefinition, typeof(int), tr => "I4");
            DoTypeSignatureAsStringTest(mscorlibAssemblyDefinition, typeof(ulong), tr => "U8");
            DoTypeSignatureAsStringTest(mscorlibAssemblyDefinition, typeof(long), tr => "I8");
            DoTypeSignatureAsStringTest(mscorlibAssemblyDefinition, typeof(float), tr => "R4");
            DoTypeSignatureAsStringTest(mscorlibAssemblyDefinition, typeof(double), tr => "R8");
            DoTypeSignatureAsStringTest(mscorlibAssemblyDefinition, typeof(string), tr => "STRING");
            DoTypeSignatureAsStringTest(mscorlibAssemblyDefinition, typeof(TimeSpan), tr => $"VALUETYPE [{tr.MetadataToken.ToInt32().ToString("x8")}]");
            DoTypeSignatureAsStringTest(mscorlibAssemblyDefinition, typeof(DateTime), tr => $"VALUETYPE [{tr.MetadataToken.ToInt32().ToString("x8")}]");
            DoTypeSignatureAsStringTest(mscorlibAssemblyDefinition, typeof(object), tr => "OBJECT");
            DoTypeSignatureAsStringTest(mscorlibAssemblyDefinition, typeof(System.IO.Path), tr => $"CLASS [{tr.MetadataToken.ToInt32().ToString("x8")}]");
            DoTypeSignatureAsStringTest(mscorlibAssemblyDefinition, typeof(int[]), tr => $"SZARRAY {tr.GetElementType().TypeSignatureAsString()}");
            DoTypeSignatureAsStringTest(mscorlibAssemblyDefinition, typeof(WeakReference), tr => $"CLASS [{tr.MetadataToken.ToInt32().ToString("x8")}]");
            DoTypeSignatureAsStringTest(mscorlibAssemblyDefinition, typeof(ICloneable), tr => $"CLASS [{tr.MetadataToken.ToInt32().ToString("x8")}]");
        }

        private void DoTypeSignatureAsStringTest(Mono.Cecil.AssemblyDefinition mscorlibAssemblyDefinition, Type type, Func<Mono.Cecil.TypeReference, string> expectedResultFunc)
        {
            var typeReference = mscorlibAssemblyDefinition.MainModule.ImportReference(type);
            Assert.IsNotNull(typeReference);

            // test
            var r = typeReference.TypeSignatureAsString();

            var expectedResult = expectedResultFunc(typeReference);
            Assert.AreEqual(expectedResult, r);
        }

        [TestMethod]
        public void ToNativeTypeAsStringTest()
        {
            var mscorlibAssemblyDefinition = TestObjectHelper.GetmscorlibAssemblyDefinition();

            DoToNativeTypeAsStringTest(mscorlibAssemblyDefinition, typeof(IntPtr), tr => "int32_t");
            DoToNativeTypeAsStringTest(mscorlibAssemblyDefinition, typeof(UIntPtr), tr => "uint32_t");
            DoToNativeTypeAsStringTest(mscorlibAssemblyDefinition, typeof(void), tr => "void");
            DoToNativeTypeAsStringTest(mscorlibAssemblyDefinition, typeof(bool), tr => "bool");
            DoToNativeTypeAsStringTest(mscorlibAssemblyDefinition, typeof(char), tr => "char");
            DoToNativeTypeAsStringTest(mscorlibAssemblyDefinition, typeof(byte), tr => "uint8_t");
            DoToNativeTypeAsStringTest(mscorlibAssemblyDefinition, typeof(sbyte), tr => "int8_t");
            DoToNativeTypeAsStringTest(mscorlibAssemblyDefinition, typeof(ushort), tr => "uint16_t");
            DoToNativeTypeAsStringTest(mscorlibAssemblyDefinition, typeof(short), tr => "int16_t");
            DoToNativeTypeAsStringTest(mscorlibAssemblyDefinition, typeof(uint), tr => "uint32_t");
            DoToNativeTypeAsStringTest(mscorlibAssemblyDefinition, typeof(int), tr => "int32_t");
            DoToNativeTypeAsStringTest(mscorlibAssemblyDefinition, typeof(ulong), tr => "uint64_t");
            DoToNativeTypeAsStringTest(mscorlibAssemblyDefinition, typeof(long), tr => "int64_t");
            DoToNativeTypeAsStringTest(mscorlibAssemblyDefinition, typeof(float), tr => "float");
            DoToNativeTypeAsStringTest(mscorlibAssemblyDefinition, typeof(double), tr => "double");
            DoToNativeTypeAsStringTest(mscorlibAssemblyDefinition, typeof(string), tr => "const char*");
            DoToNativeTypeAsStringTest(mscorlibAssemblyDefinition, typeof(TimeSpan), tr => "UNSUPPORTED");
            DoToNativeTypeAsStringTest(mscorlibAssemblyDefinition, typeof(DateTime), tr => "UNSUPPORTED");
            DoToNativeTypeAsStringTest(mscorlibAssemblyDefinition, typeof(object), tr => "UNSUPPORTED");
            DoToNativeTypeAsStringTest(mscorlibAssemblyDefinition, typeof(System.IO.Path), tr => "UNSUPPORTED");
            DoToNativeTypeAsStringTest(mscorlibAssemblyDefinition, typeof(int[]), tr => $"CLR_RT_TypedArray_{tr.GetElementType().ToCLRTypeAsString()}");
            DoToNativeTypeAsStringTest(mscorlibAssemblyDefinition, typeof(WeakReference), tr => "UNSUPPORTED");
            DoToNativeTypeAsStringTest(mscorlibAssemblyDefinition, typeof(ICloneable), tr => "UNSUPPORTED");
        }

        private void DoToNativeTypeAsStringTest(Mono.Cecil.AssemblyDefinition mscorlibAssemblyDefinition, Type type, Func<Mono.Cecil.TypeReference, string> expectedResultFunc)
        {
            var typeReference = mscorlibAssemblyDefinition.MainModule.ImportReference(type);
            Assert.IsNotNull(typeReference);

            // test
            var r = typeReference.ToNativeTypeAsString();

            var expectedResult = expectedResultFunc(typeReference);
            Assert.AreEqual(expectedResult, r);
        }


        [TestMethod]
        public void ToCLRTypeAsStringTest()
        {
            var mscorlibAssemblyDefinition = TestObjectHelper.GetmscorlibAssemblyDefinition();

            DoToCLRTypeAsStringTest(mscorlibAssemblyDefinition, typeof(IntPtr), tr => "INT32");
            DoToCLRTypeAsStringTest(mscorlibAssemblyDefinition, typeof(UIntPtr), tr => "UINT32");
            DoToCLRTypeAsStringTest(mscorlibAssemblyDefinition, typeof(void), tr => "void");
            DoToCLRTypeAsStringTest(mscorlibAssemblyDefinition, typeof(bool), tr => "bool");
            DoToCLRTypeAsStringTest(mscorlibAssemblyDefinition, typeof(char), tr => "CHAR");
            DoToCLRTypeAsStringTest(mscorlibAssemblyDefinition, typeof(byte), tr => "UINT8");
            DoToCLRTypeAsStringTest(mscorlibAssemblyDefinition, typeof(sbyte), tr => "INT8");
            DoToCLRTypeAsStringTest(mscorlibAssemblyDefinition, typeof(ushort), tr => "UINT16");
            DoToCLRTypeAsStringTest(mscorlibAssemblyDefinition, typeof(short), tr => "INT16");
            DoToCLRTypeAsStringTest(mscorlibAssemblyDefinition, typeof(uint), tr => "UINT32");
            DoToCLRTypeAsStringTest(mscorlibAssemblyDefinition, typeof(int), tr => "INT32");
            DoToCLRTypeAsStringTest(mscorlibAssemblyDefinition, typeof(ulong), tr => "UINT64");
            DoToCLRTypeAsStringTest(mscorlibAssemblyDefinition, typeof(long), tr => "INT64");
            DoToCLRTypeAsStringTest(mscorlibAssemblyDefinition, typeof(float), tr => "float");
            DoToCLRTypeAsStringTest(mscorlibAssemblyDefinition, typeof(double), tr => "double");
            DoToCLRTypeAsStringTest(mscorlibAssemblyDefinition, typeof(string), tr => "LPCSTR");
            DoToCLRTypeAsStringTest(mscorlibAssemblyDefinition, typeof(TimeSpan), tr => "UNSUPPORTED");
            DoToCLRTypeAsStringTest(mscorlibAssemblyDefinition, typeof(DateTime), tr => "UNSUPPORTED");
            DoToCLRTypeAsStringTest(mscorlibAssemblyDefinition, typeof(object), tr => "UNSUPPORTED");
            DoToCLRTypeAsStringTest(mscorlibAssemblyDefinition, typeof(System.IO.Path), tr => "UNSUPPORTED");
            DoToCLRTypeAsStringTest(mscorlibAssemblyDefinition, typeof(int[]), tr => $"{tr.GetElementType().ToCLRTypeAsString()}_ARRAY");
            DoToCLRTypeAsStringTest(mscorlibAssemblyDefinition, typeof(WeakReference), tr => "UNSUPPORTED");
            DoToCLRTypeAsStringTest(mscorlibAssemblyDefinition, typeof(ICloneable), tr => "UNSUPPORTED");
        }

        private void DoToCLRTypeAsStringTest(Mono.Cecil.AssemblyDefinition mscorlibAssemblyDefinition, Type type, Func<Mono.Cecil.TypeReference, string> expectedResultFunc)
        {
            var typeReference = mscorlibAssemblyDefinition.MainModule.ImportReference(type);
            Assert.IsNotNull(typeReference);

            // test
            var r = typeReference.ToCLRTypeAsString();

            var expectedResult = expectedResultFunc(typeReference);
            Assert.AreEqual(expectedResult, r);
        }

    }
}
