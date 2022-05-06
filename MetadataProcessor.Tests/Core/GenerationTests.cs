using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using nanoFramework.Tools.MetadataProcessor.Core;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core
{
    [TestClass]
    public class GenerationTests
    {
        [TestMethod]
        public void GenerateTestNFAppTest()
        {
            // Arrange
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
            var stubPath = $"{TestObjectHelper.TestExecutionLocation}\\Stubs";
            var assemblyDefinition = AssemblyDefinition.ReadAssembly(fileToParse,
                new ReaderParameters { AssemblyResolver = new LoadHintsAssemblyResolver(loadHints) });

            var assemblyBuilder = new nanoAssemblyBuilder(assemblyDefinition, classNamesToExclude, false, false);

            using (var stream = File.Open(Path.ChangeExtension(fileToCompile, "tmp"), FileMode.Create,
                       FileAccess.ReadWrite))
            using (var writer = new BinaryWriter(stream))
            {
                assemblyBuilder.Write(GetBinaryWriter(writer));
            }

            // OK to delete tmp PE file
            File.Delete(Path.ChangeExtension(fileToCompile, "tmp"));

            assemblyBuilder.Minimize();

            var tablesContext = assemblyBuilder.TablesContext;

            var skeletonGenerator = new nanoSkeletonGenerator(tablesContext, stubPath, "testStubs",
                "GenerationTestNFApp", false, false);

            // Act
            skeletonGenerator.GenerateSkeleton();

            // Assert
        }

        private nanoBinaryWriter GetBinaryWriter(
            BinaryWriter writer)
        {
            return nanoBinaryWriter.CreateLittleEndianBinaryWriter(writer);
        }
    }
}