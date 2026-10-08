// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mustache;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;

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
            if (!_tablesContext.NativeContract.IsEmpty)
            {
                // create <assembly>.h with the structs declarations
                GenerateAssemblyHeader();

                // generate <assembly>.cpp with the lookup definition
                GenerateAssemblyLookup();

                // generate stub files for classes, headers and marshalling code, if required
                GenerateStubs();

                // output native contract hash so it shows in build log
                Console.WriteLine("++++++++++++++++++++++++++++++++++++++++");
                Console.WriteLine($"+ Native contract hash: 0x{_tablesContext.NativeContract.Hash.ToString("X8")} +");
                Console.WriteLine("++++++++++++++++++++++++++++++++++++++++");
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
            var classList = new AssemblyClassTable
            {
                AssemblyName = _tablesContext.AssemblyDefinition.Name.Name,
                ProjectName = _safeProjectName,
                IsInterop = !_withoutInteropCode
            };

            foreach (TypeDefinition c in _tablesContext.TypeDefinitionTable.Items)
            {
                if (ShouldIncludeType(c))
                {
                    string _safeClassName = NativeContract.GetSafeClassName(c);

                    var classStubs = new AssemblyClassStubs
                    {
                        AssemblyName = _name,
                        ClassHeaderFileName = _safeClassName,
                        ClassName = NativeContract.CleanupGenericName(c.Name),
                        ShortNameUpper = $"{_assemblyName}_{_safeProjectName}_{_safeClassName}".ToUpper(),
                        RootNamespace = _assemblyName,
                        ProjectName = _safeProjectName,
                        HeaderFileName = _safeProjectName
                    };

                    classList.Classes.Add(new Class()
                    {
                        Name = _safeClassName
                    });
                    classList.HeaderFileName = classStubs.HeaderFileName;

                    foreach (var m in nanoTablesContext.GetOrderedMethods(c.Methods))
                    {
                        // check method inclusion: native methods only
                        if (_tablesContext.NativeContract.TryGetSlot(m, out _))
                        {
                            var newMethod = new MethodStub()
                            {
                                Declaration = $"Library_{_safeProjectName}_{_safeClassName}::{NativeContract.GetSafeMethodName(m)}"
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

                                declaration.Append($"{NativeContract.CleanupGenericName(m.Name)}");
                                declaration.Append("( ");

                                StringBuilder marshallingCall = new StringBuilder($"{NativeContract.CleanupGenericName(m.Name)}");
                                marshallingCall.Append("( ");

                                // loop through the parameters
                                if (m.HasParameters)
                                {
                                    int parameterIndex = 0;

                                    foreach (ParameterDefinition item in m.Parameters)
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

                            using (StreamWriter headerFile = File.CreateText(Path.Combine(_path, $"{_safeProjectName}_{_safeClassName}.cpp")))
                            {
                                string output = generator.Render(classStubs);
                                headerFile.Write(output);
                            }

                            // add class to list of classes with stubs
                            classList.ClassesWithStubs.Add(new ClassWithStubs()
                            {
                                Name = _safeClassName
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

                            using (StreamWriter headerFile = File.CreateText(Path.Combine(_path, $"{_safeProjectName}_{_safeClassName}.cpp")))
                            {
                                string output = generator.Render(classStubs);
                                headerFile.Write(output);
                            }

                            // marshal code
                            generator = compiler.Compile(SkeletonTemplates.ClassMarshallingCodeTemplate);

                            using (StreamWriter headerFile = File.CreateText(Path.Combine(_path, $"{_safeProjectName}_{_safeClassName}_mshl.cpp")))
                            {
                                string output = generator.Render(classStubs);
                                headerFile.Write(output);
                            }

                            // class header
                            generator = compiler.Compile(SkeletonTemplates.ClassHeaderTemplate);

                            using (StreamWriter headerFile = File.CreateText(Path.Combine(_path, $"{_safeProjectName}_{_safeClassName}.h")))
                            {
                                string output = generator.Render(classStubs);
                                headerFile.Write(output);
                            }

                            // add class to list of classes with stubs
                            classList.ClassesWithStubs.Add(new ClassWithStubs()
                            {
                                Name = _safeClassName
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
            NativeContract nativeContract = _tablesContext.NativeContract;

            var assemblyLookup = new AssemblyLookupTable()
            {
                IsCoreLib = _isCoreLib,
                Name = _assemblyName,
                AssemblyName = _tablesContext.AssemblyDefinition.Name.Name,
                HeaderFileName = _safeProjectName,
                NativeCRC32 = "0x" + nativeContract.Hash.ToString("X8")
            };

            // dense lookup table: only native methods, in native slot order
            foreach (NativeMethodSlot nativeMethod in nativeContract.Methods)
            {
                assemblyLookup.LookupTable.Add(new MethodStub()
                {
                    Declaration = $"Library_{_safeProjectName}_{nativeMethod.SafeClassName}::{nativeMethod.SafeMethodName}"
                });
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
            var assemblyData = new AssemblyDeclaration()
            {
                Name = _assemblyName,
                ShortName = _safeProjectName,
                ShortNameUpper = _safeProjectName.ToUpperInvariant(),
                IsCoreLib = _isCoreLib
            };

            // classes and structs (except compiler-generated ones) get a declaration with field constants and native methods
            foreach (NativeTypeLayout type in _tablesContext.NativeContract.Types)
            {
                var classData = new Class()
                {
                    AssemblyName = _safeProjectName,
                    Name = type.SafeClassName
                };

                // static fields
                foreach (NativeFieldConstant f in type.StaticFields)
                {
                    classData.StaticFields.Add(new StaticField()
                    {
                        Name = f.Name,
                        ReferenceIndex = f.Index,
                        FieldWarning = f.Warning
                    });
                }

                // instance fields
                foreach (NativeFieldConstant f in type.InstanceFields)
                {
                    classData.InstanceFields.Add(new InstanceField()
                    {
                        Name = f.Name,
                        ReferenceIndex = f.Index,
                        FieldWarning = f.Warning
                    });
                }

                // methods
                if (type.Type.HasMethods)
                {
                    foreach (var m in nanoTablesContext.GetOrderedMethods(type.Type.Methods))
                    {
                        if (_tablesContext.NativeContract.TryGetSlot(m, out _))
                        {
                            classData.Methods.Add(new MethodStub()
                            {
                                Declaration = NativeContract.GetSafeMethodName(m)
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

        internal static bool ShouldIncludeType(TypeDefinition type)
        {
            return NativeContract.IsStubIncludedType(type);
        }
    }
}
