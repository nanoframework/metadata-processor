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
        private NativeContract _stubsAppContract;

        private const string StubsAppLibraryPrefix = "Library_StubsGenerationTestNFApp_StubsGenerationTestNFApp_";

        private string ReadGeneratedFile(string fileName)
        {
            // normalize line endings
            return File.ReadAllText(Path.Combine(_stubsPath, fileName)).Replace("\r\n", "\n");
        }

        private static string GetBlock(string content, string startMarker)
        {
            int start = content.IndexOf(startMarker, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, $"Can't find '{startMarker}'");

            int end = content.IndexOf("};", start, StringComparison.Ordinal);
            Assert.IsTrue(end > start, $"Can't find end of block '{startMarker}'");

            return content.Substring(start, end - start + 2);
        }

        [TestMethod]
        public void DenseMethodLookupTableTest()
        {
            string lookupFile = ReadGeneratedFile("StubsGenerationTestNFApp.cpp");
            string methodLookup = GetBlock(lookupFile, "static const CLR_RT_MethodHandler method_lookup[] =");

            Assert.IsFalse(methodLookup.Contains("nullptr"), "Method lookup table must not have nullptr entries");

            List<string> entries = Regex.Matches(methodLookup, @"^\s{4}(\w+::\w+),$", RegexOptions.Multiline)
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .ToList();

            // one entry per native method, in native slot order
            List<string> expected = _stubsAppContract.Methods
                .Select(m => $"Library_StubsGenerationTestNFApp_{m.SafeClassName}::{m.SafeMethodName}")
                .ToList();

            CollectionAssert.AreEqual(expected, entries);

            // and sorted canonically
            CollectionAssert.AreEqual(entries.OrderBy(e => e, StringComparer.Ordinal).ToList(), entries);
        }

        [TestMethod]
        public void NativeAssemblyDataInitializerTest()
        {
            string lookupFile = ReadGeneratedFile("StubsGenerationTestNFApp.cpp");

            string expectedInitializer =
                "const CLR_RT_NativeAssemblyData g_CLR_AssemblyNative_testStubs =\n" +
                "{\n" +
                "    \"StubsGenerationTestNFApp\",\n" +
                $"    0x{_stubsAppContract.Hash:X8},\n" +
                "    method_lookup,\n" +
                "    ARRAYSIZE(method_lookup)\n" +
                "};";

            Assert.IsTrue(lookupFile.Contains(expectedInitializer), $"Initializer not found. Generated file:\n{lookupFile}");
            Assert.AreNotEqual(0u, _stubsAppContract.Hash);

            Assert.IsFalse(lookupFile.Contains("layout_guards"));
        }

        [TestMethod]
        public void HeaderFieldConstantsAreValidIdentifiersTest()
        {
            string headerFile = ReadGeneratedFile("StubsGenerationTestNFApp.h");

            // renamed fields
            Assert.IsTrue(headerFile.Contains("    // renamed backing field '<StubsGenerationTestNFApp.IFoo.Bar>k__BackingField'\n    static const int FIELD__IFoo_Bar = 1;"));
            Assert.IsTrue(headerFile.Contains("    static const int FIELD__IGen_of_Int32_Item = 2;"));
            Assert.IsTrue(headerFile.Contains("    static const int FIELD__IDict_of_String_Int32_Item = 3;"));
            Assert.IsTrue(headerFile.Contains("    static const int FIELD__class = 4;"));
            Assert.IsTrue(headerFile.Contains("    // renamed primary constructor parameter field '<width>P'\n    static const int FIELD__width = 1;"));

            // delegate cache container isn't there
            Assert.IsFalse(headerFile.Contains("SWork"));

            // every declared constant is a valid and unique (per struct) identifier
            foreach (string structBody in Regex.Matches(headerFile, @"^struct \w+\n\{\n(.*?)^\};", RegexOptions.Multiline | RegexOptions.Singleline)
                .Cast<Match>()
                .Select(m => m.Groups[1].Value))
            {
                List<string> constants = Regex.Matches(structBody, @"^\s*static const int (.+?) = \d+;$", RegexOptions.Multiline)
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value)
                    .ToList();

                foreach (string constant in constants)
                {
                    Assert.IsTrue(Regex.IsMatch(constant, @"^[A-Za-z_][\p{L}\p{Nd}_]*$"), $"'{constant}' isn't a valid identifier");
                }

                Assert.AreEqual(constants.Count, constants.Distinct(StringComparer.Ordinal).Count(), "Duplicate constant in struct");
            }

            // no line declares anything with characters not valid in identifiers
            Assert.IsFalse(Regex.IsMatch(headerFile, @"^\s*static const int [^=]*[<>.`,][^=]* =", RegexOptions.Multiline));
        }

        [TestMethod]
        public void FieldConstantsForAllClassesTest()
        {
            string headerFile = ReadGeneratedFile("StubsGenerationTestNFApp.h");

            // type with native methods and fields
            Assert.IsTrue(headerFile.Contains($"struct {StubsAppLibraryPrefix}NativeWithFields\n"));
            Assert.IsTrue(headerFile.Contains("    static const int FIELD_STATIC__s_counter = "));
            Assert.IsTrue(headerFile.Contains("    static const int FIELD___value = 1;"));
            Assert.IsTrue(headerFile.Contains("    static const int FIELD___flag = 2;"));
            Assert.IsTrue(headerFile.Contains("    NANOCLR_NATIVE_DECLARE(NativeGetValue___I4);"));

            // managed-only types get field constants
            Assert.IsTrue(headerFile.Contains($"struct {StubsAppLibraryPrefix}ManagedOnlyWithFields\n"));
            Assert.IsTrue(headerFile.Contains("    static const int FIELD___managedValue = 1;"));
            Assert.IsTrue(headerFile.Contains("    static const int FIELD___managedName = 2;"));
            Assert.IsTrue(headerFile.Contains($"struct {StubsAppLibraryPrefix}CompilerGeneratedTypesHost\n"));
            Assert.IsTrue(headerFile.Contains("    static const int FIELD___seed = 1;"));

            // cross-assembly base: own field index comes after inherited ones
            NativeTypeLayout nativeException = _stubsAppContract.Types.Single(t => t.Type.Name == "NativeException");
            Assert.IsTrue(headerFile.Contains($"    static const int FIELD___nativeErrorCode = {nativeException.InstanceFields.Single().Index};"));

            // compiler-generated types (closure display classes) don't get a declaration
            Assert.IsFalse(headerFile.Contains("DisplayClass"));
            Assert.IsFalse(headerFile.Contains("__this"));

            // types without fields nor native methods don't get a declaration
            Assert.IsFalse(headerFile.Contains($"struct {StubsAppLibraryPrefix}Program\n"));

            // every struct in the header is a contract type with content
            List<string> structs = Regex.Matches(headerFile, @"^struct (\w+)$", RegexOptions.Multiline)
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .ToList();

            List<string> expected = _stubsAppContract.Types
                .Where(t => t.HasNativeMethods || t.StaticFields.Count > 0 || t.InstanceFields.Count > 0)
                .Select(t => $"Library_StubsGenerationTestNFApp_{t.SafeClassName}")
                .ToList();

            CollectionAssert.AreEquivalent(expected, structs);
        }

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
        [Ignore("TestNFClassLibrary.dll available to the tests is built without MDP_UNIT_TESTS_BUILD (the build with it is overwritten by the TestNFApp build), so it has no native methods and no stubs are generated for it. Pre-existing test infrastructure issue.")]
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
        [Ignore("TestNFClassLibrary.dll available to the tests is built without MDP_UNIT_TESTS_BUILD (the build with it is overwritten by the TestNFApp build), so it has no native methods and no stubs are generated for it. Pre-existing test infrastructure issue.")]
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

            nanoAssemblyBuilder assemblyBuilder = new nanoAssemblyBuilder(assemblyDefinition, false);

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
            _stubsAppContract = tablesContext.NativeContract;

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

            // save types that are to be included from assembly lookup declaration (types declaring native methods)
            foreach (string safeClassName in tablesContext.NativeContract.Methods.Select(m => m.SafeClassName).Distinct())
            {
                _nfTestLibTypeToIncludeInLookupTable.Add($"Library_{skeletonGenerator.SafeProjectName}_{safeClassName}");
            }

            // save types that are to be included in assembly header (types with field constants or native methods)
            foreach (NativeTypeLayout type in tablesContext.NativeContract.Types)
            {
                if (type.HasNativeMethods
                    || type.StaticFields.Count > 0
                    || type.InstanceFields.Count > 0)
                {
                    _nfTestLibTypeToIncludeInHeader.Add($"Library_{skeletonGenerator.SafeProjectName}_{type.SafeClassName}");
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
