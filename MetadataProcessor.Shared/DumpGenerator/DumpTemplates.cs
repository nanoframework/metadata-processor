// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace nanoFramework.Tools.MetadataProcessor
{
    internal partial class DumpTemplates
    {
        internal const string DumpAllTemplate =
@"{{#each AssemblyReferences}}
AssemblyRef {{ReferenceId}}{{#newline}}
-------------------------------------------------------{{#newline}}
'{{Name}}'{{#newline}}
    Flags: {{Flags}}{{#newline}}
{{#newline}}
{{/each}}

{{#each TypeReferences}}
TypeRef {{ReferenceId}}{{#newline}}
-------------------------------------------------------{{#newline}}
Scope: {{Scope}}{{#newline}}
    '{{Name}}'{{#newline}}
{{#each MemberReferences}}
    MemberRef {{ReferenceId}}{{#newline}}
    -------------------------------------------------------{{#newline}}
        '{{Name}}'{{#newline}}
        [{{Signature}}]{{#newline}}
{{/each}}
{{#newline}}
{{/each}}

{{#each TypeDefinitions}}
TypeDef {{ReferenceId}}{{#newline}}
-------------------------------------------------------{{#newline}}
    '{{Name}}'{{#newline}}
    Flags: {{Flags}}{{#newline}}
    Extends: {{ExtendsType}}{{#newline}}
    Enclosed: {{EnclosedType}}{{#newline}}
{{#if GenericParameters}}
    Generic Parameters{{#newline}}
{{#each GenericParameters}}
        ({{Position}}) GenericParamToken {{GenericParamToken}} '{{Name}}' Owner: {{Owner}} [{{Signature}}]{{#newline}}
{{/each}}
{{/if}}

{{#each FieldDefinitions}}
    FieldDef {{ReferenceId}}{{#newline}}
    -------------------------------------------------------{{#newline}}
    Attr: {{Attributes}}{{#newline}}
    Flags: {{Flags}}{{#newline}}
    '{{Name}}'{{#newline}}
    [{{Signature}}]{{#newline}}
{{/each}}

{{#each MethodDefinitions}}
    MethodDef {{ReferenceId}}{{#newline}}
    -------------------------------------------------------{{#newline}}
        '{{Name}}'{{#newline}}
        Flags: {{Flags}}{{#newline}}
        Impl: {{Implementation}}{{#newline}}
        RVA: {{RVA}}{{#newline}}
        [{{Signature}}]{{#newline}}
{{#if Locals}}
        Locals {{Locals}}{{#newline}}
{{/if}}
{{#each ExceptionHandlers}}
        EH: {{Handler}}{{#newline}}
{{/each}}
{{#if ILCodeInstructionsCount}}
        IL count: {{ILCodeInstructionsCount}}{{#newline}}
{{/if}}
{{#each ILCode}}
            {{IL}}{{#newline}}
{{/each}}
{{/each}}

{{#each InterfaceDefinitions}}
    InterfaceImpl {{ReferenceId}} Itf: {{Interface}}{{#newline}}
    -------------------------------------------------------{{#newline}}
{{/each}}
{{#newline}}
{{/each}}

{{#each TypeSpecifications}}
TypeSpec {{ReferenceId}}{{#newline}}
-------------------------------------------------------{{#newline}}
    '{{Name}}'{{#newline}}
{{#each MemberReferences}}
    MemberRef {{ReferenceId}}{{#newline}}
    -------------------------------------------------------{{#newline}}
        '{{Name}}'{{#newline}}
        {{Signature}}{{#newline}}

{{#if Arguments}}
        Argument: {{Arguments}}{{#newline}}
{{/else}}
    No arguments
{{/if}}
{{/each}}
{{#newline}}
{{/each}}

Generic Parameters{{#newline}}
-------------------------------------------------------{{#newline}}
{{#each GenericParams}}
{{Position}} {{Name}} {{Owner}}{{#newline}}
{{/each}}
{{#newline}}

{{#each Attributes}}
Attribute: {{Name}}::[{{ReferenceId}} {{TypeToken}}]{{#newline}}
-------------------------------------------------------{{#newline}}
{{#if FixedArgs}}Fixed Arguments:{{#newline}}{{#else}}{{#newline}}{{/if}}
{{#each FixedArgs}}
{{Options}} {{Numeric}}{{Text}}{{#newline}}
{{/each}}
{{#newline}}
{{/each}}

String Heap{{#newline}}
-------------------------------------------------------{{#newline}}
{{#each StringHeap}}
{{ReferenceId}}: {{Content}}{{#newline}}
{{/each}}
{{#newline}}

User Strings{{#newline}}
-------------------------------------------------------{{#newline}}
{{#each UserStrings}}
{{ReferenceId}} : ({{Length}}) ""{{Content}}""{{#newline}}
{{/each}}
";
    }
}
