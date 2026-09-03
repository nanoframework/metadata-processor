// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Mono.Cecil;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;

namespace nanoFramework.Tools.MetadataProcessor
{
    public partial class Pdbx
    {
        public Pdbx(nanoTablesContext context) => Assembly = new Assembly(context);
    }

    public partial class Assembly
    {
        private nanoTablesContext _context;

        public Assembly(nanoTablesContext context)
        {
            _context = context;

            Token = new Token(_context.AssemblyDefinition.MetadataToken);

            FileName = _context.AssemblyDefinition.MainModule.Name;

            Version = _context.AssemblyDefinition.Name.Version;

            Classes = WriteClasses(_context);

            GenericParams = WriteGenericParams(_context);

            TypeSpecs = WriteTypeSpecs(_context);
        }

        private List<Class> WriteClasses(nanoTablesContext context)
        {
            var classes = new List<Class>();

            context.TypeDefinitionTable.ForEachItems((nanoToken, item) => WriteClassInfo(classes, context, item, nanoToken));

            return classes;
        }

        private void WriteClassInfo(List<Class> classes, nanoTablesContext context, TypeDefinition item, uint nanoToken)
        {
            classes.Add(new Class(context, item, nanoToken));
        }

        private List<GenericParam> WriteGenericParams(nanoTablesContext context)
        {
            var genericParams = new List<GenericParam>();

            context.GenericParamsTable.ForEachItems((nanoToken, item) => WriteGenericParamInfo(genericParams, context, item, nanoToken));

            return genericParams;
        }

        private void WriteGenericParamInfo(List<GenericParam> genericParams, nanoTablesContext context, GenericParameter item, uint nanoToken)
        {
            genericParams.Add(new GenericParam(context, item, nanoToken));
        }

        private List<TypeSpec> WriteTypeSpecs(nanoTablesContext context)
        {
            var typeSpecs = new List<TypeSpec>();

            context.TypeSpecificationsTable.ForEachItems((nanoToken, item) => WriteTypeSpecInfo(typeSpecs, context, item));

            return typeSpecs;
        }

        private void WriteTypeSpecInfo(List<TypeSpec> typeSpecs, nanoTablesContext context, TypeReference item)
        {
            typeSpecs.Add(new TypeSpec(context, item));
        }
    }

    public partial class Class
    {
        public Class(nanoTablesContext context, TypeDefinition item, uint nanoToken)
        {
            Token = new Token(item.MetadataToken, NanoClrTable.TBL_TypeDef.ToNanoTokenType() | nanoToken);

            Name = item.FullName;
            IsEnum = item.IsEnum;
            NumGenericParams = item.GenericParameters.Count;
            IsGenericInstance = item.IsGenericInstance;

            Methods = new List<Method>();

            foreach (var method in item.Methods)
            {
                Methods.Add(new Method(context, method));
            }

            Fields = new List<Field>();

            foreach (var field in item.Fields)
            {
                Fields.Add(new Field(context, field));
            }
        }
    }

    public partial class Method
    {
        public Method(nanoTablesContext context, MethodDefinition method)
        {
            context.MethodDefinitionTable.TryGetMethodReferenceId(method, out ushort methodToken);

            Token = new Token(method.MetadataToken, NanoClrTable.TBL_MethodDef.ToNanoTokenType() | methodToken);

            Name = method.Name;
            NumParams = method.Parameters.Count;
            NumLocals = method.HasBody ? method.Body.Variables.Count : 0;
            NumGenericParams = method.GenericParameters.Count;
            IsGenericInstance = method.IsGenericInstance;
            HasByteCode = method.HasBody;

            ILMap = new List<IL>();

            // sanity check vars
            uint prevItem1 = 0;
            uint prevItem2 = 0;

            foreach (var offset in context.TypeDefinitionTable.GetByteCodeOffsets(method.MetadataToken.ToUInt32()))
            {
                if (prevItem1 > 0)
                {
                    // 1st pass, load prevs with current values
                    Debug.Assert(prevItem1 < offset.Item1);
                    Debug.Assert(prevItem2 < offset.Item2);
                }

                ILMap.Add(new IL(offset.Item1, offset.Item2));

                prevItem1 = offset.Item1;
                prevItem2 = offset.Item2;
            }
        }
    }

    public partial class Field
    {
        public Field(nanoTablesContext context, FieldDefinition field)
        {
            context.FieldsTable.TryGetFieldDefinitionId(field, false, out ushort fieldToken);

            Token = new Token(field.MetadataToken, NanoClrTable.TBL_FieldDef.ToNanoTokenType() | fieldToken);

            Name = field.Name;
        }
    }

    public partial class IL
    {
        public IL(uint clrToken, uint nanoToken)
        {
            Token = new Token(clrToken, nanoToken);
        }
    }

    public partial class GenericParam
    {
        public GenericParam(nanoTablesContext context, GenericParameter item, uint nanoToken)
        {
            Token = new Token(item.MetadataToken, NanoClrTable.TBL_GenericParam.ToNanoTokenType() | nanoToken);

            Name = item.FullName;
        }
    }

    public partial class TypeSpec
    {
        public TypeSpec(nanoTablesContext context, TypeReference item)
        {
            // sanity check for bug in Mono.Cecil where TypeSpecification instances may show with RID = 0
            if (!context.TypeSpecificationsTable.TryGetTypeReferenceId(item, out ushort nanoToken))
            {
                // OK to return here
                return;
            }

            var clrToken = new MetadataToken(TokenType.TypeSpec, nanoToken);

            Token = new Token(clrToken, NanoClrTable.TBL_TypeSpec.ToNanoTokenType() | nanoToken);

            if (item.IsGenericInstance)
            {
                Name = item.FixedFullName();
            }
            else if (item.IsGenericParameter)
            {
                var genericParam = item as GenericParameter;

                StringBuilder typeSpecName = new StringBuilder();

                if (genericParam.Owner is TypeDefinition)
                {
                    typeSpecName.Append("!");
                }
                if (genericParam.Owner is MethodDefinition)
                {
                    typeSpecName.Append("!!");
                }

                typeSpecName.Append(genericParam.Owner.GenericParameters.IndexOf(genericParam));

                Name = typeSpecName.ToString();
            }

            IsGenericInstance = item.IsGenericInstance;

            Members = new List<Member>();

            if (item.IsGenericInstance)
            {
                var genericInstance = (GenericInstanceType)item;

                // ElementType is always the open generic TypeDef (never itself a TypeSpecification), so the
                // local-class resolver is enough here. When it is declared in a different assembly this is left null.
                GenericTypeDef = ResolveLocalClassToken(context, genericInstance.ElementType);

                GenericArguments = new List<TypeSpecArg>();

                foreach (var argument in genericInstance.GenericArguments)
                {
                    GenericArguments.Add(BuildTypeSpecArg(context, argument));
                }

                foreach (var mr in context.MethodReferencesTable.Items)
                {
                    if (context.TypeSpecificationsTable.TryGetTypeReferenceId(mr.DeclaringType, out ushort referenceId) &&
                        referenceId == nanoToken)
                    {
                        if (context.MethodReferencesTable.TryGetMethodReferenceId(mr, out referenceId))
                        {
                            Members.Add(new Member(mr, NanoClrTable.TBL_MethodRef.ToNanoTokenType() | nanoToken));
                        }
                    }
                }

                foreach (var ms in context.MethodSpecificationTable.Items)
                {
                    if (context.TypeSpecificationsTable.TryGetTypeReferenceId(ms.DeclaringType, out ushort referenceId) &&
                        referenceId == nanoToken)
                    {
                        if (context.MethodSpecificationTable.TryGetMethodSpecificationId(ms, out ushort methodSpecId))
                        {
                            Members.Add(new Member(ms, NanoClrTable.TBL_MethodSpec.ToNanoTokenType() | nanoToken));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Builds the structured description of one generic type argument: a primitive element type, or the
        /// NanoCLR token of a class (TypeDef/TypeRef) or nested TypeSpec. Returns an argument with neither
        /// <see cref="TypeSpecArg.PrimitiveType"/> nor <see cref="TypeSpecArg.TypeToken"/> set when the
        /// argument cannot be resolved to a token (for example an unresolved generic parameter) -- callers
        /// are expected to treat that as "cannot fully describe this instance".
        /// </summary>
        private static TypeSpecArg BuildTypeSpecArg(nanoTablesContext context, TypeReference argumentType)
        {
            var arg = new TypeSpecArg();

            if (nanoSignaturesTable.PrimitiveTypes.TryGetValue(argumentType.FullName, out NanoCLRDataType dataType))
            {
                arg.IsPrimitive = true;
                arg.PrimitiveType = dataType.ToString();

                return arg;
            }

            arg.IsPrimitive = false;

            if (argumentType is TypeSpecification)
            {
                // nested generic instance (or array/pointer/by-ref type) -- only describable through its
                // own TypeSpec entry, which is always local to this assembly's TypeSpec table.
                arg.TypeToken = ResolveTypeSpecToken(context, argumentType);

                return arg;
            }

            // Ordinary class. Prefer a NanoCLR TypeDef token: it is unambiguous and cheap to resolve, but it
            // only exists when the class is declared in the assembly currently being processed -- the pdbx
            // model has no TypeRef list, so there is no way for the debugger to chase a foreign-assembly
            // TypeRef token back to a class.
            arg.ClassName = argumentType.FullName;
            arg.TypeToken = ResolveLocalClassToken(context, argumentType);

            return arg;
        }

        /// <summary>
        /// Resolves the NanoCLR token of a nested TypeSpec entry (a generic instance, array, pointer, or
        /// by-ref type -- anything Mono.Cecil models as a <see cref="TypeSpecification"/>).
        /// </summary>
        /// <returns>The token, or <see langword="null"/> when the type is not registered in this assembly TypeSpec table.</returns>
        private static Token ResolveTypeSpecToken(nanoTablesContext context, TypeReference typeSpecification)
        {
            if (context.TypeSpecificationsTable.TryGetTypeReferenceId(typeSpecification, out ushort typeSpecId))
            {
                var clrToken = new MetadataToken(TokenType.TypeSpec, typeSpecId);

                return new Token(clrToken, NanoClrTable.TBL_TypeSpec.ToNanoTokenType() | typeSpecId);
            }

            return null;
        }

        /// <summary>
        /// Resolves the NanoCLR TypeDef token of a class, but only when it is declared in the assembly
        /// currently being processed (see the remarks on <see cref="BuildTypeSpecArg"/> for why an external
        /// class cannot be addressed by token here).
        /// </summary>
        /// <returns>The token, or <see langword="null"/> when the type is not a local TypeDef.</returns>
        private static Token ResolveLocalClassToken(nanoTablesContext context, TypeReference typeReference)
        {
            TypeDefinition typeDefinition = typeReference as TypeDefinition ?? typeReference.Resolve();

            if (typeDefinition != null &&
                context.TypeDefinitionTable.TryGetTypeReferenceId(typeDefinition, out ushort typeDefId))
            {
                return new Token(typeDefinition.MetadataToken, NanoClrTable.TBL_TypeDef.ToNanoTokenType() | typeDefId);
            }

            return null;
        }
    }

    public partial class Member
    {
        public Member(MethodReference mr, uint nanoToken)
        {
            Token = new Token(mr.MetadataToken, NanoClrTable.TBL_MethodRef.ToNanoTokenType() | nanoToken);

            Name = mr.Name;
        }
    }

    public partial class Token
    {
        public Token(MetadataToken metadataToken)
        {
            CLR = metadataToken.ToUInt32().ToString("X8", CultureInfo.InvariantCulture);
            NanoCLR = "00000000";
        }

        public Token(MetadataToken metadataToken, uint nanoToken)
        {
            CLR = metadataToken.ToUInt32().ToString("X8", CultureInfo.InvariantCulture);
            NanoCLR = nanoToken.ToString("X8", CultureInfo.InvariantCulture);
        }

        public Token(uint clrToken, uint nanoToken)
        {
            CLR = clrToken.ToString("X8", CultureInfo.InvariantCulture);
            NanoCLR = nanoToken.ToString("X8", CultureInfo.InvariantCulture);
        }
    }
}
