//
// Copyright (c) 2019 The nanoFramework project contributors
// Original work from Oleg Rakhmatulin.
// See LICENSE file in the project root for full license information.
//

using CommandLine;
using CommandLine.Text;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            public void Parse(string fileName)
            {
                try
                {
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
                    var builder = new nanoAssemblyBuilder(_assemblyDefinition, _classNamesToExclude, Minimize);

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

            // build header information
            var headerInfo = $"nanoFramework MetadataProcessor Utility v{informationalVersionAttribute.InformationalVersion}";
            var copyrightInfo = new CopyrightInfo(true, $"nanoFramework project contributors", 2019);

            // output header to console
            System.Console.WriteLine($"nanoFramework MetadataProcessor Utility v{informationalVersionAttribute.InformationalVersion}");
            System.Console.WriteLine("Copyright (c) 2019 nanoFramework project contributors");
            System.Console.WriteLine();
            System.Console.WriteLine("For documentation, report issues and support visit our GitHub repo: www.github.com\\nanoFramework");
            System.Console.WriteLine();
            System.Console.WriteLine();

            // check for empty argument collection
            if (!args.Any())
            {
                // no argument provided, show help text and usage examples

                // because of short-comings in CommandLine parsing 
                // need to customize the output to provide a consistent output
                var parser = new Parser(config => config.HelpWriter = null);
                var result = parser.ParseArguments<Options>(new string[] { "", "" });

                var helpText = new HelpText(
                    new HeadingInfo(headerInfo),
                    copyrightInfo)
                        .AddPreOptionsLine("No command was provided.")
                        .AddPreOptionsLine("")
                        .AddPreOptionsLine(HelpText.RenderUsageText(result))
                        .AddPreOptionsLine("")
                        .AddOptions(result);

                System.Console.WriteLine(helpText.ToString());

                return;
            }

            var parsedArguments = Parser.Default.ParseArguments<Options>(args);

            parsedArguments
                .WithParsed(opts => RunOptionsAndReturnExitCode(opts))
                .WithNotParsed(errors => HandleErrors(errors));
		}

        static void RunOptionsAndReturnExitCode(Options o)
        {
            var md = new MetadataProcessor();

            // arguments have to be processed in this order, otherwise the parsing will fail

            // load hints
            if (o.LoadHints.Any())
            {
                // load hints should be provided in the format: AssemblyName FilePath
                // like in --loadhints mscorlib e:\folder\where\the\assembly\is\mscorlib.dll
                // the LoadHints argument already provides the assembly details separated

                int hintCount = 0;

                do
                {
                    md.AddLoadHint(o.LoadHints.ElementAt(hintCount++), o.LoadHints.ElementAt(hintCount++));
                }
                while (hintCount < o.LoadHints.Count());
            }

            // set class(es) to exclude
            if (o.ExcludeClassByName.Any())
            {
                foreach (string name in o.ExcludeClassByName)
                {
                    md.AddClassToExclude(name);
                }
            }

            // set minimize option
            md.Minimize = o.Minimize;

            // parse assembly
            if (!string.IsNullOrEmpty(o.Parse))
            {
                md.Parse(o.Parse);
            }

            // compile PE
            if (!string.IsNullOrEmpty(o.Compile))
            {
                md.Compile(o.Compile);
            }
        }

        static void HandleErrors(IEnumerable<Error> errors)
        {
            // empty on purpose
        }
    }
}
