// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace nanoFramework.Tools.MetadataProcessor
{
    //////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Any changes in these classes need to be replicated on the same class at the Visual Studio extension //
    //////////////////////////////////////////////////////////////////////////////////////////////////////////

    public partial class Pdbx
    {
        public Assembly Assembly { get; set; }
    }

    public partial class Assembly
    {
        public Token Token { get; set; }

        public string FileName { get; set; }

        [JsonConverter(typeof(VersionConverter))]
        public Version Version { get; set; }

        public List<Class> Classes { get; set; }

        public List<GenericParam> GenericParams { get; set; }

        public List<TypeSpec> TypeSpecs { get; set; }
    }

    public partial class Class
    {
        public Token Token { get; set; }

        public string Name { get; set; }
        public bool IsEnum { get; set; } = false;
        public int NumGenericParams { get; set; } = 0;
        public bool IsGenericInstance { get; set; } = false;

        public List<Method> Methods { get; set; }
        public List<Field> Fields { get; set; }
    }

    public partial class Field
    {
        public string Name { get; set; }
        public Token Token { get; set; }
    }

    public partial class Method
    {
        public Token Token { get; set; }

        public string Name { get; set; }
        public int NumParams { get; set; } = 0;
        public int NumLocals { get; set; } = 0;
        public int NumGenericParams { get; set; } = 0;
        public bool IsGenericInstance { get; set; } = false;
        public bool HasByteCode { get; set; } = false;
        public List<IL> ILMap { get; set; }
    }

    public partial class IL
    {
        public Token Token { get; set; }
    }

    public partial class GenericParam
    {
        public Token Token { get; set; }

        public string Name { get; set; }
    }

    public partial class TypeSpec
    {
        public Token Token { get; set; }

        public string Name { get; set; }
        public bool IsGenericInstance { get; set; } = false;

        public List<Member> Members { get; set; }

        /// <summary>
        /// The open generic TypeDef this TypeSpec closes over (for example <c>Box`1</c> for
        /// <c>Box&lt;int&gt;</c>). Only set when <see cref="IsGenericInstance"/> is <see langword="true"/>.
        /// </summary>
        public Token GenericTypeDef { get; set; }

        /// <summary>
        /// The type arguments that close <see cref="GenericTypeDef"/>, in declaration order. Only set when
        /// <see cref="IsGenericInstance"/> is <see langword="true"/>.
        /// </summary>
        public List<TypeSpecArg> GenericArguments { get; set; }
    }

    /// <summary>
    /// One type argument of a closed generic instance (see <see cref="TypeSpec.GenericArguments"/>).
    /// Every reference here is a NanoCLR token, never a CLR one -- CLR tokens for TypeSpec entries are not
    /// reliably available (a generic instance used only as a field type or a method return type has no
    /// backing PE metadata row, so Mono.Cecil reports RID 0 for it), while the NanoCLR token is always the
    /// one this table itself assigns and is what the wire protocol (Debugging_Resolve_Type) already resolves.
    /// </summary>
    public partial class TypeSpecArg
    {
        public bool IsPrimitive { get; set; }

        /// <summary>
        /// Set only when <see cref="IsPrimitive"/> is <see langword="true"/>. The name of a
        /// <c>NanoCLRDataType</c> member (for example <c>DATATYPE_I4</c>).
        /// </summary>
        public string PrimitiveType { get; set; }

        /// <summary>
        /// Set only when <see cref="IsPrimitive"/> is <see langword="false"/>. The NanoCLR TypeDef token of
        /// a class declared in the same assembly as this TypeSpec, or the NanoCLR TypeSpec token of a nested
        /// generic instance (also always local to this assembly). <see langword="null"/> when the argument
        /// is an ordinary class declared in a different assembly -- there is no TypeRef entry in this model
        /// to address it by token, so <see cref="ClassName"/> is the only way to resolve it in that case.
        /// </summary>
        public Token TypeToken { get; set; }

        /// <summary>
        /// Set only for ordinary (non-generic-instance) class arguments: Cecil's own
        /// <c>TypeReference.FullName</c>, exactly as recorded in <see cref="Class.Name"/> for that class --
        /// never passed through the FixTypeNames helper. Used to resolve the class by name (across every
        /// loaded assembly) when <see cref="TypeToken"/> could not be resolved because the class is declared
        /// in a different assembly than this TypeSpec.
        /// </summary>
        public string ClassName { get; set; }
    }

    public partial class Member
    {
        public Token Token { get; set; }

        public string Name { get; set; }
    }

    public partial class Token
    {
        public string CLR { get; set; }
        public string NanoCLR { get; set; }
    }

    #region Converters

    public class VersionConverter : JsonConverter<Version>
    {
        public override Version Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
                Version.Parse(reader.GetString());

        public override void Write(
            Utf8JsonWriter writer,
            Version value,
            JsonSerializerOptions options) =>
                writer.WriteStringValue(value.ToString(4));
    }

    #endregion
}
