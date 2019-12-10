//
// Copyright (c) 2019 The nanoFramework project contributors
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;
using Stubble.Core.Builders;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace nanoFramework.Tools.MetadataProcessor.Core
{
    /// <summary>
    /// Generator of skeleton files from a .NET nanoFramework assembly.
    /// </summary>
    public sealed class nanoSkeletonGenerator
    {
        private readonly nanoTablesContext _tablesContext;
        private readonly string _path;
        private readonly string _name;
        private readonly string _project;
        private readonly bool _interopCode;

        private string _assemblyName;

        public nanoSkeletonGenerator(
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
            _interopCode = interopCode;
        }

        public void GenerateSkeleton()
        {
            // replaces "." with "_" so the assembly name can be part of C++ identifier name
            _assemblyName = _name.Replace('.', '_');

            // create <assembly>.h with the structs declarations
            GenerateAssemblyHeader();

            // generate <assembly>.cpp with the lookup definition
            GenerateAssemblyLookup();

            // generate <assembly>_<type>.cpp files with the type definition and stubs.
            GenerateStubs();
        }

        private void GenerateStubs()
        {
            foreach (var c in _tablesContext.TypeDefinitionTable.TypeDefinitions)
            {
                if (c.IncludeInStub() && !IsClassToExclude(c))
                {
                    var className = NativeMethodsCrc.GetClassName(c);

                    var classStubs = new AssemblyClassStubs()
                    {
                        HeaderFileName = _project
                    };

                    foreach (var m in nanoTablesContext.GetOrderedMethods(c.Methods))
                    {
                        var rva = _tablesContext.ByteCodeTable.GetMethodRva(m);

                        // check method inclusion
                        if (rva == 0xFFFF &&
                            !m.IsAbstract)
                        {
                            classStubs.Functions.Add(new Method()
                            {
                                Declaration = $"Library_{_project}_{className}::{NativeMethodsCrc.GetMethodName(m)}"
                            });
                        }
                    }

                    // anything to add to the header?
                    if (classStubs.Functions.Count > 0)
                    {
                        var stubble = new StubbleBuilder().Build();

                        using (var headerFile = File.CreateText(Path.Combine(_path, $"{_project}_{className}.cpp")))
                        {
                            var output = stubble.Render(SkeletonTemplates.ClassStubTemplate, classStubs);
                            headerFile.Write(output);
                        }
                    }
                }
            }
        }

        private void GenerateAssemblyLookup()
        {
            // grab native version from assembly attribute
            var nativeVersionAttribute = _tablesContext.AssemblyDefinition.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "AssemblyNativeVersionAttribute");
            Version nativeVersion = new Version((string)nativeVersionAttribute.ConstructorArguments[0].Value);

            var assemblyLookup = new AssemblyLookupTable()
            {
                Name = _name,
                AssemblyName = _assemblyName,
                HeaderFileName = _project,
                NativeVersion = nativeVersion,
                NativeCRC32 = "0x" + _tablesContext.NativeMethodsCrc.Current.ToString("X")
            };


            foreach (var c in _tablesContext.TypeDefinitionTable.TypeDefinitions)
            {
                if (c.IncludeInStub() && !IsClassToExclude(c))
                {
                    var className = NativeMethodsCrc.GetClassName(c);

                    foreach (var m in nanoTablesContext.GetOrderedMethods(c.Methods))
                    {
                        var rva = _tablesContext.ByteCodeTable.GetMethodRva(m);

                        // check method inclusion
                        if ((rva == 0xFFFF &&
                             !m.IsAbstract))
                        {
                            assemblyLookup.LookupTable.Add(new Method()
                            {
                                Declaration = $"Library_{_project}_{className}::{NativeMethodsCrc.GetMethodName(m)}"
                            });
                        }
                        else
                        {
                            assemblyLookup.LookupTable.Add(new Method()
                            {
                                Declaration = "NULL"
                            });
                        }
                    }
                }
            }

            var stubble = new StubbleBuilder().Build();

            using (var headerFile = File.CreateText(Path.Combine(_path, $"{_project}.cpp")))
            {
                var output = stubble.Render(SkeletonTemplates.AssemblyLookupTemplate, assemblyLookup);
                headerFile.Write(output);
            }
        }

        private void GenerateAssemblyHeader()
        {
            int staticFieldCount = 0;

            var assemblyData = new AssemblyDeclaration()
            {
                Name = _name, 
                ShortName = _project, 
                ShortNameUpper = _project.ToUpperInvariant()
            };

            foreach (var c in _tablesContext.TypeDefinitionTable.TypeDefinitions)
            {
                if (c.IncludeInStub() && !IsClassToExclude(c))
                {
                    var classData = new Class()
                    {
                        AssemblyName = _project,
                        Name = NativeMethodsCrc.GetClassName(c)
                    };

                    // static fields
                    int fieldCount = 0;
                    foreach (var f in c.Fields.Where(f => f.IsStatic))
                    {
                        classData.StaticFields.Add(new StaticField()
                        {
                            Name = f.Name,
                            ReferenceIndex = staticFieldCount + fieldCount++
                        });
                    }

                    // instance fields
                    fieldCount = 0;
                    foreach (var f in c.Fields.Where(f => !f.IsStatic))
                    {
                        // sanity check for field name
                        // like auto-vars and such
                        if (f.Name.IndexOfAny(new char[] { '<', '>' }) > 0)
                        {
                            classData.InstanceFields.Add(new InstanceField()
                            {
                                FieldWarning = $"*** Something wrong with field '{f.Name}'. Possibly its backing field is missing (mandatory for nanoFramework).\n"
                            });
                        }
                        else
                        {
                            ushort fieldRefId;
                            if (_tablesContext.FieldsTable.TryGetFieldReferenceId(f, false, out fieldRefId))
                            {
                                classData.InstanceFields.Add(new InstanceField()
                                {
                                    Name = f.Name,
                                    ReferenceIndex = fieldRefId + 1
                                });
                            }
                            fieldCount++;
                        }
                    }

                    // methods
                    if(c.HasMethods)
                    {
                        foreach (var m in nanoTablesContext.GetOrderedMethods(c.Methods))
                        {
                            var rva = _tablesContext.ByteCodeTable.GetMethodRva(m);

                            if( rva == 0xFFFF &&
                                !m.IsAbstract)
                            {
                                classData.Methods.Add(new Method()
                                {
                                    Declaration = NativeMethodsCrc.GetMethodName(m)
                                });
                            }
                        }

                    }

                    // anything to add to the header?
                    if( classData.StaticFields.Count > 0 ||
                        classData.InstanceFields.Count > 0 ||
                        classData.Methods.Count > 0)
                    {
                        assemblyData.Classes.Add(classData);
                    }
                }

                staticFieldCount += c.Fields.Count(f => f.IsStatic);
            }

            var stubble = new StubbleBuilder().Build();

            Directory.CreateDirectory(_path);

            using (var headerFile = File.CreateText(Path.Combine(_path, $"{_project}.h")))
            {
                var output = stubble.Render(SkeletonTemplates.AssemblyHeaderTemplate, assemblyData);
                headerFile.Write(output);
            }
        }

        private bool IsClassToExclude(TypeDefinition td)
        {
            return _tablesContext.ClassNamesToExclude.Contains(td.FullName);
        }
    }
}
