//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using Mustache;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            DumpTypeSpecifications(dumpTable);
            DumpCustomAttributes(dumpTable);
            DumpStringHeap(dumpTable);
            DumpUserStrings(dumpTable);


            FormatCompiler compiler = new FormatCompiler();
            Generator generator = compiler.Compile(DumpTemplates.DumpAllTemplate);

            using (var dumpFile = File.CreateText(_path))
            {
                var output = generator.Render(dumpTable);
                dumpFile.Write(output);
            }
        }

        private void DumpCustomAttributes(DumpAllTable dumpTable)
        {
            foreach (var a in _tablesContext.TypeDefinitionTable.Items.Where(td => td.HasCustomAttributes))
            {
                foreach (var ma in a.Methods)
                {
                    if (ma.HasCustomAttributes)
                    {
                        var attribute = new AttributeCustom()
                        {
                            Name = a.Module.Assembly.Name.Name,
                            ReferenceId = ma.MetadataToken.ToInt32().ToString("x8"),
                            TypeToken = ma.CustomAttributes[0].Constructor.MetadataToken.ToInt32().ToString("x8")
                        };

                        if (ma.CustomAttributes[0].HasConstructorArguments)
                        {
                            foreach (var value in ma.CustomAttributes[0].ConstructorArguments)
                            {
                                attribute.FixedArgs.Add(BuildFixedArgsAttribute(value));
                            }
                        }

                        dumpTable.Attributes.Add(attribute);
                    }
                }

                foreach (var fa in a.Fields)
                {
                    if(fa.HasCustomAttributes)
                    {
                        var attribute = new AttributeCustom()
                        {
                            Name = a.Module.Assembly.Name.Name,
                            ReferenceId = fa.MetadataToken.ToInt32().ToString("x8"),
                            TypeToken = fa.CustomAttributes[0].Constructor.MetadataToken.ToInt32().ToString("x8")
                        };

                        if (fa.CustomAttributes[0].HasConstructorArguments)
                        {
                            foreach (var value in fa.CustomAttributes[0].ConstructorArguments)
                            {
                                attribute.FixedArgs.Add(BuildFixedArgsAttribute(value));
                            }
                        }

                        dumpTable.Attributes.Add(attribute);
                    }
                }

                var attribute1 = new AttributeCustom()
                {
                    Name = a.Module.Assembly.Name.Name,
                    ReferenceId = a.MetadataToken.ToInt32().ToString("x8"),
                    TypeToken = a.CustomAttributes[0].Constructor.MetadataToken.ToInt32().ToString("x8")
                };

                if (a.CustomAttributes[0].HasConstructorArguments)
                {
                    foreach (var value in a.CustomAttributes[0].ConstructorArguments)
                    {
                        attribute1.FixedArgs.Add(BuildFixedArgsAttribute(value));
                    }
                }

                dumpTable.Attributes.Add(attribute1);
            }
        }

        private AttFixedArgs BuildFixedArgsAttribute(CustomAttributeArgument value)
        {
            var serializationType = value.Type.ToSerializationType();

            var newArg = new AttFixedArgs()
            {
                Options = ((byte)serializationType).ToString("X2"),
                Numeric = 0.ToString("X16"),
                Text = "",
            };

            switch(serializationType)
            {
                case nanoSerializationType.ELEMENT_TYPE_BOOLEAN:
                    newArg.Numeric = ((bool)value.Value) ? 1.ToString("X16") : 0.ToString("X16");
                    break;

                case nanoSerializationType.ELEMENT_TYPE_STRING:
                    newArg.Text = (string)value.Value;
                    break;

                default:
                    newArg.Numeric = ((int)value.Value).ToString("X16");
                    break;
            }

            return newArg;
        }

        private void DumpStringHeap(DumpAllTable dumpTable)
        {
            foreach (var s in _tablesContext.StringTable.GetItems().OrderBy(i => i.Value))
            {
                // don't output the empty string
                if (s.Value == 0)
                {
                    continue;
                }

                dumpTable.StringHeap.Add(
                    new HeapString()
                    {
                        ReferenceId = s.Value.ToString("x8"),
                        Content = s.Key
                    });
            }
        }


        private void DumpUserStrings(DumpAllTable dumpTable)
        {
            foreach (var s in _tablesContext.StringTable.GetItems().OrderBy(i => i.Value).Where(i => i.Value > _tablesContext.StringTable.LastPreAllocatedId))
            {
                // don't output the empty string
                if (s.Value == 0)
                {
                    continue;
                }

                // fake the metadata token from the ID
                var stringMetadataToken = new MetadataToken(TokenType.String, _tablesContext.StringTable.GetOrCreateStringId(s.Key, true));

                dumpTable.UserStrings.Add(
                    new UserString()
                    {
                        ReferenceId = stringMetadataToken.ToInt32().ToString("x8"),
                        Length = s.Key.Length.ToString("x2"),
                        Content = s.Key
                });
            }
        }

        private void DumpTypeDefinitions(DumpAllTable dumpTable)
        {
            foreach (var t in _tablesContext.TypeDefinitionTable.Items.OrderBy(tr => tr.MetadataToken.ToInt32()))
            {
                // fill type definition
                var typeDef = new TypeDef()
                {
                   ReferenceId = t.MetadataToken.ToInt32().ToString("x8"),
                };

                if(t.IsNested)
                {
                    typeDef.Name = t.Name;
                }
                else
                {
                    typeDef.Name = t.FullName;
                }

                var typeFlags = (uint)nanoTypeDefinitionTable.GetFlags(
                    t,
                    _tablesContext.MethodDefinitionTable);

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

                // list generic parameters
                foreach (var gp in t.GenericParameters)
                {
                    var genericParam = new GenericParam()
                    {
                        Position = gp.Position.ToString(),
                        GenericParamToken = gp.MetadataToken.ToInt32().ToString("x8"),
                        Name = new string('!', gp.Position + 1) + gp.FullName,
                        Owner = gp.Owner.MetadataToken.ToInt32().ToString("x8"),
                        Signature =  gp.DeclaringType.Name
                    };

                    typeDef.GenericParameters.Add(genericParam);
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
                        Signature = f.FieldType.TypeSignatureAsString()
                    };

                    typeDef.FieldDefinitions.Add(fieldDef);
                }

                // list type methods
                foreach (var m in t.Methods)
                {
                    var methodDef = new MethodDef()
                    {
                        ReferenceId = m.MetadataToken.ToInt32().ToString("x8"),
                        Name = m.FullName(),
                        RVA = m.RVA.ToString("x8"),
                        Implementation = "00000000",
                        Signature = PrintSignatureForMethod(m)
                    };

                    var methodFlags = nanoMethodDefinitionTable.GetFlags(m);
                    methodDef.Flags = methodFlags.ToString("x8");

                    if (m.HasBody)
                    {
                        // locals
                        if (m.Body.HasVariables)
                        {
                            methodDef.Locals = $"{m.Body.Variables.Count}: {PrintSignatureForLocalVar(m.Body.Variables)}";
                        }

                        // exceptions
                        foreach (var eh in m.Body.ExceptionHandlers)
                        {
                            var h = new ExceptionHandler
                            {
                                Handler = $"{(int)eh.HandlerType:x2} " +
                                $"{eh.TryStart?.Offset.ToString("x8")}->{eh.TryEnd?.Offset.ToString("x8")} " +
                                $"{eh.HandlerStart?.Offset.ToString("x8")}->{eh.HandlerEnd?.Offset.ToString("x8")} "
                            };

                            if (eh.CatchType != null)
                            {
                                h.Handler += $"{eh.CatchType.MetadataToken.ToInt32():x8}";
                            }
                            else
                            {
                                h.Handler += "00000000";
                            }

                            methodDef.ExceptionHandlers.Add(h);
                        }

                        methodDef.ILCodeInstructionsCount = m.Body.Instructions.Count.ToString();

                        // IL code
                        foreach (var instruction in m.Body.Instructions)
                        {
                            MetadataToken fakeToken = new MetadataToken();
                            string typeName = string.Empty;

                            ILCode ilCode = new ILCode();
                            
                            ilCode.IL += $"IL_{instruction.Offset:x4}:  ";

                            ilCode.IL += instruction.OpCode.Name.PadRight(12);

                            if (instruction.Operand != null)
                            {
                                if (instruction.OpCode.OperandType == OperandType.InlineMethod)
                                {
                                    typeName = (instruction.Operand as MethodReference).FullName;

                                    MethodReference methodReference = (MethodReference)instruction.Operand;

                                    if (!_tablesContext.MethodReferencesTable.TryGetMethodReferenceId(methodReference, out ushort referenceId))
                                    {
                                        if(!_tablesContext.MethodDefinitionTable.TryGetMethodReferenceId(methodReference.Resolve(), out referenceId))
                                        {
                                            Debug.Fail("Can't find method in any table.");
                                        }
                                    }

                                    fakeToken = new MetadataToken(TokenType.Method, referenceId);

                                    ilCode.IL += $"{typeName}[{fakeToken.ToInt32():x8}]";
                                }
                                else if (instruction.OpCode.OperandType == OperandType.InlineField ||
                                    instruction.OpCode.OperandType == OperandType.InlineTok ||
                                    instruction.OpCode.OperandType == OperandType.InlineSig)
                                {
                                    ilCode.IL += $"[{((IMetadataTokenProvider)instruction.Operand).MetadataToken.ToInt32():x8}]";
                                }
                                else if (instruction.OpCode.OperandType == OperandType.InlineType)
                                {

                                    if (instruction.Operand is TypeSpecification &&
                                        _tablesContext.TypeSpecificationsTable.TryGetTypeReferenceId(instruction.Operand as TypeSpecification, out ushort referenceId))
                                    {
                                        typeName = (instruction.Operand as TypeSpecification).FullName;

                                        fakeToken = new MetadataToken(TokenType.TypeSpec, referenceId);
                                    }
                                    else if (_tablesContext.TypeReferencesTable.TryGetTypeReferenceId(instruction.Operand as TypeReference, out referenceId))
                                    {
                                        typeName = (instruction.Operand as TypeReference).FullName;

                                        fakeToken = new MetadataToken(TokenType.TypeRef, referenceId);
                                    }
                                    else if (instruction.Operand is TypeDefinition &&
                                             _tablesContext.TypeDefinitionTable.TryGetTypeReferenceId(instruction.Operand as TypeDefinition, out referenceId))
                                    {
                                        typeName = (instruction.Operand as TypeDefinition).FullName;

                                        fakeToken = new MetadataToken(TokenType.TypeDef, referenceId);
                                    }
                                    else if(instruction.Operand is GenericParameter &&
                                            _tablesContext.GenericParamsTable.TryGetParameterId(instruction.Operand as GenericParameter, out referenceId))
                                    {
                                        typeName = (instruction.Operand as GenericParameter).TypeSignatureAsString();

                                        TypeReference typeReference = (TypeReference)instruction.Operand;

                                        if (typeReference is TypeSpecification ||
                                            typeReference is GenericParameter)
                                        {
                                            referenceId = _tablesContext.TypeSpecificationsTable.GetOrCreateTypeSpecificationId(typeReference);
                                            
                                            fakeToken = new MetadataToken(TokenType.TypeSpec, referenceId);
                                        }
                                        else if (_tablesContext.TypeReferencesTable.TryGetTypeReferenceId(typeReference, out referenceId))
                                        {
                                            fakeToken = new MetadataToken(TokenType.TypeRef, referenceId);
                                        }
                                        else
                                        {
                                            if (!_tablesContext.TypeDefinitionTable.TryGetTypeReferenceId(typeReference.Resolve(), out referenceId))
                                            {
                                                fakeToken = new MetadataToken(TokenType.TypeDef, referenceId);
                                            }
                                            else
                                            {
                                                Debug.Fail("Can't find generic parameter type in any table.");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Debug.Fail("Can't find operand type in any table.");
                                    }

                                    ilCode.IL += $"{typeName}[{fakeToken.ToInt32():x8}]";
                                }
                                else if (instruction.OpCode.OperandType == OperandType.InlineString)
                                {
                                    // strings need a different processing
                                    // get string ID from table
                                    var stringReferenceId = _tablesContext.StringTable.GetOrCreateStringId((string)instruction.Operand, true);

                                    // fake the metadata token from the ID
                                    var stringMetadataToken = new MetadataToken(TokenType.String, stringReferenceId);

                                    ilCode.IL += $"\"{instruction.Operand}\" [{stringMetadataToken.ToInt32():x8}]";
                                }

                            }

                            ilCode.IL = ilCode.IL.TrimEnd();

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
            foreach (var t in _tablesContext.TypeReferencesTable.Items.OrderBy(tr => tr.MetadataToken.ToInt32()))
            {
                ushort refId;

                var typeRef = new TypeRef()
                {
                    Name = t.FullName,
                    // need to add 1 to match the index on the old MDP
                    Scope = new MetadataToken(TokenType.AssemblyRef, _tablesContext.TypeReferencesTable.GetScope(t) + 1).ToInt32().ToString("x8")
                };

                if (_tablesContext.TypeReferencesTable.TryGetTypeReferenceId(t, out refId))
                {
                    typeRef.ReferenceId = t.MetadataToken.ToInt32().ToString("x8");
                }

                // list member refs               
                foreach(var m in _tablesContext.MethodReferencesTable.Items.Where(mr => mr.DeclaringType == t))
                {
                    var memberRef = new MemberRef()
                    {
                        Name = m.Name
                    };

                    if (_tablesContext.MethodReferencesTable.TryGetMethodReferenceId(m, out refId))
                    {
                        memberRef.ReferenceId = m.MetadataToken.ToInt32().ToString("x8");
                        memberRef.Signature = PrintSignatureForMethod(m);
                    }

                    typeRef.MemberReferences.Add(memberRef);
                }

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
                    // need to add 1 to match the index on the old MDP
                    ReferenceId = new MetadataToken(TokenType.AssemblyRef, _tablesContext.AssemblyReferenceTable.GetReferenceId(a) + 1).ToInt32().ToString("x8"),
                    Flags = "00000000"
                });
            }
        }

        private void DumpTypeSpecifications(DumpAllTable dumpTable)
        {
            foreach (var t in _tablesContext.TypeSpecificationsTable.GetItems().OrderBy(ts => ts.Value))
            {
                var typeSpec = new TypeSpec();

                // build name
                StringBuilder typeSpecName = new StringBuilder();

                if(t.Key is GenericParameter)
                {
                    var genericParam = t.Key as GenericParameter;

                    typeSpecName.Append(t.Key.MetadataType);

                    if (genericParam.Owner is TypeDefinition)
                    {
                        typeSpecName.Append("!");
                    }
                    if (genericParam.Owner is MethodDefinition)
                    {
                        typeSpecName.Append("!!");
                    }

                    typeSpecName.Append(genericParam.Owner.GenericParameters.IndexOf(genericParam));

                    typeSpecName.Append($" ({genericParam.FullName})");

                    typeSpec.Name = typeSpecName.ToString();

                    // get token
                    if (_tablesContext.TypeSpecificationsTable.TryGetTypeReferenceId(t.Key, out ushort refId))
                    {
                        typeSpec.ReferenceId = new MetadataToken(TokenType.TypeSpec, refId).ToInt32().ToString("x8");
                    }
                }
                else
                {
                    // type is a GenericInstance
                    // can't compare with Cecil MetadataToken because the tables have been cleaned-up and re-indexed

                    typeSpec.Name = t.Key.FullName;

                    // get token
                    if (_tablesContext.TypeSpecificationsTable.TryGetTypeReferenceId(t.Key, out ushort typeSpecId))
                    {
                        typeSpec.ReferenceId = new MetadataToken(TokenType.TypeSpec, typeSpecId).ToInt32().ToString("x8");
                    }

                    foreach (var mr in _tablesContext.MethodReferencesTable.Items)
                    {
                        if (_tablesContext.TypeSpecificationsTable.TryGetTypeReferenceId(mr.DeclaringType, out ushort referenceId) &&
                            referenceId == t.Value)
                        {
                            var memberRef = new MemberRef()
                            {
                                Name = mr.Name
                            };

                            if (_tablesContext.MethodReferencesTable.TryGetMethodReferenceId(mr, out ushort methodRefId))
                            {
                                memberRef.ReferenceId = new MetadataToken(TokenType.TypeSpec, methodRefId).ToInt32().ToString("x8");
                            }

                            typeSpec.MemberReferences.Add(memberRef);
                        }
                    }

                    Debug.Assert(typeSpec.MemberReferences.Count > 0, $"Couldn't find any MethodRef for TypeSpec[{t.Value}] {t.Key.FullName}");
                }

                dumpTable.TypeSpecifications.Add(typeSpec);
            }
        }

        private string PrintSignatureForMethod(MethodReference method)
        {
            var sig = new StringBuilder(method.ReturnType.TypeSignatureAsString());

            sig.Append("( ");

            foreach(var p in method.Parameters)
            {
                sig.Append(p.ParameterType.TypeSignatureAsString());
                sig.Append(", ");
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

            sig.Append(" )");

            return sig.ToString();
        }


        private string PrintSignatureForLocalVar(Collection<VariableDefinition> variables)
        {
            StringBuilder sig = new StringBuilder();
            sig.Append("( ");

            foreach (var l in variables)
            {
                sig.Append($"[{l.Index}] ");
                sig.Append(l.VariableType.TypeSignatureAsString());
                sig.Append(", ");
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

            sig.Append(" )");

            return sig.ToString();
        }
    }
}
