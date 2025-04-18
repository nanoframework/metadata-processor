﻿//
// Copyright (c) .NET Foundation and Contributors
// Original work from Oleg Rakhmatulin.
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using nanoFramework.Tools.MetadataProcessor.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Xml;

namespace nanoFramework.Tools.MetadataProcessor.Console
{
    internal static class MainClass
    {
        private sealed class MetadataProcessor
        {
            private readonly IDictionary<string, string> _loadHints =
                new Dictionary<string, string>(StringComparer.Ordinal);

            private AssemblyDefinition _assemblyDefinition;
            private nanoAssemblyBuilder _assemblyBuilder;

            private List<string> _classNamesToExclude = new List<string>();

            internal string PeFileName;

            internal bool Verbose { get; set; }

            internal bool VerboseMinimize { get; set; }

            public bool DumpMetadata { get; internal set; } = false;

            public void Parse(string fileName)
            {
                try
                {
                    if (Verbose) System.Console.WriteLine("Parsing assembly...");

                    _assemblyDefinition = AssemblyDefinition.ReadAssembly(fileName,
                        new ReaderParameters { AssemblyResolver = new LoadHintsAssemblyResolver(_loadHints) });
                }
                catch (Exception)
                {
                    System.Console.Error.WriteLine(
                        "Unable to parse input assembly file '{0}' - check if path and file exists.", fileName);
                    Environment.Exit(1);
                }
            }

            public void Compile(
                string fileName,
                bool isCoreLibrary)
            {
                try
                {
                    if (Verbose) System.Console.WriteLine("Compiling assembly...");

                    _assemblyBuilder = new nanoAssemblyBuilder(_assemblyDefinition, _classNamesToExclude, VerboseMinimize, isCoreLibrary);

                    using (var stream = File.Open(Path.ChangeExtension(fileName, "tmp"), FileMode.Create, FileAccess.ReadWrite))
                    using (var writer = new BinaryWriter(stream))
                    {
                        _assemblyBuilder.Write(GetBinaryWriter(writer));
                    }
                }
                catch (Exception)
                {
                    System.Console.Error.WriteLine(
                        $"Unable to compile output assembly file '{fileName}' - check parse command results.");
                    throw;
                }

                try
                {
                    // OK to delete tmp PE file
                    File.Delete(Path.ChangeExtension(fileName, "tmp"));

                    if (Verbose) System.Console.WriteLine("Minimizing assembly...");

                    _assemblyBuilder.Minimize();

                    if (Verbose) System.Console.WriteLine("Recompiling assembly...");

                    using (var stream = File.Open(fileName, FileMode.Create, FileAccess.ReadWrite))
                    using (var writer = new BinaryWriter(stream))
                    {
                        _assemblyBuilder.Write(GetBinaryWriter(writer));
                    }

                    using (var writer = XmlWriter.Create(Path.ChangeExtension(fileName, "pdbx")))
                    {
                        _assemblyBuilder.Write(writer);
                    }

                    if (DumpMetadata)
                    {
                        nanoDumperGenerator dumper = new nanoDumperGenerator(
                            _assemblyBuilder.TablesContext,
                            Path.ChangeExtension(fileName, "dump.txt"));
                        dumper.DumpAll();
                    }
                }
                catch (ArgumentException ex)
                {
                    System.Console.Error.WriteLine($"Exception minimizing assembly: {ex.Message}.");
                }
                catch (Exception)
                {
                    System.Console.Error.WriteLine($"Exception minimizing assembly.");
                    throw;
                }
            }

            private nanoBinaryWriter GetBinaryWriter(BinaryWriter writer)
            {
                return nanoBinaryWriter.CreateLittleEndianBinaryWriter(writer);
            }

            public void AddLoadHint(
                string assemblyName,
                string assemblyFileName)
            {
                _loadHints[assemblyName] = assemblyFileName;
            }

            public void AddClassToExclude(
                string className)
            {
                _classNamesToExclude.Add(className);
            }

            public void GenerateSkeleton(
                string file,
                string name,
                string project,
                bool withoutInteropCode,
                bool isCoreLibrary)
            {
                try
                {
                    if (Verbose) System.Console.WriteLine("Generating skeleton files...");

                    var skeletonGenerator = new nanoSkeletonGenerator(
                        _assemblyBuilder.TablesContext,
                        file,
                        name,
                        project,
                        withoutInteropCode,
                        isCoreLibrary);

                    skeletonGenerator.GenerateSkeleton();
                }
                catch (Exception ex)
                {
                    System.Console.Error.WriteLine("Unable to generate skeleton files.");
                    throw;
                }
            }

            public void GenerateDependency(string fileName)
            {
                try
                {
                    var dependencyGenerator = new nanoDependencyGenerator(
                        _assemblyDefinition,
                        _assemblyBuilder.TablesContext,
                        fileName);

                    using (var writer = XmlWriter.Create(fileName))
                    {
                        dependencyGenerator.Write(writer);
                    }
                }
                catch (Exception)
                {
                    System.Console.Error.WriteLine(
                        "Unable to generate and write dependency graph for assembly file '{0}'.", fileName);
                    throw;
                }
            }
        }

        public static void Main(string[] args)
        {
            FileVersionInfo fileVersion = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);

            bool isCoreLibrary = false;

            // output header to console
            System.Console.WriteLine($"nanoFramework MetadataProcessor Utility v{fileVersion.ToString()}");
            System.Console.WriteLine("Copyright (c) 2019 nanoFramework project contributors");
            System.Console.WriteLine();
            System.Console.WriteLine("For documentation, report issues and support visit our GitHub repo: www.github.com\\nanoFramework");
            System.Console.WriteLine();
            System.Console.WriteLine();

            var md = new MetadataProcessor();

            for (var i = 0; i < args.Length; ++i)
            {
                var arg = args[i].ToLower(CultureInfo.InvariantCulture);

                if ((arg == "-h" ||
                    arg == "-help" ||
                    arg == "?") &&
                    (i + 1 < args.Length))
                {
                    System.Console.WriteLine("");
                    System.Console.WriteLine("-parse <path-to-assembly-file>                        Analyses .NET assembly.");
                    System.Console.WriteLine("-compile <path-to-PE-file> <isCoreLibrary>            Compiles an assembly into nanoCLR format and (true/false) if this it's a core library.");
                    System.Console.WriteLine("-loadHints <assembly-name> <path-to-assembly-file>    Loads one (or more) assembly file(s) as a dependency(ies).");
                    System.Console.WriteLine("-excludeClassByName <class-name>                      Removes the class from an assembly.");
                    System.Console.WriteLine("-generateskeleton                                     Generate skeleton files with stubs to add native code for an assembly.");
                    System.Console.WriteLine("-generateDependency                                   Generates an XML file with the relationship between assemblies.");
                    System.Console.WriteLine("-verbose                                              Outputs each command before executing it.");
                    System.Console.WriteLine("-verboseMinimize                                      Turns on verbose level for the minimization phase.");
                    System.Console.WriteLine("-dump_all                                             Generates a report of an assembly's metadata.");
                    System.Console.WriteLine("");
                }
                else if (arg == "-parse" && i + 1 < args.Length)
                {
                    md.Parse(args[++i]);
                }
                else if (arg == "-compile" && i + 2 < args.Length)
                {
                    if (!bool.TryParse(args[i + 2], out isCoreLibrary))
                    {
                        System.Console.Error.WriteLine("Bad parameter for compile. IsCoreLib options has to be 'true' or 'false'.");

                        Environment.Exit(1);
                    }

                    md.PeFileName = args[i + 1];

                    md.Compile(md.PeFileName, isCoreLibrary);

                    i += 2;
                }
                else if (arg == "-excludeclassbyname" && i + 1 < args.Length)
                {
                    System.Console.WriteLine("*********************************************** WARNING *************************************************");
                    System.Console.WriteLine("Use ExcludeTypeAttribute instead of passing this option. This will be removed in a future version of MDP.");
                    System.Console.WriteLine("*********************************************************************************************************");

                    md.AddClassToExclude(args[++i]);
                }
                else if (arg == "-verbose")
                {
                    md.Verbose = true;
                }
                else if (arg == "-verboseminimize" && i + 1 < args.Length)
                {
                    md.VerboseMinimize = true;
                }
                else if (arg == "-loadhints" && i + 2 < args.Length)
                {
                    md.AddLoadHint(args[i + 1], args[i + 2]);
                    i += 2;
                }
                else if (arg == "-generateskeleton" && i + 2 < args.Length)
                {
                    // fill in arguments
                    string file = args[i + 1];
                    string name = args[i + 2];
                    string project = args[i + 3];
                    bool withoutInteropCode = false;

                    if (!bool.TryParse(args[i + 4], out withoutInteropCode))
                    {
                        System.Console.Error.WriteLine("Bad parameter for generateSkeleton. Generate code without Interop support has to be 'true' or 'false'.");

                        Environment.Exit(1);
                    }

                    md.GenerateSkeleton(
                        file,
                        name,
                        project,
                        withoutInteropCode,
                        isCoreLibrary);

                    i += 4;
                }
                else if (arg == "-generatedependency" && i + 1 < args.Length)
                {
                    md.GenerateDependency(args[++i]);
                }
                else if (arg == "-dump_all" && i + 1 < args.Length)
                {
                    md.DumpMetadata = true;
                }
                else
                {
                    System.Console.Error.WriteLine("Unknown command line option '{0}' ignored.", arg);
                }
            }
        }
    }
}
