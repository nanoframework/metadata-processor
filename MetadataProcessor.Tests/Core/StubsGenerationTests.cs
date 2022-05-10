//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using nanoFramework.Tools.MetadataProcessor.Core;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core
{
    [TestClass]
    public class StubsGenerationTests
    {
        private const string NativeMethodGenerationDeclaration =
            @"void NativeMethodGeneration::NativeMethodWithReferenceParameters( uint8_t& param0, uint16_t& param1, HRESULT &hr )
{

    (void)param0;
    (void)param1;
    (void)hr;


    ////////////////////////////////
    // implementation starts here //


    // implementation ends here   //
    ////////////////////////////////


}";

        private const string NativeMarshallingMethodGenerationDeclaration =
            @"HRESULT Library_StubsGenerationTestNFApp_StubsGenerationTestNFApp_NativeMethodGeneration::NativeMethodWithReferenceParameters___VOID__BYREF_U1__BYREF_U2( CLR_RT_StackFrame& stack )
{
    NANOCLR_HEADER(); hr = S_OK;
    {

        uint8_t& param0 = 0x0;
        uint8_t heapblock0[CLR_RT_HEAP_BLOCK_SIZE];
        NANOCLR_CHECK_HRESULT( Interop_Marshal_UINT8_ByRef( stack, heapblock0, param0 ) );

        uint16_t& param1 = 0x0;
        uint8_t heapblock1[CLR_RT_HEAP_BLOCK_SIZE];
        NANOCLR_CHECK_HRESULT( Interop_Marshal_UINT16_ByRef( stack, heapblock1, param1 ) );

        NativeMethodGeneration::NativeMethodWithReferenceParameters( param0, param1, hr );
        NANOCLR_CHECK_HRESULT( hr );

    }
    NANOCLR_NOCLEANUP();
}";

        private string stubPath;

        [TestMethod]
        public void GeneratingStubsFromNFAppTest()
        {
            // read generated stub file and look for the function declaration
            var generatedFile =
                File.ReadAllText(
                    $"{stubPath}\\StubsGenerationTestNFApp_StubsGenerationTestNFApp_NativeMethodGeneration.cpp");

            Assert.IsTrue(generatedFile.Contains(NativeMethodGenerationDeclaration));
        }

        [TestMethod]
        public void GeneratingMarshallingStubsFromNFAppTest()
        {
            var generatedFile =
                File.ReadAllText(
                    $"{stubPath}\\StubsGenerationTestNFApp_StubsGenerationTestNFApp_NativeMethodGeneration_mshl.cpp");

            Assert.IsTrue(generatedFile.Contains(NativeMarshallingMethodGenerationDeclaration));
        }

        [TestInitialize]
        public void GenerateStubs()
        {
            var loadHints = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["mscorlib"] = Path.Combine(Directory.GetParent(TestObjectHelper.GenerationNFAppFullPath).FullName,
                    "mscorlib.dll")
            };

            // class names to exclude from processing
            var classNamesToExclude = new List<string>
            {
                "THIS_NAME_DOES_NOT_EXIST_IN_THE_PROJECT"
            };

            var fileToParse = TestObjectHelper.GenerationNFAppFullPath;
            var fileToCompile = Path.ChangeExtension(fileToParse, "pe");

            // get path where stubs will be generated
            stubPath = Path.Combine(
                TestObjectHelper.TestExecutionLocation,
                "Stubs");

            var assemblyDefinition = AssemblyDefinition.ReadAssembly(
                fileToParse,
                new ReaderParameters { AssemblyResolver = new LoadHintsAssemblyResolver(loadHints) });

            var assemblyBuilder = new nanoAssemblyBuilder(assemblyDefinition, classNamesToExclude, false);

            using (var stream = File.Open(
                       Path.ChangeExtension(fileToCompile, "tmp"),
                       FileMode.Create,
                       FileAccess.ReadWrite))
            using (var writer = new BinaryWriter(stream))
            {
                assemblyBuilder.Write(GetBinaryWriter(writer));
            }

            // OK to delete tmp PE file
            File.Delete(Path.ChangeExtension(fileToCompile, "tmp"));

            assemblyBuilder.Minimize();

            var tablesContext = assemblyBuilder.TablesContext;

            var skeletonGenerator = new nanoSkeletonGenerator(
                tablesContext,
                stubPath,
                "testStubs",
                "StubsGenerationTestNFApp",
                false,
                false);

            skeletonGenerator.GenerateSkeleton();
        }

        [TestCleanup]
        public void DeleteStubs()
        {
            Directory.Delete(stubPath, true);
        }

        private nanoBinaryWriter GetBinaryWriter(
            BinaryWriter writer)
        {
            return nanoBinaryWriter.CreateLittleEndianBinaryWriter(writer);
        }
    }
}