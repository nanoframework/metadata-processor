//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

        uint8_t *param0;
        uint8_t heapblock0[CLR_RT_HEAP_BLOCK_SIZE];
        NANOCLR_CHECK_HRESULT( Interop_Marshal_UINT8_ByRef( stack, heapblock0, 1, param0 ) );

        uint16_t *param1;
        uint8_t heapblock1[CLR_RT_HEAP_BLOCK_SIZE];
        NANOCLR_CHECK_HRESULT( Interop_Marshal_UINT16_ByRef( stack, heapblock1, 2, param1 ) );

        NativeMethodGeneration::NativeMethodWithReferenceParameters( *param0, *param1, hr );
        NANOCLR_CHECK_HRESULT( hr );

    }
    NANOCLR_NOCLEANUP();
}";

        private const string NativeHeaderMethodGenerationDeclaration =
            "static void NativeMethodWithReferenceParameters( uint8_t& param0, uint16_t& param1, HRESULT &hr );";

        private string _stubsPath;
        private List<string> _nfTestLibTypeToIncludeInLookupTable = new List<string>();
        private List<string> _nfTestLibTypeToIncludeInHeader = new List<string>();

        [TestMethod]
        public void GeneratingStubsFromNFAppTest()
        {
            // read generated stub file and look for the function declaration
            var generatedFile =
                File.ReadAllText(
                    $"{_stubsPath}\\StubsGenerationTestNFApp_StubsGenerationTestNFApp_NativeMethodGeneration.cpp");

            Assert.IsTrue(generatedFile.Contains(NativeMethodGenerationDeclaration));
        }

        [TestMethod]
        public void GeneratingMarshallingStubsFromNFAppTest()
        {
            var generatedFile =
                File.ReadAllText(
                    $"{_stubsPath}\\StubsGenerationTestNFApp_StubsGenerationTestNFApp_NativeMethodGeneration_mshl.cpp");

            Assert.IsTrue(generatedFile.Contains(NativeMarshallingMethodGenerationDeclaration));
        }

        [TestMethod]
        public void GeneratingHeaderStubsFromNFAppTest()
        {
            var generatedFile =
                File.ReadAllText(
                    $"{_stubsPath}\\StubsGenerationTestNFApp_StubsGenerationTestNFApp_NativeMethodGeneration.h");

            Assert.IsTrue(generatedFile.Contains(NativeHeaderMethodGenerationDeclaration));
        }

        private const string StaticMethodWithoutParameterHeaderGeneration =
            @"static void NativeStaticMethod(  HRESULT &hr );";
        private const string StaticMethodWithoutParameterMarshallGeneration =
            @"HRESULT Library_StubsGenerationTestNFApp_StubsGenerationTestNFApp_NativeMethodGeneration::NativeStaticMethod___STATIC__VOID( CLR_RT_StackFrame& stack )
{
    NANOCLR_HEADER(); hr = S_OK;
    {

        NativeMethodGeneration::NativeStaticMethod(  hr );
        NANOCLR_CHECK_HRESULT( hr );

    }
    NANOCLR_NOCLEANUP();
}";
        private const string StaticMethodWithoutParameterImplementationGeneration =
            @"void NativeMethodGeneration::NativeStaticMethod(  HRESULT &hr )
{

    (void)hr;


    ////////////////////////////////
    // implementation starts here //


    // implementation ends here   //
    ////////////////////////////////


}";

        [TestMethod]
        public void GeneratingStaticMethodWithoutParams()
        {
            var generatedHeaderFile =
                File.ReadAllText(
                    $"{_stubsPath}\\StubsGenerationTestNFApp_StubsGenerationTestNFApp_NativeMethodGeneration.h");

            var generatedMarshallFile =
                File.ReadAllText(
                    $"{_stubsPath}\\StubsGenerationTestNFApp_StubsGenerationTestNFApp_NativeMethodGeneration_mshl.cpp");

            var generatedImplementationFile =
                File.ReadAllText(
                    $"{_stubsPath}\\StubsGenerationTestNFApp_StubsGenerationTestNFApp_NativeMethodGeneration.cpp");

            Assert.IsTrue(generatedHeaderFile.Contains(StaticMethodWithoutParameterHeaderGeneration));
            Assert.IsTrue(generatedMarshallFile.Contains(StaticMethodWithoutParameterMarshallGeneration));
            Assert.IsTrue(generatedImplementationFile.Contains(StaticMethodWithoutParameterImplementationGeneration));
        }

        private const string StaticMethodHeaderGeneration =
            @"static uint8_t NativeStaticMethodReturningByte( char param0, HRESULT &hr );";
        private const string StaticMethodMarshallGeneration =
            @"HRESULT Library_StubsGenerationTestNFApp_StubsGenerationTestNFApp_NativeMethodGeneration::NativeStaticMethodReturningByte___STATIC__U1__CHAR( CLR_RT_StackFrame& stack )
{
    NANOCLR_HEADER(); hr = S_OK;
    {

        char param0;
        NANOCLR_CHECK_HRESULT( Interop_Marshal_CHAR( stack, 0, param0 ) );

        uint8_t retValue = NativeMethodGeneration::NativeStaticMethodReturningByte( param0, hr );
        NANOCLR_CHECK_HRESULT( hr );
        SetResult_UINT8( stack, retValue );
    }
    NANOCLR_NOCLEANUP();
}";
        private const string StaticMethodImplementationGeneration =
            @"uint8_t NativeMethodGeneration::NativeStaticMethodReturningByte( char param0, HRESULT &hr )
{

    (void)param0;
    (void)hr;
    uint8_t retValue = 0;

    ////////////////////////////////
    // implementation starts here //


    // implementation ends here   //
    ////////////////////////////////

    return retValue;
}";

        [TestMethod]
        public void GeneratingStaticMethod()
        {
            var generatedHeaderFile =
                File.ReadAllText(
                    $"{_stubsPath}\\StubsGenerationTestNFApp_StubsGenerationTestNFApp_NativeMethodGeneration.h");

            var generatedMarshallFile =
                File.ReadAllText(
                    $"{_stubsPath}\\StubsGenerationTestNFApp_StubsGenerationTestNFApp_NativeMethodGeneration_mshl.cpp");

            var generatedImplementationFile =
                File.ReadAllText(
                    $"{_stubsPath}\\StubsGenerationTestNFApp_StubsGenerationTestNFApp_NativeMethodGeneration.cpp");

            Assert.IsTrue(generatedHeaderFile.Contains(StaticMethodHeaderGeneration));
            Assert.IsTrue(generatedMarshallFile.Contains(StaticMethodMarshallGeneration));
            Assert.IsTrue(generatedImplementationFile.Contains(StaticMethodImplementationGeneration));
        }

        [TestMethod]
        public void BackingFieldsAbsentTests()
        {
            string generatedAssemblyHeaderFile =
                File.ReadAllText(
                    $"{_stubsPath}\\TestNFClassLibrary.h");

            // check for property with backing field patter in the name
            Assert.IsFalse(generatedAssemblyHeaderFile.Contains("k__BackingField ="), "Found a name with BackingField pattern, when it shouldn't");

            // deep check for backing field name pattern (except for entry patter in comments)
            Assert.IsFalse(Regex.IsMatch(generatedAssemblyHeaderFile, @"(?<!')<\w+>k__BackingField(?!')"), "Found a name with BackingField pattern, when it shouldn't");
        }

        [TestMethod]
        public void StubsAndDeclarationMatchTests()
        {
            string generatedAssemblyHeaderFile = File.ReadAllText($"{_stubsPath}\\TestNFClassLibrary.h");
            string generatedAssemblyLookupFile = File.ReadAllText($"{_stubsPath}\\TestNFClassLibrary.cpp");

            // extract all type definitions from the header file
            MatchCollection typeDefinitionsInHeader = Regex.Matches(generatedAssemblyHeaderFile, @"struct\s{1}(\w+_\w+_\w+_\w+)\b", RegexOptions.IgnoreCase | RegexOptions.Multiline);

            // extract all type definitions from the lookup file
            List<Match> typeDefinitionsInLookupTable = Regex.Matches(generatedAssemblyLookupFile, @"^\s{4}(\w+_\w+_\w+_\w+)::", RegexOptions.IgnoreCase | RegexOptions.Multiline)
                .Cast<Match>()
                .GroupBy(m => m.Groups[1].Value)
                .Select(g => g.First())
                .ToList();

            // check if all entries in lookup table are present in the header
            foreach (Match typeDefinition in typeDefinitionsInLookupTable)
            {
                string typeName = typeDefinition.Groups[1].Value;
                bool found = typeDefinitionsInHeader.Cast<Match>().Any(md => md.Groups[1].Value == typeName);
                Assert.IsTrue(found, $"Type definition {typeName} not found in header file");
            }

            // check if all expected types are present in the lookup table
            Assert.AreEqual(_nfTestLibTypeToIncludeInLookupTable.Count, typeDefinitionsInLookupTable.Count, "Number of type definitions don't match");

            foreach (string typeName in _nfTestLibTypeToIncludeInLookupTable)
            {
                bool found = typeDefinitionsInLookupTable.Any(md => md.Groups[1].Value == typeName);
                Assert.IsTrue(found, $"Type definition {typeName} not found in lookup table");
            }

            // check if all expected types are present in the header file
            Assert.AreEqual(_nfTestLibTypeToIncludeInHeader.Count, typeDefinitionsInHeader.Count, "Number of type definitions don't match");

            foreach (string typeName in _nfTestLibTypeToIncludeInHeader)
            {
                bool found = typeDefinitionsInHeader.Cast<Match>().Any(md => md.Groups[1].Value == typeName);
                Assert.IsTrue(found, $"Type definition {typeName} not found in header file");
            }
        }

        [TestInitialize]
        public void GenerateStubs()
        {
            var loadHints = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["mscorlib"] = Path.Combine(Directory.GetParent(TestObjectHelper.StubsGenerationNFAppFullPath).FullName,
                    "mscorlib.dll")
            };

            // class names to exclude from processing
            var classNamesToExclude = new List<string>
            {
                "THIS_NAME_DOES_NOT_EXIST_IN_THE_PROJECT"
            };

            // Conpile StubsGenerationNFApp
            string stubsGenerationFileToParse = TestObjectHelper.StubsGenerationNFAppFullPath;
            string stubsGenerationFileToCompile = Path.ChangeExtension(stubsGenerationFileToParse, "pe");

            // get path where stubs will be generated
            _stubsPath = Path.Combine(
                TestObjectHelper.TestExecutionLocation,
                "Stubs");

            AssemblyDefinition assemblyDefinition = AssemblyDefinition.ReadAssembly(
                stubsGenerationFileToParse,
                new ReaderParameters { AssemblyResolver = new LoadHintsAssemblyResolver(loadHints) });

            nanoAssemblyBuilder assemblyBuilder = new nanoAssemblyBuilder(assemblyDefinition, classNamesToExclude, false);

            using (FileStream stream = File.Open(
                       Path.ChangeExtension(stubsGenerationFileToCompile, "tmp"),
                       FileMode.Create,
                       FileAccess.ReadWrite))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                assemblyBuilder.Write(GetBinaryWriter(writer));
            }

            // OK to delete tmp PE file
            File.Delete(Path.ChangeExtension(stubsGenerationFileToCompile, "tmp"));

            assemblyBuilder.Minimize();

            // recompile
            using (FileStream stream = File.Open(
                     Path.ChangeExtension(stubsGenerationFileToCompile, "tmp"),
                     FileMode.Create,
                     FileAccess.ReadWrite))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                assemblyBuilder.Write(GetBinaryWriter(writer));
            }

            nanoTablesContext tablesContext = assemblyBuilder.TablesContext;

            var skeletonGenerator = new nanoSkeletonGenerator(
                tablesContext,
                _stubsPath,
                "testStubs",
                "StubsGenerationTestNFApp",
                false,
                false);

            skeletonGenerator.GenerateSkeleton();

            // Compile the TestNFClassLibrary
            string nfLibFileToParse = TestObjectHelper.TestNFClassLibFullPath;
            string nfLibFileToCompile = Path.ChangeExtension(nfLibFileToParse, "pe");

            assemblyDefinition = AssemblyDefinition.ReadAssembly(
                nfLibFileToParse,
                new ReaderParameters { AssemblyResolver = new LoadHintsAssemblyResolver(loadHints) });

            assemblyBuilder = new nanoAssemblyBuilder(
                assemblyDefinition,
                new List<string>(),
                false);

            using (FileStream stream = File.Open(
                       Path.ChangeExtension(nfLibFileToCompile, "tmp"),
                       FileMode.Create,
                       FileAccess.ReadWrite))

            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                assemblyBuilder.Write(GetBinaryWriter(writer));
            }

            // OK to delete tmp PE file
            File.Delete(Path.ChangeExtension(nfLibFileToCompile, "tmp"));

            assemblyBuilder.Minimize();

            // recompile
            using (FileStream stream = File.Open(
                  Path.ChangeExtension(nfLibFileToCompile, "tmp"),
                  FileMode.Create,
                  FileAccess.ReadWrite))

            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                assemblyBuilder.Write(GetBinaryWriter(writer));
            }

            tablesContext = assemblyBuilder.TablesContext;

            skeletonGenerator = new nanoSkeletonGenerator(
                tablesContext,
                _stubsPath,
                "testStubs",
                "TestNFClassLibrary",
                true,
                true);

            skeletonGenerator.GenerateSkeleton();

            // save types that are to be included from assembly lookup declaration
            foreach (TypeDefinition c in tablesContext.TypeDefinitionTable.Items)
            {
                if (c.HasMethods && nanoSkeletonGenerator.ShouldIncludeType(c))
                {
                    foreach (MethodDefinition m in nanoTablesContext.GetOrderedMethods(c.Methods))
                    {
                        ushort rva = tablesContext.ByteCodeTable.GetMethodRva(m);

                        // check method inclusion
                        // method is not a native implementation (RVA 0xFFFF) and is not abstract
                        if (rva == 0xFFFF && !m.IsAbstract)
                        {
                            _nfTestLibTypeToIncludeInLookupTable.Add($"Library_{skeletonGenerator.SafeProjectName}_{NativeMethodsCrc.GetClassName(c)}");

                            // only need to add the type once
                            break;
                        }
                    }
                }
            }

            // save types that are to be included in assembly header
            foreach (TypeDefinition c in tablesContext.TypeDefinitionTable.Items)
            {
                if (nanoSkeletonGenerator.ShouldIncludeType(c)
                    && c.HasMethods
                    && c.HasFields)
                {
                    _nfTestLibTypeToIncludeInHeader.Add($"Library_{skeletonGenerator.SafeProjectName}_{NativeMethodsCrc.GetClassName(c)}");
                }
            }
        }

        [TestCleanup]
        public void DeleteStubs()
        {
            Directory.Delete(_stubsPath, true);
        }

        private nanoBinaryWriter GetBinaryWriter(
            BinaryWriter writer)
        {
            return nanoBinaryWriter.CreateLittleEndianBinaryWriter(writer);
        }
    }
}
