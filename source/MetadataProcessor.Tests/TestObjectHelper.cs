using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace nanoFramework.Tools.MetadataProcessor.Tests
{
    public static class TestObjectHelper
    {
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

        public static AssemblyDefinition GetTestNFAppAssemblyDefinition()
        {
            AssemblyDefinition ret = null;

            var thisAssemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var testNfAppDir = Path.Combine(thisAssemblyDir, "TestNFApp");
            var testNfAppExePath = Path.Combine(testNfAppDir, "TestNFApp.exe");

            ret = AssemblyDefinition.ReadAssembly(testNfAppExePath);

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

        public static MethodDefinition GetTestNFAppOneClassOverAllDummyStaticMethodDefinition(TypeDefinition oneClassOverAllTypeDefinition)
        {
            MethodDefinition ret = GetTestNFAppOneClassOverAllMethodDefinition(oneClassOverAllTypeDefinition, "DummyStaticMethod");

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

    }
}
