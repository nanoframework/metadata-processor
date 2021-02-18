//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

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
            Token = new Token(item.MetadataToken, nanoClrTable.TBL_TypeDef.ToNanoTokenType() | nanoToken);

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

            Token = new Token(method.MetadataToken, nanoClrTable.TBL_MethodDef.ToNanoTokenType() | methodToken);

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
            context.FieldsTable.TryGetFieldReferenceId(field, false, out ushort fieldToken);

            Token = new Token(field.MetadataToken, nanoClrTable.TBL_FieldDef.ToNanoTokenType() | fieldToken);

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
            Token = new Token(item.MetadataToken, nanoClrTable.TBL_GenericParam.ToNanoTokenType() | nanoToken);

            Name = item.FullName;
        }
    }

    public partial class TypeSpec
    {
        public TypeSpec(nanoTablesContext context, TypeReference item)
        {
            context.TypeSpecificationsTable.TryGetTypeReferenceId(item, out ushort nanoToken);

            // assume that real token index is the same as ours
            // need to add one because ours is 0 indexed
            var clrToken = new MetadataToken(TokenType.TypeSpec, nanoToken + 1);

            Token = new Token(clrToken, nanoClrTable.TBL_TypeSpec.ToNanoTokenType() | nanoToken);

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
                foreach (var mr in context.MethodReferencesTable.Items)
                {
                    if (context.TypeSpecificationsTable.TryGetTypeReferenceId(mr.DeclaringType, out ushort referenceId) &&
                        referenceId == nanoToken)
                    {
                        if (context.MethodReferencesTable.TryGetMethodReferenceId(mr, out referenceId))
                        {
                            Members.Add(new Member(mr, nanoClrTable.TBL_MethodRef.ToNanoTokenType() | nanoToken));
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
                            Members.Add(new Member(ms, nanoClrTable.TBL_MethodSpec.ToNanoTokenType() | nanoToken));
                        }
                    }
                }
            }
        }
    }

    public partial class Member
    {
        public Member(MethodReference mr, uint nanoToken)
        {
            Token = new Token(mr.MetadataToken, nanoClrTable.TBL_MethodRef.ToNanoTokenType() | nanoToken);

            Name = mr.Name;
        }
    }

    public partial class Token
    {
        public Token(MetadataToken metadataToken)
        {
            Clr = metadataToken.ToUInt32().ToString("X8", CultureInfo.InvariantCulture);
            NanoClr = "00000000";
        }

        public Token(MetadataToken metadataToken, uint nanoToken)
        {
            Clr = metadataToken.ToUInt32().ToString("X8", CultureInfo.InvariantCulture);
            NanoClr = nanoToken.ToString("X8", CultureInfo.InvariantCulture);
        }

        public Token(uint clrToken, uint nanoToken)
        {
            Clr = clrToken.ToString("X8", CultureInfo.InvariantCulture);
            NanoClr = nanoToken.ToString("X8", CultureInfo.InvariantCulture);
        }
    }
}
