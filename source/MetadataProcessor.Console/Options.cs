//
// Copyright (c) 2019 The nanoFramework project contributors
// Original work from Oleg Rakhmatulin.
// See LICENSE file in the project root for full license information.
//

using CommandLine;
using System.Collections.Generic;

namespace nanoFramework.Tools.MetadataProcessor.Console
{
    public class Options
    {

        [Option(
            "parse",
            Required = false,
            Default = null,
            HelpText = "Analyses .NET assembly.")]
        public string Parse { get; set; }

        [Option(
            "compile",
            Required = false,
            Default = null,
            HelpText = "Compiles an assembly into nanoCLR format.")]
        public string Compile { get; set; }

        [Option(
            "loadhints",
            Separator = ' ',
            Required = false,
            HelpText = "Loads one (or more) assembly file(s) as a dependency(ies).")]
        public IEnumerable<string> LoadHints { get; set; }

        [Option(
            "excludeClassByName",
            Required = false,
            Default = null,
            HelpText = "Removes the class from an assembly.")]
        public IEnumerable<string> ExcludeClassByName { get; set; }


    }
}
