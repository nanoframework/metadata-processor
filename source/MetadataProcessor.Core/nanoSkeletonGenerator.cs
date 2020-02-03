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
        private readonly bool _withoutInteropCode;

        private string _assemblyName;

        public nanoSkeletonGenerator(
            nanoTablesContext tablesContext,
            string path,
            string name,
            string project,
            bool withoutInteropCode)
        {
            _tablesContext = tablesContext;
            _path = path;
            _name = name;
            _project = project;
            _withoutInteropCode = withoutInteropCode;
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
                if (c.IncludeInStub() && 
                    !c.IsClassToExclude())
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
            var nativeVersionAttribute = _tablesContext.AssemblyDefinition.CustomAttributes.FirstOrDefault(a => a?.AttributeType?.Name == "AssemblyNativeVersionAttribute");
            Version nativeVersion = new Version((string)nativeVersionAttribute.ConstructorArguments[0].Value);

            var assemblyLookup = new AssemblyLookupTable()
            {
                Name = _name,
                AssemblyName = _assemblyName,
                HeaderFileName = _project,
                NativeVersion = nativeVersion,
                NativeCRC32 = "0x" + _tablesContext.NativeMethodsCrc.Current.ToString("X")
            };


            foreach (var c in _tablesContext.TypeDefinitionTable.Items)
            {
                // only care about types that have methods
                if (c.HasMethods)
                {
                    if (c.IncludeInStub())
                    {
                        // don't include if it's on the exclude list
                        if (!c.IsClassToExclude())
                        {
                            var className = NativeMethodsCrc.GetClassName(c);

                            foreach (var m in nanoTablesContext.GetOrderedMethods(c.Methods))
                            {
                                var rva = _tablesContext.ByteCodeTable.GetMethodRva(m);

                                // check method inclusion
                                // method is not a native implementation (RVA 0xFFFF) and is not abstract
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
                                    // method won't be included, still
                                    // need to add a NULL entry for it
                                    // unless it's on the exclude list

                                    if (!c.IsClassToExclude())
                                    {
                                        assemblyLookup.LookupTable.Add(new Method()
                                        {
                                            Declaration = "NULL"
                                            //Declaration = $"**Library_{_project}_{NativeMethodsCrc.GetClassName(c)}::{NativeMethodsCrc.GetMethodName(m)}"
                                        });
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // type won't be included, still
                        // need to add a NULL entry for each method 
                        // unless it's on the exclude list

                        if (!c.IsClassToExclude())
                        {
                            foreach (var m in nanoTablesContext.GetOrderedMethods(c.Methods))
                            {
                                assemblyLookup.LookupTable.Add(new Method()
                                {
                                    Declaration = "NULL"
                                    //Declaration = $"**Library_{_project}_{NativeMethodsCrc.GetClassName(c)}::{NativeMethodsCrc.GetMethodName(m)}"
                                });
                            }
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
                Name = _name.Replace('.', '_'), 
                ShortName = _project, 
                ShortNameUpper = _project.ToUpperInvariant()
            };

            foreach (var c in _tablesContext.TypeDefinitionTable.Items)
            {
                if (c.IncludeInStub() && 
                    !c.IsClassToExclude())
                {
                    var classData = new Class()
                    {
                        AssemblyName = _project,
                        Name = NativeMethodsCrc.GetClassName(c)
                    };

                    // static fields
                    int fieldCount = 0;
                    var staticFields = c.Fields.Where(f => f.IsStatic && !f.IsLiteral);

                    foreach (var f in staticFields)
                    {
                        classData.StaticFields.Add(new StaticField()
                        {
                            Name = f.Name,
                            ReferenceIndex = staticFieldCount + fieldCount++
                        });
                    }

                    // update static field counter
                    staticFieldCount += staticFields.Count();

                    int firstInstanceFieldId = GetInstanceFieldsOffset(c);

                    // 0 based index, need to add 1
                    firstInstanceFieldId++;

                    // instance fields
                    fieldCount = 0;
                    foreach (var f in c.Fields.Where(f => !f.IsStatic && !f.IsLiteral))
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
                            if (_tablesContext.FieldsTable.TryGetFieldReferenceId(f, false, out ushort fieldRefId))
                            {
                                classData.InstanceFields.Add(new InstanceField()
                                {
                                    Name = f.Name,
                                    ReferenceIndex = firstInstanceFieldId++
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
            }

            var stubble = new StubbleBuilder().Build();

            Directory.CreateDirectory(_path);

            using (var headerFile = File.CreateText(Path.Combine(_path, $"{_project}.h")))
            {
                var output = stubble.Render(SkeletonTemplates.AssemblyHeaderTemplate, assemblyData);
                headerFile.Write(output);
            }
        }

        private int GetInstanceFieldsOffset(TypeDefinition c)
        {
            // check if this type has a base type different from System.Object
            if( c.BaseType != null &&
                c.BaseType.FullName != "System.Object")
            {
                // get base parent type fields count
                return GetNestedFieldsCount(c.BaseType.Resolve());
            }
            else
            {
                return 0;
            }
        }

        private int GetNestedFieldsCount(TypeDefinition c)
        {
            ushort tt;

            int fieldCount = 0;

            if (c.BaseType != null &&
                c.BaseType.FullName != "System.Object")
            {
                // get parent type fields count
                fieldCount = GetNestedFieldsCount(c.BaseType.Resolve());

                // now add the fields count from this type
                if (_tablesContext.TypeDefinitionTable.TryGetTypeReferenceId(c, out tt))
                {
                    fieldCount += c.Fields.Count(f => !f.IsStatic && !f.IsLiteral);
                }

                return fieldCount;
            }
            else
            {
                // get the fields count from this type

                if (_tablesContext.TypeDefinitionTable.TryGetTypeReferenceId(c, out tt))
                {
                    return c.Fields.Count(f => !f.IsStatic && !f.IsLiteral);
                }
                else
                {
                    // can't find this type in the table
                    return 0;
                }
            }
        }
    }
}
