// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Original work from Oleg Rakhmatulin.

using System.Xml;
using Mono.Cecil;

namespace nanoFramework.Tools.MetadataProcessor.Core
{
    /// <summary>
    /// Generates dependency graph for a .NET nanoFramework assembly.
    /// </summary>
    public sealed class nanoDependencyGenerator
    {
        private readonly nanoTablesContext _tablesContext;
        private readonly AssemblyDefinition _assemblyDefinition;
        private readonly string _path;
        private readonly string _name;
        private readonly string _project;

        private string _assemblyName;
        private nanoTablesContext tablesContext;
        private string _fileName;

        public nanoDependencyGenerator(
            AssemblyDefinition assemblyDefinition,
            nanoTablesContext tablesContext,
            string fileName)
        {
            _assemblyDefinition = assemblyDefinition;
            _tablesContext = tablesContext;
            _fileName = fileName;
        }

        public nanoDependencyGenerator(
            nanoTablesContext tablesContext,
            string path,
            string name,
            string project,
            bool interopCode)
        {
            _tablesContext = tablesContext;
            _path = path;
            _name = name;
            _project = project;

        }

        public void Write(
            XmlWriter xmlWriter)
        {
            var dependencyWriter = new nanoDependencyGeneratorWriter(
                _assemblyDefinition,
                _tablesContext);
            dependencyWriter.Write(xmlWriter);
        }

    }
}
