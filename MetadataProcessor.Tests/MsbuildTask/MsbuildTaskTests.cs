// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using nanoFramework.Tools.MetadataProcessor.Core;

namespace nanoFramework.Tools.MetadataProcessor.Tests.MsbuildTask
{
    [TestClass]
    public class MsbuildTaskTests
    {
        [TestMethod]
        public void ProcessTestApp()
        {
            // setup load hints
            var loadHints = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["mscorlib"] = Path.Combine(Directory.GetParent(TestObjectHelper.NFAppFullPath).FullName, "mscorlib.dll")
            };

            // class names to exclude from processing
            var classNamesToExclude = new List<string>
            {
                "THIS_NAME_DOES_NOT_EXIST_IN_THE_PROJECT"
            };

            var fileToParse = TestObjectHelper.NFAppFullPath;
            var fileToCompiler = Path.ChangeExtension(fileToParse, "pe");

            ProcessAssembly(loadHints, classNamesToExclude, fileToParse, fileToCompiler);
        }

        private void ProcessAssembly(
            Dictionary<string, string> loadHints,
            List<string> classNamesToExclude,
            string fileToParse,
            string fileToCompile)
        {
            ProcessAssembly(
                loadHints,
                classNamesToExclude,
                fileToParse,
                fileToCompile,
                null,
                null,
                null,
                false,
                false,
                out string _);
        }

        private void ProcessAssembly(
            Dictionary<string, string> loadHints,
            List<string> classNamesToExclude,
            string fileToParse,
            string fileToCompile,
            string GenerateSkeletonFile,
            string GenerateSkeletonProject,
            string GenerateSkeletonName,
            bool GenerateStubs,
            bool SkeletonWithoutInterop,
            out string dumpFile)
        {
            // this method reproduces what happens in MetaDataProcessorTask class 
            // reason being that's the most practical way to get the full processing sequence tested without scattering a lot of code

            ///////////////////////////
            // code from ExecuteParse()
            // parse executable
            var assemblyDefinition = AssemblyDefinition.ReadAssembly(
                fileToParse,
                new ReaderParameters { AssemblyResolver = new LoadHintsAssemblyResolver(loadHints) });

            /////////////////////////////
            // code from ExecuteCompile()
            // compile
            var _assemblyBuilder = new nanoAssemblyBuilder(
                assemblyDefinition,
                classNamesToExclude,
                true,
                false);

            using (var stream = File.Open(Path.ChangeExtension(fileToCompile, "tmp"), FileMode.Create, FileAccess.ReadWrite))
            using (var writer = new BinaryWriter(stream))
            {
                _assemblyBuilder.Write(GetBinaryWriter(writer));
            }

            // OK to delete tmp PE file
            File.Delete(Path.ChangeExtension(fileToCompile, "tmp"));

            // minimize (has to be called after the 1st compile pass)
            _assemblyBuilder.Minimize();

            // compile assembly (2nd pass after minimize)

            using (var stream = File.Open(fileToCompile, FileMode.Create, FileAccess.ReadWrite))
            using (var writer = new BinaryWriter(stream))
            {
                _assemblyBuilder.Write(GetBinaryWriter(writer));
            }

            // output PDBX
            _assemblyBuilder.Write(Path.ChangeExtension(fileToCompile, "pdbx"));

            // output assembly metadata
            dumpFile = Path.ChangeExtension(fileToCompile, "dump.txt");

            nanoDumperGenerator dumper = new nanoDumperGenerator(
                _assemblyBuilder.TablesContext,
                dumpFile);
            dumper.DumpAll();
        }

        private nanoBinaryWriter GetBinaryWriter(
            BinaryWriter writer)
        {
            return nanoBinaryWriter.CreateLittleEndianBinaryWriter(writer);
        }
    }
}
