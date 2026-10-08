// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Original work from Oleg Rakhmatulin.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Native contract of an assembly: the set of native methods (with their canonical native slots)
    /// and the field layout constants (FIELD__ / FIELD_STATIC__) that native code is compiled against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The contract hash changes when something native code depends on changes: a native method is added,
    /// removed or has its signature changed, or the field layout (name, index or type) of any class/struct changes.
    /// Compiler-generated types (closures, iterators, async state machines, ...) are not part of it.
    /// Managed-only methods, method bodies and type usage order don't affect it. Note that FIELD_STATIC__
    /// indexes are assembly-wide, so a new static field (including compiler-generated lambda caches)
    /// shifts the indexes of the types that follow it, which is a real native break and changes the hash.
    /// </para>
    /// <para>
    /// Native slots are assigned by ordinal sort of (safe class name, safe method name), so they are
    /// independent of type and method ordering in the PE.
    /// </para>
    /// </remarks>
    public sealed class NativeContract
    {
        /// <summary>
        /// Version tag fed first into the contract hash. Bump when the hash composition changes.
        /// </summary>
        public const string ContractVersionTag = "nfNativeContract/3";

        // keyed by reference: method definitions are the same instances throughout the processing
        private readonly Dictionary<MethodDefinition, ushort> _slotByMethod = new Dictionary<MethodDefinition, ushort>();
        private readonly List<NativeMethodSlot> _methods = new List<NativeMethodSlot>();
        private readonly List<NativeTypeLayout> _types = new List<NativeTypeLayout>();
        private readonly List<string> _contractLog = new List<string>();

        private NativeContract()
        {
        }

        /// <summary>
        /// Native contract hash.
        /// Will return 0 if there are no native methods.
        /// </summary>
        public uint Hash { get; private set; }

        /// <summary>
        /// Native methods, in native slot order (index in the list is the slot).
        /// </summary>
        public IReadOnlyList<NativeMethodSlot> Methods => _methods;

        /// <summary>
        /// Types with field constants and/or native methods (all stub-included classes and structs, except
        /// compiler-generated ones), in type definition table order.
        /// Only computed when there are native methods (empty otherwise).
        /// </summary>
        public IReadOnlyList<NativeTypeLayout> Types => _types;

        /// <summary>
        /// <c>true</c> if there are no native methods, meaning there is no native contract.
        /// </summary>
        public bool IsEmpty => _methods.Count == 0;

        /// <summary>
        /// Gets the native slot assigned to a method.
        /// </summary>
        /// <param name="method">Method definition.</param>
        /// <param name="slot">Native slot, if the method is a native method.</param>
        /// <returns><c>true</c> if the method is a native method, <c>false</c> otherwise.</returns>
        public bool TryGetSlot(
            MethodDefinition method,
            out ushort slot)
        {
            slot = 0;

            return method != null
                && _slotByMethod.TryGetValue(method, out slot);
        }

        /// <summary>
        /// Returns the log entries collected while building the contract: one per native slot
        /// and one per type (with its FIELD constants).
        /// </summary>
        public IReadOnlyList<string> GetContractLog() => _contractLog;

        /// <summary>
        /// Builds the native contract for the assembly in <paramref name="context"/>.
        /// </summary>
        /// <param name="context">Tables context. Requires type definition and fields tables.</param>
        /// <exception cref="ArgumentException">On duplicate native method or type names, or if a base type can't be resolved.</exception>
        public static NativeContract Build(nanoTablesContext context)
        {
            var contract = new NativeContract();

            string assemblyName = context.AssemblyDefinition.Name.Name;

            ///////////////////////////
            // native methods & slots

            var nativeMethods = new List<NativeMethodSlot>();

            foreach (TypeDefinition c in context.TypeDefinitionTable.Items)
            {
                if (!IsStubIncludedType(c))
                {
                    continue;
                }

                foreach (MethodDefinition m in nanoTablesContext.GetOrderedMethods(c.Methods))
                {
                    if (IsNativeMethod(m))
                    {
                        nativeMethods.Add(new NativeMethodSlot(
                            m,
                            GetSafeClassName(c),
                            GetSafeMethodName(m)));
                    }
                }
            }

            nativeMethods = nativeMethods
                .OrderBy(m => m.SafeClassName, StringComparer.Ordinal)
                .ThenBy(m => m.SafeMethodName, StringComparer.Ordinal)
                .ToList();

            // a slot is stored in the 16 bits RVA field, 0xFFFF is reserved for "no body"
            if (nativeMethods.Count >= 0xFFFF)
            {
                throw new ArgumentException($"Assembly '{assemblyName}' has {nativeMethods.Count} native methods, exceeding the maximum supported ({0xFFFF - 1}).");
            }

            for (int i = 0; i < nativeMethods.Count; i++)
            {
                NativeMethodSlot item = nativeMethods[i];

                if (i > 0
                    && nativeMethods[i - 1].SafeClassName == item.SafeClassName
                    && nativeMethods[i - 1].SafeMethodName == item.SafeMethodName)
                {
                    throw new ArgumentException($"Duplicate native method name 'Library_..._{item.SafeClassName}::{item.SafeMethodName}' in assembly '{assemblyName}' ('{nativeMethods[i - 1].Method.FullName}' and '{item.Method.FullName}'). Native methods must have distinct native signatures.");
                }

                item.Slot = (ushort)i;

                contract._methods.Add(item);
                contract._slotByMethod.Add(item.Method, item.Slot);
                contract._contractLog.Add($"  [{item.Slot,4}] {item.SafeClassName}::{item.SafeMethodName}");
            }

            // no native methods: there is no native contract (no stubs are generated)
            if (contract.IsEmpty)
            {
                contract.Hash = 0;

                return contract;
            }

            ///////////////////////////
            // types and field constants
            // This has to follow exactly what the header generator used to compute so the indexes match
            // what native code expects (static field index is assembly-wide).

            int staticFieldCount = 0;

            foreach (TypeDefinition c in context.TypeDefinitionTable.Items)
            {
                if (!IsStubIncludedType(c))
                {
                    continue;
                }

                // static fields take assembly-wide slots, even in the types skipped below
                List<FieldDefinition> staticFields = c.Fields.Where(f => f.IsStatic && !f.IsLiteral).ToList();

                // Developer notes:
                // - Exclude Roslyn-generated compiler helper type <PrivateImplementationDetails>
                // which holds embedded static data (hash-named fields) and has no nanoFramework relevance.
                // - Need to check the original type name because the sanitized name has
                // angle-bracket content stripped, leaving an empty string that never matches.
                if (c.Name.StartsWith("<PrivateImplementationDetails>"))
                {
                    staticFieldCount += staticFields.Count;

                    continue;
                }

                string safeClassName = GetSafeClassName(c);

                // Guard against any other compiler-generated type whose sanitized name is empty
                if (string.IsNullOrWhiteSpace(safeClassName))
                {
                    staticFieldCount += staticFields.Count;

                    continue;
                }

                if (IsCompilerGenerated(c))
                {
                    // Developer notes:
                    // - Compiler-generated types (closures, iterators, state machines, lambda caches) are
                    // not accessed by native code: no field constants and not part of the contract.
                    // - Their static fields still take static field slots, so they have to be accounted for.
                    staticFieldCount += staticFields.Count;

                    continue;
                }

                var layout = new NativeTypeLayout(c, safeClassName, c.Methods.Any(m => IsNativeMethod(m)));

                // static fields
                int fieldCount = 0;

                foreach (FieldDefinition f in staticFields)
                {
                    FixFieldName(f, out string fixedFieldName, out string fieldWarning);

                    layout.StaticFields.Add(new NativeFieldConstant(
                        f,
                        string.IsNullOrEmpty(fixedFieldName) ? f.Name : fixedFieldName,
                        staticFieldCount + fieldCount++,
                        fieldWarning));
                }

                // update static field counter
                staticFieldCount += staticFields.Count;

                int inheritedInstanceFields = GetInstanceFieldsOffset(c);

                // 0 based index, need to add 1
                int instanceFieldId = inheritedInstanceFields + 1;

                // instance fields
                foreach (FieldDefinition f in c.Fields.Where(f => !f.IsStatic && !f.IsLiteral))
                {
                    FixFieldName(f, out string fixedFieldName, out string fieldWarning);

                    if (context.FieldsTable.TryGetFieldDefinitionId(f, false, out _))
                    {
                        layout.InstanceFields.Add(new NativeFieldConstant(
                            f,
                            string.IsNullOrEmpty(fixedFieldName) ? f.Name : fixedFieldName,
                            instanceFieldId++,
                            fieldWarning));
                    }
                }

                CheckDuplicateFieldNames(c, layout.StaticFields, "FIELD_STATIC__");
                CheckDuplicateFieldNames(c, layout.InstanceFields, "FIELD__");

                contract._types.Add(layout);
            }

            // sanity check for duplicate type names (would produce duplicate struct names in the header)
            foreach (IGrouping<string, NativeTypeLayout> duplicate in contract._types
                .GroupBy(t => t.SafeClassName, StringComparer.Ordinal)
                .Where(g => g.Count() > 1))
            {
                throw new ArgumentException($"Duplicate native type name '{duplicate.Key}' in assembly '{assemblyName}' ({string.Join(", ", duplicate.Select(t => $"'{t.Type.FullName}'"))}).");
            }

            ///////////////////////////
            // contract hash

            uint crc = 0;

            crc = Feed(crc, ContractVersionTag);
            crc = Feed(crc, assemblyName);

            crc = Feed(crc, $"methods:{contract._methods.Count}");

            foreach (NativeMethodSlot item in contract._methods)
            {
                crc = Feed(crc, item.SafeClassName);
                crc = Feed(crc, item.SafeMethodName);
            }

            // only types with field constants are part of the field layout
            List<NativeTypeLayout> sortedTypes = contract._types
                .Where(t => t.StaticFields.Count > 0 || t.InstanceFields.Count > 0)
                .OrderBy(t => t.SafeClassName, StringComparer.Ordinal)
                .ToList();

            crc = Feed(crc, $"types:{sortedTypes.Count}");

            foreach (NativeTypeLayout type in sortedTypes)
            {
                crc = Feed(crc, type.SafeClassName);

                List<string> fieldEntries = type.StaticFields.Select(f => $"FIELD_STATIC__{f.Name}={f.Index}:{f.TypeName}")
                    .Concat(type.InstanceFields.Select(f => $"FIELD__{f.Name}={f.Index}:{f.TypeName}"))
                    .ToList();

                foreach (string entry in fieldEntries)
                {
                    crc = Feed(crc, entry);
                }

                contract._contractLog.Add($"  [type] {type.SafeClassName} {{ {string.Join(", ", fieldEntries)} }}");
            }

            contract.Hash = crc;

            return contract;
        }

        /// <summary>
        /// Feeds a string into the CRC, followed by a NULL separator so that consecutive
        /// strings can't be ambiguously concatenated.
        /// </summary>
        private static uint Feed(
            uint crc,
            string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value + "\0");

            return Crc32.Compute(bytes, crc);
        }

        /// <summary>
        /// Checks if a method is a native method (no body and not abstract).
        /// Only meaningful for methods declared in stub-included types (see <see cref="IsStubIncludedType"/>).
        /// </summary>
        internal static bool IsNativeMethod(MethodDefinition method)
        {
            return !method.HasBody && !method.IsAbstract;
        }

        /// <summary>
        /// Checks if a type is to be considered for stub generation: classes and value types,
        /// not delegates, not excluded and not an ignored attribute.
        /// </summary>
        internal static bool IsStubIncludedType(TypeDefinition type)
        {
            return type.IncludeInStub()
                && !type.IsToExclude()
                && !nanoTablesContext.IgnoringAttributes.Contains(type.FullName);
        }

        /// <summary>
        /// Checks if a type is compiler-generated, or nested in such a type (closures/display classes,
        /// iterator and async state machines, lambda caches, anonymous types...).
        /// </summary>
        /// <remarks>
        /// Detection is by attribute only. Roslyn marks every type it synthesizes with CompilerGeneratedAttribute,
        /// except the inline array types used by collection expressions, which carry InlineArrayAttribute instead.
        /// Both attributes are defined in the core library.
        /// </remarks>
        internal static bool IsCompilerGenerated(TypeDefinition type)
        {
            for (TypeDefinition current = type; current != null; current = current.DeclaringType)
            {
                if (current.HasCustomAttributes
                    && current.CustomAttributes.Any(a => s_compilerGeneratedMarkers.Contains(a?.AttributeType?.FullName)))
                {
                    return true;
                }
            }

            return false;
        }

        private static readonly HashSet<string> s_compilerGeneratedMarkers = new HashSet<string>(StringComparer.Ordinal)
        {
            "System.Runtime.CompilerServices.CompilerGeneratedAttribute",
            "System.Runtime.CompilerServices.InlineArrayAttribute",
        };

        private static TypeDefinition ResolveBaseType(TypeDefinition type)
        {
            TypeDefinition baseType = type.BaseType.Resolve();

            if (baseType == null)
            {
                throw new ArgumentException($"Can't resolve base type '{type.BaseType.FullName}' of '{type.FullName}'. Required to compute the field layout for the native contract.");
            }

            return baseType;
        }

        private static int GetInstanceFieldsOffset(TypeDefinition c)
        {
            // check if this type has a base type different from System.Object
            if (c.BaseType != null &&
                c.BaseType.FullName != "System.Object")
            {
                // get base parent type fields count
                return GetNestedFieldsCount(ResolveBaseType(c));
            }
            else
            {
                return 0;
            }
        }

        private static int GetNestedFieldsCount(TypeDefinition c)
        {
            int fieldCount = 0;

            if (c.BaseType != null &&
                c.BaseType.FullName != "System.Object")
            {
                // get parent type fields count
                fieldCount = GetNestedFieldsCount(ResolveBaseType(c));

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

        /// <summary>
        /// Fix field name to a valid C++ identifier (to be used in FIELD__ / FIELD_STATIC__ constants).
        /// </summary>
        /// <param name="field">The field definition to work on.</param>
        /// <param name="fixedFieldName">The fixed field name, or an <see cref="string.Empty"/> string if no fix is needed.</param>
        /// <param name="fieldWarning">The comment to be added to the field declaration, or empty if the field wasn't renamed.</param>
        internal static void FixFieldName(
            FieldDefinition field,
            out string fixedFieldName,
            out string fieldWarning)
        {
            fixedFieldName = SanitizeFieldName(field.Name, out string renameKind);

            if (renameKind == null)
            {
                fixedFieldName = string.Empty;
                fieldWarning = string.Empty;
            }
            else
            {
                fieldWarning = $"// renamed {renameKind} '{field.Name}'";
            }
        }

        /// <summary>
        /// Converts a metadata field name into a valid C++ identifier.
        /// </summary>
        /// <param name="name">Field name as in metadata.</param>
        /// <param name="renameKind">Description of the rename applied, or <c>null</c> if the name is used as is.</param>
        /// <returns>The identifier to use.</returns>
        /// <remarks>
        /// Rules (see Utility/NativeContract.cs "Field names"):
        /// <list type="number">
        /// <item>Valid identifiers (letters, digits, '_') are used as is.</item>
        /// <item>Auto-property backing field <c>&lt;X&gt;k__BackingField</c>: <c>X</c>. For explicit interface
        /// implementations (<c>Ns.IFoo.Bar</c>) the namespace is dropped, keeping interface simple name and member: <c>IFoo_Bar</c>.</item>
        /// <item>Primary constructor captured parameter <c>&lt;x&gt;P</c>: <c>x</c>.</item>
        /// <item>Anything else with invalid characters: sanitized with the same scheme.</item>
        /// </list>
        /// Generic arguments: <c>Name&lt;A,B&gt;</c> becomes <c>Name_of_A_B</c>, arguments use simple names (no namespace),
        /// nested generics recursively (<c>IGen&lt;List&lt;Int32&gt;&gt;</c> becomes <c>IGen_of_List_of_Int32</c>).
        /// Arity notation <c>IGen`1</c> becomes <c>IGen_1</c>. Dotted segments are joined with '_'.
        /// Any other character not valid in an identifier becomes '_', without doubling or trailing underscores.
        /// </remarks>
        /// <exception cref="ArgumentException">If a valid identifier can't be produced.</exception>
        internal static string SanitizeFieldName(string name, out string renameKind)
        {
            renameKind = null;

            if (IsValidIdentifier(name))
            {
                return name;
            }

            string result;

            Match backingField = Regex.Match(name, @"^<(.+)>k__BackingField$");
            Match primaryConstructorParameter = Regex.Match(name, @"^<(.+)>P$");

            if (backingField.Success)
            {
                renameKind = "backing field";

                // explicit interface implementation: keep only interface simple name + member
                List<string> segments = SplitTopLevel(backingField.Groups[1].Value, '.');

                result = MangleSegments(segments.Count > 2 ? segments.Skip(segments.Count - 2).ToList() : segments);
            }
            else if (primaryConstructorParameter.Success)
            {
                renameKind = "primary constructor parameter field";

                result = MangleSegments(SplitTopLevel(primaryConstructorParameter.Groups[1].Value, '.'));
            }
            else
            {
                renameKind = "field";

                result = MangleSegments(SplitTopLevel(name, '.'));
            }

            if (!IsValidIdentifier(result))
            {
                throw new ArgumentException($"Can't convert field name '{name}' into a valid C++ identifier (got '{result}').");
            }

            return result;
        }

        private static bool IsValidIdentifier(string name)
        {
            return !string.IsNullOrEmpty(name)
                && name.All(c => c == '_' || char.IsLetterOrDigit(c));
        }

        /// <summary>
        /// Mangles dotted segments, each possibly with generic arguments, and joins them with '_'.
        /// </summary>
        private static string MangleSegments(IEnumerable<string> segments)
        {
            return JoinParts(segments.Select(MangleSegment));
        }

        /// <summary>
        /// Mangles a single segment: <c>Name&lt;A,B&gt;</c> becomes <c>Name_of_A_B</c>, where each argument
        /// uses its simple name (last dotted segment) mangled recursively.
        /// </summary>
        private static string MangleSegment(string segment)
        {
            int open = segment.IndexOf('<');

            // a leading '<' isn't a generic argument list (e.g. compiler-generated names): just sanitize
            if (open > 0 && segment.EndsWith(">"))
            {
                string baseName = SanitizeChars(segment.Substring(0, open));
                string argumentList = segment.Substring(open + 1, segment.Length - open - 2);

                IEnumerable<string> arguments = SplitTopLevel(argumentList, ',')
                    .Select(a => SplitTopLevel(a.Trim(), '.').Last())
                    .Select(MangleSegment);

                return JoinParts(new[] { baseName, "of" }.Concat(arguments));
            }

            return SanitizeChars(segment);
        }

        /// <summary>
        /// Replaces characters not valid in an identifier with '_' ('`' arity notation included),
        /// without producing doubled, leading or trailing replacement underscores.
        /// </summary>
        private static string SanitizeChars(string value)
        {
            var builder = new StringBuilder();
            bool lastWasReplacement = false;

            foreach (char c in value)
            {
                if (c == '_' || char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                    lastWasReplacement = false;
                }
                else if (!lastWasReplacement && builder.Length > 0)
                {
                    builder.Append('_');
                    lastWasReplacement = true;
                }
            }

            if (lastWasReplacement)
            {
                builder.Length--;
            }

            return builder.ToString();
        }

        private static string JoinParts(IEnumerable<string> parts)
        {
            return string.Join("_", parts.Where(p => !string.IsNullOrEmpty(p)));
        }

        /// <summary>
        /// Splits a string by <paramref name="separator"/>, ignoring separators inside '&lt;' '&gt;' (generic arguments).
        /// </summary>
        private static List<string> SplitTopLevel(
            string value,
            char separator)
        {
            var parts = new List<string>();
            int depth = 0;
            int start = 0;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];

                if (c == '<')
                {
                    depth++;
                }
                else if (c == '>' && depth > 0)
                {
                    depth--;
                }
                else if (c == separator && depth == 0)
                {
                    parts.Add(value.Substring(start, i - start));
                    start = i + 1;
                }
            }

            parts.Add(value.Substring(start));

            return parts;
        }

        /// <summary>
        /// Checks that the field constants of a type are unique, throwing an error naming the type and the clashing fields.
        /// </summary>
        private static void CheckDuplicateFieldNames(
            TypeDefinition type,
            IEnumerable<NativeFieldConstant> fields,
            string constantPrefix)
        {
            var byName = new Dictionary<string, NativeFieldConstant>(StringComparer.Ordinal);

            foreach (NativeFieldConstant f in fields)
            {
                if (byName.TryGetValue(f.Name, out NativeFieldConstant existing))
                {
                    throw new ArgumentException($"Fields '{existing.Field.Name}' and '{f.Field.Name}' of type '{type.FullName}' both map to the native constant '{constantPrefix}{f.Name}'. Rename one of them.");
                }

                byName.Add(f.Name, f);
            }
        }

        internal static string GetSafeClassName(TypeDefinition type)
        {
            string className = (type != null
                ? string.Join("_", GetSafeClassName(type.DeclaringType), type.Namespace, type.Name)
                    .Replace(".", "_")
                    .TrimStart('_')
                : string.Empty);

            return CleanupGenericName(className);
        }

        internal static string GetSafeMethodName(MethodDefinition method)
        {
            string name = string.Concat(method.Name, (method.IsStatic ? "___STATIC__" : "___"),
                string.Join("__", GetAllParameters(method)));

            string originalName = name.Replace(".", "_")
                                .Replace("/", "");

            return CleanupGenericName(originalName);
        }

        private static IEnumerable<string> GetAllParameters(
            MethodDefinition method)
        {
            yield return GetParameterType(method.ReturnType);

            if (method.HasParameters)
            {
                foreach (ParameterDefinition item in method.Parameters)
                {
                    yield return GetParameterType(item.ParameterType);
                }
            }
        }

        /// <summary>
        /// Gets the type name of a field, using the same mangling as native method signatures.
        /// </summary>
        internal static string GetFieldTypeName(FieldDefinition field)
        {
            return GetParameterType(field.FieldType);
        }

        private static string GetParameterType(
            TypeReference parameterType)
        {
            string typeName = "";
            bool continueProcessing = true;

            // special processing for arrays
            if (parameterType.IsArray)
            {
                typeName += NanoCLRDataType.DATATYPE_SZARRAY + "_" + GetParameterType(parameterType.GetElementType());
                continueProcessing = false;
            }
            else if (parameterType.IsByReference)
            {
                TypeReference elementType = ((TypeSpecification)parameterType).ElementType;

                typeName += NanoCLRDataType.DATATYPE_BYREF + "_";

                if (elementType.IsArray)
                {
                    typeName += NanoCLRDataType.DATATYPE_SZARRAY + "_" + GetParameterType(((TypeSpecification)elementType).ElementType);
                }
                else
                {
                    typeName += GetNanoCLRTypeName(elementType);
                }
                continueProcessing = false;
            }
            else if (!parameterType.IsPrimitive)
            {
                // TBD
                continueProcessing = true;
            }

            if (continueProcessing)
            {
                typeName = GetNanoCLRTypeName(parameterType);
            }

            // clear 'DATATYPE_' prefixes
            // and make it upper case
            return typeName.Replace("DATATYPE_", "");
        }

        internal static string GetNanoCLRTypeName(TypeReference parameterType)
        {
            // try getting primitive type

            NanoCLRDataType myType;
            if (nanoSignaturesTable.PrimitiveTypes.TryGetValue(parameterType.FullName, out myType))
            {
                if (myType == NanoCLRDataType.DATATYPE_LAST_PRIMITIVE)
                {
                    return "DATATYPE_STRING";
                }
                else if (myType == NanoCLRDataType.DATATYPE_LAST_NONPOINTER)
                {
                    return "DATATYPE_TIMESPAN";
                }
                else if (myType == NanoCLRDataType.DATATYPE_LAST_PRIMITIVE_TO_MARSHAL)
                {
                    return "DATATYPE_TIMESPAN";
                }
                else if (myType == NanoCLRDataType.DATATYPE_LAST_PRIMITIVE_TO_PRESERVE)
                {
                    return "DATATYPE_R8";
                }
                else
                {
                    return myType.ToString();
                }
            }
            else
            {
                // type is not primitive

                if (parameterType.IsGenericParameter)
                {
                    // check if it's generic
                    return "DATATYPE_GENERICTYPE";
                }
                else if (parameterType.IsPointer)
                {
                    if (nanoSignaturesTable.PrimitiveTypes.TryGetValue(parameterType.GetElementType().FullName, out myType))
                    {
                        return $"{myType}ptr";
                    }
                }

                // last attempt: get full qualified type name
                string typeName = parameterType.FullName.Replace(".", string.Empty);

                return CleanupGenericName(typeName);
            }
        }

        internal static string CleanupGenericName(string name)
        {
            // Replace the CLR backtick-N generic arity notation with an underscore
            // (e.g. Dictionary`2 → Dictionary_2).
            string fixedName = name
                    .Replace('`', '_');

            return Regex.Replace(fixedName, @"<[^>]*>", string.Empty);
        }
    }

    /// <summary>
    /// A native method and its native slot.
    /// </summary>
    public sealed class NativeMethodSlot
    {
        internal NativeMethodSlot(
            MethodDefinition method,
            string safeClassName,
            string safeMethodName)
        {
            Method = method;
            SafeClassName = safeClassName;
            SafeMethodName = safeMethodName;
        }

        /// <summary>
        /// Native slot (index in the native method lookup table).
        /// </summary>
        public ushort Slot { get; internal set; }

        public MethodDefinition Method { get; }

        public string SafeClassName { get; }

        public string SafeMethodName { get; }
    }

    /// <summary>
    /// A type with field constants and/or native methods, as declared in the native header.
    /// </summary>
    public sealed class NativeTypeLayout
    {
        internal NativeTypeLayout(
            TypeDefinition type,
            string safeClassName,
            bool hasNativeMethods)
        {
            Type = type;
            SafeClassName = safeClassName;
            HasNativeMethods = hasNativeMethods;
        }

        public TypeDefinition Type { get; }

        public string SafeClassName { get; }

        public bool HasNativeMethods { get; }

        /// <summary>
        /// FIELD_STATIC__ constants, in emitted order.
        /// </summary>
        public List<NativeFieldConstant> StaticFields { get; } = new List<NativeFieldConstant>();

        /// <summary>
        /// FIELD__ constants, in emitted order.
        /// </summary>
        public List<NativeFieldConstant> InstanceFields { get; } = new List<NativeFieldConstant>();
    }

    /// <summary>
    /// A FIELD constant emitted in the native header.
    /// </summary>
    public sealed class NativeFieldConstant
    {
        internal NativeFieldConstant(
            FieldDefinition field,
            string name,
            int index,
            string warning)
        {
            Field = field;
            Name = name;
            Index = index;
            Warning = warning;
            TypeName = NativeContract.GetFieldTypeName(field);
        }

        public FieldDefinition Field { get; }

        public string Name { get; }

        public int Index { get; }

        public string Warning { get; }

        /// <summary>
        /// Field type name, mangled as in native method signatures.
        /// </summary>
        public string TypeName { get; }
    }
}
