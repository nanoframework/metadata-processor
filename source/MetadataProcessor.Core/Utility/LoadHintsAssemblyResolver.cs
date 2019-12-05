//
// Copyright (c) 2019 The nanoFramework project contributors
// Original work from Oleg Rakhmatulin.
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Implements special external .NET nanoFramework assemblies resolution logic.
    /// MetadataTransformer gets maps with pair assembly name and assebly path in command line,
    /// if we unable to load assemlby using default resolver we will try to use this map.
    /// </summary>
    public sealed class LoadHintsAssemblyResolver : BaseAssemblyResolver
    {
        /// <summary>
        /// List of 'load hints' - map between assembly name and assembly path.
        /// </summary>
        private readonly IDictionary<string, string> _loadHints;

        /// <summary>
        /// Creates new instance of <see cref="LoadHintsAssemblyResolver"/> object.
        /// </summary>
        /// <param name="loadHints">Metadata transformer load hints.</param>
        public LoadHintsAssemblyResolver(
            IDictionary<string, string> loadHints)
        {
            _loadHints = loadHints;
        }

        /// <inheritdoc/>
        public override AssemblyDefinition Resolve(AssemblyNameReference name)
        {
            try
            {
                return base.Resolve(name);
            }
            catch (Exception)
            {
                string assemblyFileName;
                if (_loadHints.TryGetValue(name.Name, out assemblyFileName))
                {
                    return AssemblyDefinition.ReadAssembly(assemblyFileName);
                }

                throw;
            }
        }

       
        /// <inheritdoc/>
        public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            try
            {
                return base.Resolve(name, parameters);
            }
            catch (Exception)
            {
                string assemblyFileName;
                if (_loadHints.TryGetValue(new AssemblyName(name.FullName).Name, out assemblyFileName))
                {
                    return AssemblyDefinition.ReadAssembly(assemblyFileName);
                }

                throw;
            }
        }
    }
}