//
// Copyright (c) 2019 The nanoFramework project contributors
// Original work from Oleg Rakhmatulin.
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using System;
using System.Collections.Generic;
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

            private List<string> _classNamesToExclude = new List<string>();

            internal bool Minimize { get; set; }

            internal bool Verbose { get; set; }

            public void Parse(string fileName)
            {
                try
                {
                    if(Verbose) System.Console.WriteLine("Parsing assembly...");

                    _assemblyDefinition = AssemblyDefinition.ReadAssembly(fileName,
                        new ReaderParameters { AssemblyResolver = new LoadHintsAssemblyResolver(_loadHints)});
                }
                catch (Exception)
                {
                    System.Console.Error.WriteLine(
                        "Unable to parse input assembly file '{0}' - check if path and file exists.", fileName);
                    Environment.Exit(1);
                }
            }

            public void Compile(string fileName)
            {
                try
                {
                    if (Verbose) System.Console.WriteLine("Compiling assembly...");

                    var builder = new nanoAssemblyBuilder(_assemblyDefinition, _classNamesToExclude, Minimize, Verbose);

                    using (var stream = File.Open(fileName, FileMode.Create, FileAccess.ReadWrite))
                    using (var writer = new BinaryWriter(stream))
                    {
                        builder.Write(GetBinaryWriter(writer));
                    }

                    using (var writer = XmlWriter.Create(Path.ChangeExtension(fileName, "pdbx")))
                    {
                        builder.Write(writer);
                    }
                }
                catch (Exception)
                {
                    System.Console.Error.WriteLine(
                        "Unable to compile output assembly file '{0}' - check parse command results.", fileName);
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
        }

        public static void Main(string[] args)
		{
            // grab the assembly version
            var informationalVersionAttribute = Attribute.GetCustomAttribute(
                Assembly.GetEntryAssembly(),
                typeof(AssemblyInformationalVersionAttribute))
            as AssemblyInformationalVersionAttribute;

            // output header to console
            System.Console.WriteLine($"nanoFramework MetadataProcessor Utility v{informationalVersionAttribute.InformationalVersion}");
            System.Console.WriteLine("Copyright (c) 2019 nanoFramework project contributors");
            System.Console.WriteLine();
            System.Console.WriteLine("For documentation, report issues and support visit our GitHub repo: www.github.com\\nanoFramework");
            System.Console.WriteLine();
            System.Console.WriteLine();

            var md = new MetadataProcessor();

            for (var i = 0; i < args.Length; ++i)
            {
                var arg = args[i].ToLower(CultureInfo.InvariantCulture);

                if ( (arg == "-h" ||
                    arg == "-help" ||
                    arg == "?") && 
                    (i + 1 < args.Length))
                {
                    System.Console.WriteLine("");
                    System.Console.WriteLine("-parse <path-to-assembly-file>                        Analyses .NET assembly.");
                    System.Console.WriteLine("-compile <path-to-PE-file>                            Compiles an assembly into nanoCLR format.");
                    System.Console.WriteLine("-loadHints <assembly-name> <path-to-assembly-file>    Loads one (or more) assembly file(s) as a dependency(ies).");
                    System.Console.WriteLine("-excludeClassByName <class-name>                      Removes the class from an assembly.");
                    System.Console.WriteLine("-minimize                                             Minimizes the assembly, removing unwanted elements.");
                    System.Console.WriteLine("-verbose                                              Outputs each command before executing it.");
                    System.Console.WriteLine("");
                }
                else if (arg == "-parse" && i + 1 < args.Length)
                {
                    md.Parse(args[++i]);
                }
                else if (arg == "-compile" && i + 1 < args.Length)
                {
                    md.Compile(args[++i]);
                }
                else if (arg == "-excludeclassbyName" && i + 1 < args.Length)
                {
                    md.AddClassToExclude(args[++i]);
                }
                else if (arg == "-minimize" && i + 1 < args.Length)
                {
                    md.Minimize = true;
                }
                else if (arg == "-verbose" && i + 1 < args.Length)
                {
                    md.Verbose = true;
                }
                else if (arg == "-loadhints" && i + 2 < args.Length)
                {
                    md.AddLoadHint(args[i + 1], args[i + 2]);
                    i += 2;
                }
                else
                {
                    System.Console.Error.WriteLine("Unknown command line option '{0}' ignored.", arg);
                }
            }
		}
    }
}
