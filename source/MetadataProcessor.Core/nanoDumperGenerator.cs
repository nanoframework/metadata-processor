//
// Copyright (c) 2020 The nanoFramework project contributors
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using Stubble.Core.Builders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nanoFramework.Tools.MetadataProcessor.Core
{
    /// <summary>
    /// Dumps details for parsed assemblies and PE files.
    /// </summary>
    public sealed class nanoDumperGenerator
    {
        private readonly nanoTablesContext _tablesContext;
        private readonly string _path;

        public nanoDumperGenerator(
                    nanoTablesContext tablesContext,
                    string path)
        {
            _tablesContext = tablesContext;
            _path = path;
        }

        public void DumpAll()
        {
            var dumpTable = new DumpAllTable();

            DumpAssemblyReferences(dumpTable);
            //DumpModuleReferences(dumpTable);
            DumpTypeReferences(dumpTable);

            DumpTypeDefinitions(dumpTable);
            DumpCustomAttributes(dumpTable);
            DumpUserStrings(dumpTable);

            var stubble = new StubbleBuilder().Build();

            using (var dumpFile = File.CreateText(_path))
            {
                var output = stubble.Render(DumpTemplates.DumpAllTemplate, dumpTable);
                dumpFile.Write(output);
            }
        }

        private void DumpCustomAttributes(DumpAllTable dumpTable)
        {
            foreach (var a in _tablesContext.AssemblyDefinition.CustomAttributes)
            {

            }
        }

        private void DumpUserStrings(DumpAllTable dumpTable)
        {
            foreach (var s in _tablesContext.StringTable.GetItems().OrderBy(i => i.Value))
            {
                // don't output the empty string
                if(s.Value == 0)
                {
                    continue;
                }

                // fake the metadata token from the ID
                var stringMetadataToken = new MetadataToken(TokenType.String, s.Value);

                dumpTable.UserStrings.Add(
                    new UserString()
                    {
                        ReferenceId = stringMetadataToken.ToInt32().ToString("x8"),
                        Content = s.Key
                });
            }
        }

        private void DumpTypeDefinitions(DumpAllTable dumpTable)
        {
            foreach (var t in _tablesContext.TypeDefinitionTable.Items)
            {
                // fill type definition
                var typeDef = new TypeDef()
                {
                   ReferenceId = t.MetadataToken.ToInt32().ToString("x8"),
                   Name = t.FullName
                };

                var typeFlags = (uint)nanoTypeDefinitionTable.GetFlags(t);
                typeDef.Flags = typeFlags.ToString("x8");

                if (t.BaseType != null)
                {
                    typeDef.ExtendsType = t.BaseType.MetadataToken.ToInt32().ToString("x8");
                }
                else
                {
                    var token = new MetadataToken(TokenType.TypeRef, 0);
                    typeDef.ExtendsType = token.ToInt32().ToString("x8");
                }
                
                if(t.DeclaringType != null)
                {
                    typeDef.EnclosedType = t.DeclaringType.MetadataToken.ToInt32().ToString("x8");
                }
                else
                {
                    var token = new MetadataToken(TokenType.TypeDef, 0);
                    typeDef.EnclosedType = token.ToInt32().ToString("x8");
                }

                // list type fields
                foreach (var f in t.Fields)
                {
                    uint att = (uint)f.Attributes;

                    var fieldDef = new FieldDef()
                    {
                        ReferenceId = f.MetadataToken.ToInt32().ToString("x8"),
                        Name = f.Name,
                        Flags = att.ToString("x8"),
                        Attributes = att.ToString("x8"),
                        Signature = PrintSignatureForType(f.FieldType)
                    };

                    typeDef.FieldDefinitions.Add(fieldDef);
                }

                // list type methods
                foreach (var m in t.Methods)
                {
                    var methodDef = new MethodDef()
                    {
                        ReferenceId = m.MetadataToken.ToInt32().ToString("x8"),
                        Name = m.Name,
                        RVA = m.RVA.ToString("x8"),
                        Implementation = "Implementation",
                        Signature = PrintSignatureForMethod(m)
                    };

                    var methodFlags = nanoMethodDefinitionTable.GetFlags(m);
                    methodDef.Flags = methodFlags.ToString("x8");

                    if (m.HasBody)
                    {
                        // locals
                        if (m.Body.HasVariables)
                        {
                            methodDef.Locals = $"{m.Body.Variables.Count.ToString()}: {PrintSignatureForLocalVar(m.Body.Variables)}";
                        }

                        // exceptions
                        foreach (var eh in m.Body.ExceptionHandlers)
                        {
                            var h = new ExceptionHandler();

                            if (eh.HandlerType == Mono.Cecil.Cil.ExceptionHandlerType.Filter)
                            {
                                h.Handler = "THIS IS AN EXCEPTION HANDLER";
                            }
                            else
                            {
                                h.Handler = "THIS IS ANoTHER EXCEPTION HANDLER";
                            }

                            methodDef.ExceptionHandlers.Add(h);
                        }

                        methodDef.ILCodeInstructionsCount = m.Body.Instructions.Count.ToString();

                        // IL code
                        foreach (var instruction in m.Body.Instructions)
                        {
                            ILCode ilCode = new ILCode();

                            ilCode.IL += instruction.OpCode.Name.PadRight(12);

                            if (instruction.Operand != null)
                            {
                                if (instruction.OpCode.OperandType == Mono.Cecil.Cil.OperandType.InlineField ||
                                    instruction.OpCode.OperandType == Mono.Cecil.Cil.OperandType.InlineMethod ||
                                    instruction.OpCode.OperandType == Mono.Cecil.Cil.OperandType.InlineType ||
                                    instruction.OpCode.OperandType == Mono.Cecil.Cil.OperandType.InlineTok ||
                                    instruction.OpCode.OperandType == Mono.Cecil.Cil.OperandType.InlineSig)
                                {
                                    ilCode.IL += $"[{((IMetadataTokenProvider)instruction.Operand).MetadataToken.ToInt32().ToString("x8")}]";
                                }
                                else if (instruction.OpCode.OperandType == Mono.Cecil.Cil.OperandType.InlineString)
                                {
                                    // strings need a different processing
                                    // get string ID from table
                                    var stringReferenceId = _tablesContext.StringTable.GetOrCreateStringId((string)instruction.Operand, true);

                                    // fake the metadata token from the ID
                                    var stringMetadataToken = new MetadataToken(TokenType.String, stringReferenceId);

                                    ilCode.IL += $"[{stringMetadataToken.ToInt32().ToString("x8")}]";
                                }

                            }

                            methodDef.ILCode.Add(ilCode);
                        }
                    }

                    typeDef.MethodDefinitions.Add(methodDef);
                }

                // list interface implementations
                foreach (var i in t.Interfaces)
                {
                    typeDef.InterfaceDefinitions.Add(
                        new InterfaceDef()
                        {
                            ReferenceId = i.MetadataToken.ToInt32().ToString("x8"),
                            Interface = i.InterfaceType.MetadataToken.ToInt32().ToString("x8")
                        });
                }

                dumpTable.TypeDefinitions.Add(typeDef);
            }
        }

        private void DumpTypeReferences(DumpAllTable dumpTable)
        {
            foreach (var t in _tablesContext.TypeReferencesTable.Items)
            {
                ushort refId;

                var typeRef = new TypeRef()
                {
                    Name = t.Name,
                    Scope = t.Scope.MetadataScopeType.ToString("x8")
                };

                if (_tablesContext.TypeReferencesTable.TryGetTypeReferenceId(t, out refId))
                {
                    typeRef.ReferenceId = "0x" + refId.ToString("x8");
                    typeRef.Name = t.Name;
                }

                // TODO 
                // list member refs

                dumpTable.TypeReferences.Add(typeRef);
            }
        }

        private void DumpModuleReferences(DumpAllTable dumpTable)
        {
            throw new NotImplementedException();
        }

        private void DumpAssemblyReferences(DumpAllTable dumpTable)
        {
            foreach(var a in _tablesContext.AssemblyReferenceTable.Items)
            {
                dumpTable.AssemblyReferences.Add(new AssemblyRef()
                {
                    Name = a.Name,
                    ReferenceId = "0x" + _tablesContext.AssemblyReferenceTable.GetReferenceId(a).ToString("x8"),
                    Flags = "0x"
                });
            }
        }

        private string PrintSignatureForType(TypeReference type)
        {
            nanoCLR_DataType dataType;
            if (nanoSignaturesTable.PrimitiveTypes.TryGetValue(type.FullName, out dataType))
            {
                switch(dataType)
                {
                    case nanoCLR_DataType.DATATYPE_VOID:
                    case nanoCLR_DataType.DATATYPE_BOOLEAN:
                    case nanoCLR_DataType.DATATYPE_CHAR:
                    case nanoCLR_DataType.DATATYPE_I1:
                    case nanoCLR_DataType.DATATYPE_U1:
                    case nanoCLR_DataType.DATATYPE_I2:
                    case nanoCLR_DataType.DATATYPE_U2:
                    case nanoCLR_DataType.DATATYPE_I4:
                    case nanoCLR_DataType.DATATYPE_U4:
                    case nanoCLR_DataType.DATATYPE_I8:
                    case nanoCLR_DataType.DATATYPE_U8:
                    case nanoCLR_DataType.DATATYPE_R4:
                    case nanoCLR_DataType.DATATYPE_R8:
                    case nanoCLR_DataType.DATATYPE_STRING:
                    case nanoCLR_DataType.DATATYPE_BYREF:
                    case nanoCLR_DataType.DATATYPE_OBJECT:
                        return dataType.ToString().Replace("DATATYPE_", "");
                }
            }

            if (type.MetadataType == MetadataType.Class)
            {
                StringBuilder classSig = new StringBuilder("CLASS [");
                classSig.Append(type.MetadataToken.ToInt32().ToString("x8"));
                classSig.Append("]");

                return classSig.ToString();
            }

            if (type.MetadataType == MetadataType.ValueType)
            {
                StringBuilder valueTypeSig = new StringBuilder("VALUETYPE [");
                valueTypeSig.Append(type.MetadataToken.ToInt32().ToString("x8"));
                valueTypeSig.Append("]");

                return valueTypeSig.ToString();
            }

            if (type.IsArray)
            {
                StringBuilder arraySig = new StringBuilder("SZARRAY ");
                arraySig.Append(PrintSignatureForType(type.GetElementType()));

                return arraySig.ToString();
            }

            return "";
        }

        private string PrintSignatureForMethod(MethodReference method)
        {
            var sig = new StringBuilder(PrintSignatureForType(method.ReturnType));

            sig.Append("(");

            foreach(var p in method.Parameters)
            {
                sig.Append(" ");
                sig.Append(PrintSignatureForType(p.ParameterType));
                sig.Append(" , ");
            }

            // remove trailing", "
            if (method.Parameters.Count > 0)
            {
                sig.Remove(sig.Length - 2, 2);
            }
            else
            {
                sig.Append(" ");
            }

            sig.Append(")");

            return sig.ToString();
        }


        private string PrintSignatureForLocalVar(Collection<VariableDefinition> variables)
        {
            StringBuilder sig = new StringBuilder();
            sig.Append("{");

            foreach (var l in variables)
            {
                sig.Append(" ");
                sig.Append(PrintSignatureForType(l.VariableType));
                sig.Append(" , ");
            }

            // remove trailing", "
            if (variables.Count > 0)
            {
                sig.Remove(sig.Length - 2, 2);
            }
            else
            {
                sig.Append(" ");
            }

            sig.Append("}");

            return sig.ToString();
        }
    }
}
