﻿//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using Mustache;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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
        private readonly bool _isCoreLib;
        private readonly string _assemblyName;

        private string _safeProjectName => _project.Replace('.', '_');

        public string SafeProjectName => _safeProjectName;

        public nanoSkeletonGenerator(
            nanoTablesContext tablesContext,
            string path,
            string name,
            string project,
            bool withoutInteropCode,
            bool isCoreLib)
        {
            _tablesContext = tablesContext;
            _path = path;
            _name = name;
            _project = project;
            _withoutInteropCode = withoutInteropCode;
            _isCoreLib = isCoreLib;

            // replaces "." with "_" so the assembly name can be part of C++ identifier name
            _assemblyName = _name.Replace('.', '_');
        }

        public void GenerateSkeleton()
        {
            // check if there are any native methods
            if (_tablesContext.NativeMethodsCrc.CurrentCrc > 0)
            {
                // create <assembly>.h with the structs declarations
                GenerateAssemblyHeader();

                // generate <assembly>.cpp with the lookup definition
                GenerateAssemblyLookup();

                // generate stub files for classes, headers and marshalling code, if required
                GenerateStubs();

                // output native checksum so it shows in build log
                Console.WriteLine("++++++++++++++++++++++++++++++++++++++++++++");
                Console.WriteLine($"+ Native declaration checksum: 0x{_tablesContext.NativeMethodsCrc.CurrentCrc.ToString("X8")} +");
                Console.WriteLine("++++++++++++++++++++++++++++++++++++++++++++");
            }
            else
            {
                Console.WriteLine("++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
                Console.WriteLine("+ Skipping skeleton generation because this class doesn't have native implementation +");
                Console.WriteLine("++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
            }
        }

        private void GenerateStubs()
        {
            var generatedFiles = new List<string>();

            var classList = new AssemblyClassTable
            {
                AssemblyName = _tablesContext.AssemblyDefinition.Name.Name,
                ProjectName = _safeProjectName,
                IsInterop = !_withoutInteropCode
            };

            foreach (TypeDefinition c in _tablesContext.TypeDefinitionTable.TypeDefinitions)
            {
                if (ShouldIncludeType(c))
                {
                    var className = NativeMethodsCrc.GetClassName(c);

                    var classStubs = new AssemblyClassStubs
                    {
                        AssemblyName = _name,
                        ClassHeaderFileName = className,
                        ClassName = c.Name,
                        ShortNameUpper = $"{_assemblyName}_{_safeProjectName}_{className}".ToUpper(),
                        RootNamespace = _assemblyName,
                        ProjectName = _safeProjectName,
                        HeaderFileName = _safeProjectName
                    };

                    classList.Classes.Add(new Class()
                    {
                        Name = className
                    });
                    classList.HeaderFileName = classStubs.HeaderFileName;

                    foreach (var m in nanoTablesContext.GetOrderedMethods(c.Methods))
                    {
                        var rva = _tablesContext.ByteCodeTable.GetMethodRva(m);

                        // check method inclusion
                        if (rva == 0xFFFF &&
                            !m.IsAbstract)
                        {
                            var newMethod = new MethodStub()
                            {
                                Declaration = $"Library_{_safeProjectName}_{className}::{NativeMethodsCrc.GetMethodName(m)}"
                            };

                            if (!_withoutInteropCode)
                            {
                                // process with Interop code

                                newMethod.IsStatic = m.IsStatic;
                                newMethod.HasReturnType = (
                                    m.MethodReturnType != null &&
                                    m.MethodReturnType.ReturnType.FullName != "System.Void");

                                StringBuilder declaration = new StringBuilder();

                                newMethod.ReturnType = m.MethodReturnType.ReturnType.ToNativeTypeAsString();

                                newMethod.MarshallingReturnType = m.MethodReturnType.ReturnType.ToCLRTypeAsString();

                                declaration.Append($"{m.Name}");
                                declaration.Append("( ");

                                StringBuilder marshallingCall = new StringBuilder($"{m.Name}");
                                marshallingCall.Append("( ");

                                // loop through the parameters
                                if (m.HasParameters)
                                {
                                    int parameterIndex = 0;

                                    foreach (var item in m.Parameters)
                                    {
                                        // get the parameter type
                                        string parameterType = string.Empty;
                                        string parameterTypeWORef = string.Empty;
                                        string parameterTypeClr = string.Empty;

                                        if (item.ParameterType.IsByReference)
                                        {
                                            // for ref types need an extra step to get the element type
                                            parameterType = item.ParameterType.GetElementType().ToNativeTypeAsString() + "&";
                                            parameterTypeWORef = item.ParameterType.GetElementType().ToNativeTypeAsString();
                                            parameterTypeClr = item.ParameterType.GetElementType().ToCLRTypeAsString();
                                        }
                                        else
                                        {
                                            parameterType = item.ParameterType.ToNativeTypeAsString();
                                            parameterTypeClr = item.ParameterType.ToCLRTypeAsString();
                                        }

                                        // compose the function declaration
                                        declaration.Append($"{parameterType} param{parameterIndex}, ");

                                        // compose the function call
                                        if (item.ParameterType.IsByReference)
                                        {
                                            marshallingCall.Append($"*param{parameterIndex}, ");
                                        }
                                        else
                                        {
                                            marshallingCall.Append($"param{parameterIndex}, ");
                                        }


                                        // compose the variable block
                                        var parameterDeclaration = new ParameterDeclaration()
                                        {
                                            Index = parameterIndex.ToString(),
                                            Name = $"param{parameterIndex}",
                                        };

                                        if (item.ParameterType.IsByReference)
                                        {
                                            // declaration like
                                            // INT8 param1;
                                            // UINT8 heapblock1[CLR_RT_HEAP_BLOCK_SIZE];

                                            parameterDeclaration.Type = parameterType;

                                            parameterDeclaration.Declaration =
                                                $"{parameterTypeWORef} *{parameterDeclaration.Name};" + Environment.NewLine +
                                                $"        uint8_t heapblock{parameterIndex}[CLR_RT_HEAP_BLOCK_SIZE];";

                                            parameterDeclaration.MarshallingDeclaration = $"Interop_Marshal_{parameterTypeClr}_ByRef( stack, heapblock{parameterIndex}, {(parameterIndex + (m.IsStatic ? 0 : 1))}, {parameterDeclaration.Name} )";

                                        }
                                        else if (item.ParameterType.IsArray)
                                        {
                                            // declaration like
                                            // CLR_RT_TypedArray_UINT8 param0;

                                            parameterDeclaration.Type = parameterType;
                                            parameterDeclaration.Declaration = $"{parameterType} {parameterDeclaration.Name};";
                                            parameterDeclaration.MarshallingDeclaration = $"Interop_Marshal_{parameterTypeClr}( stack, {(parameterIndex + (m.IsStatic ? 0 : 1))}, {parameterDeclaration.Name} )";
                                        }
                                        else
                                        {
                                            // declaration like
                                            // INT8 param1;

                                            parameterDeclaration.Type = parameterType;
                                            parameterDeclaration.Declaration = $"{parameterType} {parameterDeclaration.Name};";
                                            parameterDeclaration.MarshallingDeclaration = $"Interop_Marshal_{parameterTypeClr}( stack, {(parameterIndex + (m.IsStatic ? 0 : 1))}, {parameterDeclaration.Name} )";
                                        }
                                        newMethod.ParameterDeclaration.Add(parameterDeclaration);
                                        parameterIndex++;
                                    }

                                    declaration.Append("HRESULT &hr )");
                                    marshallingCall.Append("hr )");
                                }
                                else
                                {
                                    declaration.Append(" HRESULT &hr )");
                                    marshallingCall.Append(" hr )");
                                }

                                newMethod.DeclarationForUserCode = declaration.ToString();
                                newMethod.CallFromMarshalling = marshallingCall.ToString();
                            }

                            classStubs.Functions.Add(newMethod);
                        }
                    }

                    // anything to add to the header?
                    if (classStubs.Functions.Count > 0)
                    {
                        if (_withoutInteropCode)
                        {
                            FormatCompiler compiler = new FormatCompiler
                            {
                                RemoveNewLines = false
                            };
                            Generator generator = compiler.Compile(SkeletonTemplates.ClassWithoutInteropStubTemplate);

                            using (var headerFile = File.CreateText(Path.Combine(_path, $"{_safeProjectName}_{className}.cpp")))
                            {
                                var output = generator.Render(classStubs);
                                headerFile.Write(output);
                            }

                            // add class to list of classes with stubs
                            classList.ClassesWithStubs.Add(new ClassWithStubs()
                            {
                                Name = className
                            });
                        }
                        else
                        {
                            FormatCompiler compiler = new FormatCompiler
                            {
                                RemoveNewLines = false
                            };

                            // user code stub
                            Generator generator = compiler.Compile(SkeletonTemplates.ClassStubTemplate);

                            using (var headerFile = File.CreateText(Path.Combine(_path, $"{_safeProjectName}_{className}.cpp")))
                            {
                                var output = generator.Render(classStubs);
                                headerFile.Write(output);
                            }

                            // marshal code
                            generator = compiler.Compile(SkeletonTemplates.ClassMarshallingCodeTemplate);

                            using (var headerFile = File.CreateText(Path.Combine(_path, $"{_safeProjectName}_{className}_mshl.cpp")))
                            {
                                var output = generator.Render(classStubs);
                                headerFile.Write(output);
                            }

                            // class header
                            generator = compiler.Compile(SkeletonTemplates.ClassHeaderTemplate);

                            using (var headerFile = File.CreateText(Path.Combine(_path, $"{_safeProjectName}_{className}.h")))
                            {
                                var output = generator.Render(classStubs);
                                headerFile.Write(output);
                            }

                            // add class to list of classes with stubs
                            classList.ClassesWithStubs.Add(new ClassWithStubs()
                            {
                                Name = className
                            });
                        }
                    }
                }
            }

            if (classList.Classes.Count > 0)
            {
                FormatCompiler compiler = new FormatCompiler
                {
                    RemoveNewLines = false
                };

                // CMake module
                Generator generator = compiler.Compile(SkeletonTemplates.CMakeModuleTemplate);

                string fileName;

                if (!_withoutInteropCode)
                {
                    // this is an Interop library: FindINTEROP-NF.AwesomeLib.cmake
                    fileName = Path.Combine(_path, $"FindINTEROP-{classList.AssemblyName}.cmake");
                }
                else
                {
                    // this is a class library: FindWindows.Devices.Gpio.cmake
                    fileName = Path.Combine(_path, $"Find{classList.AssemblyName}.cmake");
                }

                using (var headerFile = File.CreateText(fileName))
                {
                    var output = generator.Render(classList);
                    headerFile.Write(output);
                }
            }
        }

        private void GenerateAssemblyLookup()
        {
            // grab native version from assembly attribute
            var nativeVersionAttribute = _tablesContext.AssemblyDefinition.CustomAttributes.FirstOrDefault(a => a?.AttributeType?.Name == "AssemblyNativeVersionAttribute");

            // check for existing AssemblyNativeVersionAttribute
            if (nativeVersionAttribute == null)
            {
                throw new ArgumentException("Missing AssemblyNativeVersionAttribute." + Environment.NewLine +
                    "Make sure that AssemblyNativeVersionAttribute is defined in AssemblyInfo.cs, like this:" + Environment.NewLine +
                    "[assembly: AssemblyNativeVersion(\"1.0.0.0\")]");
            }

            Version nativeVersion = new Version((string)nativeVersionAttribute.ConstructorArguments[0].Value);

            var assemblyLookup = new AssemblyLookupTable()
            {
                IsCoreLib = _isCoreLib,
                Name = _assemblyName,
                AssemblyName = _tablesContext.AssemblyDefinition.Name.Name,
                HeaderFileName = _safeProjectName,
                NativeVersion = nativeVersion,
                NativeCRC32 = "0x" + _tablesContext.NativeMethodsCrc.CurrentCrc.ToString("X8")
            };


            foreach (TypeDefinition c in _tablesContext.TypeDefinitionTable.Items)
            {
                // only care about types that have methods
                if (c.HasMethods)
                {
                    if (ShouldIncludeType(c))
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
                                assemblyLookup.LookupTable.Add(new MethodStub()
                                {
                                    Declaration = $"Library_{_safeProjectName}_{className}::{NativeMethodsCrc.GetMethodName(m)}"
                                });
                            }
                            else
                            {
                                // method won't be included, still
                                // need to add a NULL entry for it
                                assemblyLookup.LookupTable.Add(new MethodStub()
                                {
#if DEBUG
                                    Declaration = $"NULL, // <<<<< Library_{_safeProjectName}_{NativeMethodsCrc.GetClassName(c)}::{NativeMethodsCrc.GetMethodName(m)}",
#else
                                    Declaration = "NULL"
#endif
                                });
                            }
                        }
                    }
                    else
                    {
                        // type won't be included, still
                        // need to add a NULL entry for each method 
                        foreach (var m in nanoTablesContext.GetOrderedMethods(c.Methods))
                        {
                            assemblyLookup.LookupTable.Add(new MethodStub()
                            {
#if DEBUG
                                Declaration = $"NULL, // <<<<< Library_{_safeProjectName}_{NativeMethodsCrc.GetClassName(c)}::{NativeMethodsCrc.GetMethodName(m)}",
#else
                                Declaration = "NULL"
#endif
                            });
                        }
                    }
                }
            }

            FormatCompiler compiler = new FormatCompiler();
            Generator generator = compiler.Compile(SkeletonTemplates.AssemblyLookupTemplate);

            using (var headerFile = File.CreateText(Path.Combine(_path, $"{_safeProjectName}.cpp")))
            {
                var output = generator.Render(assemblyLookup);
                headerFile.Write(output);
            }
        }

        private void GenerateAssemblyHeader()
        {
            int staticFieldCount = 0;

            var assemblyData = new AssemblyDeclaration()
            {
                Name = _assemblyName,
                ShortName = _safeProjectName,
                ShortNameUpper = _safeProjectName.ToUpperInvariant(),
                IsCoreLib = _isCoreLib
            };

            foreach (TypeDefinition c in _tablesContext.TypeDefinitionTable.Items)
            {
                if (ShouldIncludeType(c))
                {
                    var classData = new Class()
                    {
                        AssemblyName = _safeProjectName,
                        Name = NativeMethodsCrc.GetClassName(c)
                    };

                    // If class name starts from <PrivateImplementationDetails>,
                    // then we need to exclude this class as actually this is static data object 
                    // described in metadata.
                    if (classData.Name.StartsWith("<PrivateImplementationDetails>"))
                    {
                        // Go to the next class. This metadata describes global variable, not a type
                        continue;
                    }

                    // static fields
                    int fieldCount = 0;
                    IEnumerable<FieldDefinition> staticFields = c.Fields.Where(f => f.IsStatic && !f.IsLiteral);

                    foreach (FieldDefinition f in staticFields)
                    {
                        FixFieldName(f, out string fixedFieldName, out string fieldWarning);

                        classData.StaticFields.Add(new StaticField()
                        {
                            Name = string.IsNullOrEmpty(fixedFieldName) ? f.Name : fixedFieldName,
                            ReferenceIndex = staticFieldCount + fieldCount++,
                            FieldWarning = fieldWarning
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
                        FixFieldName(f, out string fixedFieldName, out string fieldWarning);

                        if (_tablesContext.FieldsTable.TryGetFieldReferenceId(f, false, out ushort fieldRefId))
                        {
                            classData.InstanceFields.Add(new InstanceField()
                            {
                                Name = string.IsNullOrEmpty(fixedFieldName) ? f.Name : fixedFieldName,
                                ReferenceIndex = firstInstanceFieldId++,
                                FieldWarning = fieldWarning
                            });
                        }

                        fieldCount++;
                    }

                    // methods
                    if (c.HasMethods)
                    {
                        foreach (var m in nanoTablesContext.GetOrderedMethods(c.Methods))
                        {
                            var rva = _tablesContext.ByteCodeTable.GetMethodRva(m);

                            if (rva == 0xFFFF &&
                                !m.IsAbstract)
                            {
                                classData.Methods.Add(new MethodStub()
                                {
                                    Declaration = NativeMethodsCrc.GetMethodName(m)
                                });
                            }
                        }
                    }

                    // anything to add to the header?
                    if (classData.StaticFields.Count > 0 ||
                        classData.InstanceFields.Count > 0 ||
                        classData.Methods.Count > 0)
                    {
                        assemblyData.Classes.Add(classData);
                    }
                }
            }

            // enums have to be processed separatly
            foreach (var e in _tablesContext.TypeDefinitionTable.EnumDeclarations)
            {
                // check if enum is to exclude
                if (nanoTablesContext.ClassNamesToExclude.Contains(e.FullName) ||
                    nanoTablesContext.ClassNamesToExclude.Contains(e.Name))
                {
                    continue;
                }

                assemblyData.Enums.Add(e);
            }

            FormatCompiler compiler = new FormatCompiler();
            Generator generator = compiler.Compile(SkeletonTemplates.AssemblyHeaderTemplate);

            // create stubs directory
            Directory.CreateDirectory(_path);

            // output header file
            using (var headerFile = File.CreateText(Path.Combine(_path, $"{_safeProjectName}.h")))
            {
                var output = generator.Render(assemblyData);
                headerFile.Write(output);
            }
        }

        /// <summary>
        /// Fix field name to a valid C++ identifier.
        /// </summary>
        /// <param name="field">The field definition to work on.</param>
        /// <param name="fixedFieldName">The fixed field name, or an <see cref="Empty"/> string if no fix is needed.</param>
        /// <param name="fieldWarning">The warning message to be added to the field declaration, or empty if no warning is needed.</param>
        private static void FixFieldName(
            FieldDefinition field,
            out string fixedFieldName,
            out string fieldWarning)
        {
            fixedFieldName = string.Empty;
            fieldWarning = string.Empty;

            if (Regex.IsMatch(field.Name, @"<\w+>k__BackingField"))
            {
                fixedFieldName = $"{field.Name.Replace("<", "").Replace(">k__BackingField", "")}";
                fieldWarning = $"// renamed backing field '{field.Name}'";
            }
        }

        private int GetInstanceFieldsOffset(TypeDefinition c)
        {
            // check if this type has a base type different from System.Object
            if (c.BaseType != null &&
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
            int fieldCount = 0;

            if (c.BaseType != null &&
                c.BaseType.FullName != "System.Object" &&
                c.BaseType.FullName != "System.MarshalByRefObject")
            {
                // get parent type fields count
                fieldCount = GetNestedFieldsCount(c.BaseType.Resolve());

                // now add the fields count from this type
                fieldCount += c.Fields.Count(f => !f.IsStatic && !f.IsLiteral);

                return fieldCount;
            }
            else
            {
                // get the fields count from this type
                return c.Fields.Count(f => !f.IsStatic && !f.IsLiteral);
            }
        }

        internal static bool ShouldIncludeType(TypeDefinition type)
        {
            return type.IncludeInStub()
                && !type.IsToExclude()
                && !nanoTablesContext.IgnoringAttributes.Contains(type.FullName);
        }
    }
}
