//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace nanoFramework.Tools.MetadataProcessor.Tests
{
    public static class TestObjectHelper
    {
        private const string _varNameForLocalNanoCLRInstancePath = "MDP_TEST_NANOCLR_INSTANCE_PATH";
        private static string _testExecutionLocation;
        private static string _testNFAppLocation;
        private static string _nfAppProjectLocation;
        private static string _testNFClassLibLocation;
        private static string _nanoClrLocation;
        private static string _configuration;
        private static string _StubsGenerationTestNFAppLocation;

        public static nanoTablesContext GetTestNFAppNanoTablesContext()
        {
            nanoTablesContext ret = null;

            var assemblyDefinition = GetTestNFAppAssemblyDefinition();

            ret = new nanoTablesContext(
                assemblyDefinition,
                null,
                new List<string>(),
                null,
                false,
                false,
                false);

            return ret;
        }

        public static string Configuration => _configuration;

        public static string TestExecutionLocation
        {
            get
            {
                if (string.IsNullOrEmpty(_testExecutionLocation))
                {
                    _testExecutionLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

                    if (_testExecutionLocation.Contains("Debug"))
                    {
                        _configuration = "Debug";
                    }
                    else if (_testExecutionLocation.Contains("Release"))
                    {
                        _configuration = "Release";
                    }
                    else
                    {
                        throw new InvalidOperationException("Expecting configuration to be 'Debug' or 'Release'");
                    }
                }

                return _testExecutionLocation;
            }
        }

        public static string TestNFAppFullPath
        {
            get
            {
                return Path.Combine(
                        TestNFAppLocation,
                        "TestNFApp.exe");
            }
        }

        public static string NFAppFullPath
        {
            get
            {
                return Path.Combine(
                    TestExecutionLocation,
                    "..\\..\\TestNFApp\\bin",
                    _configuration,
                    "TestNFApp.exe");
            }
        }

        public static string TestNFAppLocation
        {
            get
            {
                if (string.IsNullOrEmpty(_testNFAppLocation))
                {
                    _testNFAppLocation = Path.Combine(
                        TestExecutionLocation,
                        "TestNFApp");
                }

                return _testNFAppLocation;
            }
        }

        public static string TestNFClassLibFullPath
        {
            get
            {
                return Path.Combine(
                        TestNFAppLocation,
                        "TestNFClassLibrary.dll");
            }
        }

        public static string TestNFClassLibLocation
        {
            get
            {
                if (string.IsNullOrEmpty(_testNFClassLibLocation))
                {
                    _testNFClassLibLocation = Path.Combine(
                        TestExecutionLocation,
                        "TestNFApp");
                }

                return _testNFClassLibLocation;
            }
        }

        public static string NanoClrLocation
        {
            get
            {
                if (string.IsNullOrEmpty(_nanoClrLocation))
                {
                    _nanoClrLocation = Path.Combine(
                        TestExecutionLocation,
                        "nanoClr",
                        "nanoFramework.nanoCLR.exe");
                }

                return _nanoClrLocation;
            }
        }

        public static string GenerationNFAppFullPath
        {
            get
            {
                return Path.Combine(
                    TestExecutionLocation,
                    "..\\..\\StubsGenerationTestNFApp\\bin",
                    _configuration,
                    "StubsGenerationTestNFApp.exe");
            }
        }

        public static string StubsGenerationTestNFAppFullPath
        {
            get
            {
                return Path.Combine(
                    StubsGenerationTestNFAppLocation,
                    "StubsGenerationTestNFApp.exe");
            }
        }

        public static string StubsGenerationTestNFAppLocation
        {
            get
            {
                if (string.IsNullOrEmpty(_StubsGenerationTestNFAppLocation))
                {
                    _StubsGenerationTestNFAppLocation = Path.Combine(
                        TestExecutionLocation,
                        "StubsGenerationTestNFApp");
                }

                return _StubsGenerationTestNFAppLocation;
            }
        }

        public static AssemblyDefinition GetTestNFAppAssemblyDefinition()
        {
            AssemblyDefinition ret = null;

            ret = AssemblyDefinition.ReadAssembly(TestNFAppFullPath);

            return ret;
        }

        public static AssemblyDefinition GetTestNFAppAssemblyDefinitionWithLoadHints()
        {
            IDictionary<string, string> loadHints = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["mscorlib"] = GetmscorlibAssemblyDefinition().MainModule.FileName,
                ["TestNFClassLibrary"] = GetTestNFClassLibraryDefinition().MainModule.FileName
            };

            return AssemblyDefinition.ReadAssembly(
                TestNFAppFullPath,
                new ReaderParameters { AssemblyResolver = new LoadHintsAssemblyResolver(loadHints) });
        }


        public static AssemblyDefinition GetTestNFClassLibraryDefinition()
        {
            AssemblyDefinition ret = null;

            ret = AssemblyDefinition.ReadAssembly(TestNFClassLibFullPath);

            return ret;
        }

        public static AssemblyDefinition GetmscorlibAssemblyDefinition()
        {
            AssemblyDefinition ret = null;

            var mscorlibAssembly = typeof(System.Object).Assembly;
            ret = AssemblyDefinition.ReadAssembly(mscorlibAssembly.Location);

            return ret;
        }

        public static TypeDefinition GetTestNFAppOneClassOverAllTypeDefinition(AssemblyDefinition assemblyDefinition)
        {
            TypeDefinition ret = null;

            var module = assemblyDefinition.Modules[0];
            ret = module.Types.First(i => i.FullName == "TestNFApp.OneClassOverAll");

            return ret;
        }

        public static TypeDefinition GetTestNFAppOneClassOverAllSubClassTypeDefinition(AssemblyDefinition assemblyDefinition)
        {
            TypeDefinition ret = null;

            var oneClassOverAllTypeDefinition = GetTestNFAppOneClassOverAllTypeDefinition(assemblyDefinition);
            ret = oneClassOverAllTypeDefinition.NestedTypes.First(i => i.Name == "SubClass");

            return ret;
        }

        public static MethodDefinition GetTestNFAppOneClassOverAllDummyMethodDefinition(TypeDefinition oneClassOverAllTypeDefinition)
        {
            MethodDefinition ret = GetTestNFAppOneClassOverAllMethodDefinition(oneClassOverAllTypeDefinition, "DummyMethod");

            return ret;
        }

        public static MethodDefinition GetTestNFAppOneClassOverAllDummyMethodWithRetvalDefinition(TypeDefinition oneClassOverAllTypeDefinition)
        {
            MethodDefinition ret = GetTestNFAppOneClassOverAllMethodDefinition(oneClassOverAllTypeDefinition, "DummyMethodWithRetval");

            return ret;
        }


        public static MethodDefinition GetTestNFAppOneClassOverAllDummyStaticMethodDefinition(TypeDefinition oneClassOverAllTypeDefinition)
        {
            MethodDefinition ret = GetTestNFAppOneClassOverAllMethodDefinition(oneClassOverAllTypeDefinition, "DummyStaticMethod");

            return ret;
        }

        public static MethodDefinition GetTestNFAppOneClassOverAllDummyStaticMethodWithRetvalDefinition(TypeDefinition oneClassOverAllTypeDefinition)
        {
            MethodDefinition ret = GetTestNFAppOneClassOverAllMethodDefinition(oneClassOverAllTypeDefinition, "DummyStaticMethodWithRetval");

            return ret;
        }

        public static MethodDefinition GetTestNFAppOneClassOverAllDummyMethodWithParamsDefinition(TypeDefinition oneClassOverAllTypeDefinition)
        {
            MethodDefinition ret = GetTestNFAppOneClassOverAllMethodDefinition(oneClassOverAllTypeDefinition, "DummyMethodWithParams");

            return ret;
        }

        public static MethodDefinition GetTestNFAppOneClassOverAllDummyStaticMethodWithParamsDefinition(TypeDefinition oneClassOverAllTypeDefinition)
        {
            MethodDefinition ret = GetTestNFAppOneClassOverAllMethodDefinition(oneClassOverAllTypeDefinition, "DummyStaticMethodWithParams");
            return ret;
        }

        public static MethodDefinition GetTestNFAppOneClassOverAllDummyExternMethodDefinition(TypeDefinition oneClassOverAllTypeDefinition)
        {
            MethodDefinition ret = GetTestNFAppOneClassOverAllMethodDefinition(oneClassOverAllTypeDefinition, "DummyExternMethod");
            return ret;
        }

        public static MethodDefinition GetTestNFAppOneClassOverAllDummyMethodWithUglyParamsMethodDefinition(TypeDefinition oneClassOverAllTypeDefinition)
        {
            MethodDefinition ret = GetTestNFAppOneClassOverAllMethodDefinition(oneClassOverAllTypeDefinition, "DummyMethodWithUglyParams");
            return ret;
        }

        public static MethodDefinition GetTestNFAppOneClassOverAllUglyAddMethodDefinition(TypeDefinition oneClassOverAllTypeDefinition)
        {
            MethodDefinition ret = GetTestNFAppOneClassOverAllMethodDefinition(oneClassOverAllTypeDefinition, "UglyAdd");
            return ret;
        }

        public static MethodDefinition GetTestNFAppOneClassOverAllMethodDefinition(TypeDefinition oneClassOverAllTypeDefinition, string methodName)
        {
            MethodDefinition ret = null;

            ret = oneClassOverAllTypeDefinition.Methods.First(i => i.Name == methodName);

            return ret;
        }

        public static FieldDefinition GetTestNFAppOneClassOverAllDummyFieldDefinition(TypeDefinition oneClassOverAllTypeDefinition)
        {
            FieldDefinition ret = oneClassOverAllTypeDefinition.Fields.First(i => i.Name == "dummyField");
            return ret;
        }



        public static Stream GetResourceStream(string resourceName)
        {
            if (String.IsNullOrWhiteSpace(resourceName))
            {
                throw new ArgumentNullException("resourceName");
            }

            Stream ret = null;

            var thisAssembly = System.Reflection.Assembly.GetExecutingAssembly();

            ret = thisAssembly.GetManifestResourceStream(String.Concat(thisAssembly.GetName().Name, ".", resourceName));

            return ret;
        }

        public static byte[] GetResourceStreamContent(string resourceName)
        {
            byte[] ret = null;

            using (var resourceStream = GetResourceStream(resourceName))
            {
                if (resourceStream.Length > int.MaxValue)
                {
                    throw new NotImplementedException($"{resourceStream.Length} bytes");
                }

                using (var rdr = new BinaryReader(resourceStream))
                {
                    ret = rdr.ReadBytes((int)resourceStream.Length);
                }
            }

            return ret;
        }

        public static byte[] DoWithNanoBinaryWriter(Func<BinaryWriter, nanoBinaryWriter> writerCreatorFunc, Action<MemoryStream, BinaryWriter, nanoBinaryWriter> actionToDo)
        {
            byte[] ret = null;

            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms, Encoding.Default, true))
                {
                    var iut = writerCreatorFunc(bw);

                    actionToDo(ms, bw, iut);

                    bw.Flush();

                    ret = ms.ToArray();
                }
            }

            return ret;
        }

        public static void AdjustMethodBodyOffsets(Mono.Cecil.Cil.MethodBody methodBody)
        {
            var offset = 0;
            foreach (var instruction in methodBody.Instructions)
            {
                instruction.Offset = offset;
                offset += instruction.GetSize();
            }
        }

        internal static TypeDefinition GetTestNFAppTestingDelegatesTypeDefinition(AssemblyDefinition assemblyDefinition)
        {
            TypeDefinition ret = null;

            var module = assemblyDefinition.Modules[0];
            ret = module.Types.First(i => i.FullName == "TestNFApp.TestingDelegates");

            return ret;
        }

        internal static TypeDefinition GetTestNFAppTestingDestructorsTypeDefinition(AssemblyDefinition assemblyDefinition)
        {
            TypeDefinition ret = null;

            var module = assemblyDefinition.Modules[0];
            ret = module.Types.First(i => i.FullName == "TestNFApp.TestingDestructors");

            return ret;
        }

        internal static TypeDefinition GetTestNFAppDestructorsTestClassTypeDefinition(AssemblyDefinition assemblyDefinition)
        {
            TypeDefinition ret = null;

            var module = assemblyDefinition.Modules[0];
            ret = module.Types.First(i => i.FullName == "TestNFApp.DestructorsTestClass");

            return ret;
        }

        internal static TypeDefinition GetTestNFAppDestructorsTestAnotherClassBaseTypeDefinition(AssemblyDefinition assemblyDefinition)
        {
            TypeDefinition ret = null;

            var module = assemblyDefinition.Modules[0];
            ret = module.Types.First(i => i.FullName == "TestNFApp.DestructorsTestAnotherClassBase");

            return ret;
        }

        internal static TypeDefinition GetTestNFAppDestructorsTestAnotherClassTypeDefinition(AssemblyDefinition assemblyDefinition)
        {
            TypeDefinition ret = null;

            var module = assemblyDefinition.Modules[0];
            ret = module.Types.First(i => i.FullName == "TestNFApp.DestructorsTestAnotherClass");

            return ret;
        }

        internal static MethodDefinition GetMethodDefinition(
            TypeDefinition typeDefinition,
            string delegateName,
            string methodName)
        {
            var delegateType = typeDefinition.NestedTypes.First(nt => nt.Name == delegateName);
            return delegateType.Methods.First(m => m.Name == methodName);
        }

        internal static MethodDefinition GetMethodDefinition(
            TypeDefinition typeDefinition,
            string methodName)
        {
            return typeDefinition.Methods.First(m => m.Name == methodName);
        }
        
        // no need to check if path exists as this validation is performed by nanoclr
        public static string NanoClrLocalInstance => Environment.GetEnvironmentVariable(
            _varNameForLocalNanoCLRInstancePath,
            EnvironmentVariableTarget.User);

    }
}
