﻿//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace nanoFramework.Tools.MetadataProcessor.Tests
{
    public static class TestObjectHelper
    {
        private static string _testNFAppLocation;
        private static string _testNFClassLibLocation;
        private static string _nanoClrLocation;

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

        public static string TestNFAppLocation
        {
            get
            {
                if (string.IsNullOrEmpty(_testNFAppLocation))
                {
                    var thisAssemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    _testNFAppLocation = Path.Combine(
                        thisAssemblyDir,
                        "TestNFApp",
                        "TestNFApp.exe");
                }

                return _testNFAppLocation;
            }
        }

        public static string TestNFClassLibLocation
        {
            get
            {
                if (string.IsNullOrEmpty(_testNFClassLibLocation))
                {
                    var thisAssemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    _testNFClassLibLocation = Path.Combine(
                        thisAssemblyDir,
                        "TestNFApp",
                        "TestNFClassLibrary.dll");
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
                    var thisAssemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    _nanoClrLocation = Path.Combine(
                        thisAssemblyDir,
                        "nanoClr",
                        "nanoFramework.nanoCLR.exe");
                }

                return _nanoClrLocation;
            }
        }

        public static AssemblyDefinition GetTestNFAppAssemblyDefinition()
        {
            AssemblyDefinition ret = null;


            ret = AssemblyDefinition.ReadAssembly(TestNFAppLocation);

            return ret;
        }
        public static AssemblyDefinition GetTestNFClassLibraryDefinition()
        {
            AssemblyDefinition ret = null;

            ret = AssemblyDefinition.ReadAssembly(TestNFClassLibLocation);

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

            var thisAssembly = Assembly.GetExecutingAssembly();

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

    }
}
