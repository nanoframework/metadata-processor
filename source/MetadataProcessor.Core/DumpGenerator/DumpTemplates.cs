//
// Copyright (c) 2020 The nanoFramework project contributors
// See LICENSE file in the project root for full license information.
//

namespace nanoFramework.Tools.MetadataProcessor
{
    internal partial class DumpTemplates
    {
        internal const string DumpAllTemplate =
@"{{#each AssemblyReferences}}
AssemblyRefProps [{{ReferenceId}}]: Flags: {{Flags}} '{{Name}}'{{#newline}}
{{/each}}
{{#if AssemblyReferences}}{{#newline}}{{/if}}

{{#each TypeReferences}}
TypeRefProps [{{ReferenceId}}]: Scope: {{Scope}} '{{Name}}'{{#newline}}
{{#each MemberReferences}}
    MemberRefProps [{{ReferenceId}}]: '{{Name}}' [{{Signature}}]{{#newline}}
{{/each}}
{{/each}}
{{#if TypeReferences}}{{#newline}}{{/if}}

{{#each TypeDefinitions}}
TypeDefProps [{{ReferenceId}}]: Flags: {{Flags}} Extends: {{ExtendsType}} Enclosed: {{EnclosedType}} '{{Name}}'{{#newline}}
{{#each FieldDefinitions}}
    FieldDefProps [{{ReferenceId}}]: Attr: {{Attributes}} Flags: {{Flags}} '{{Name}}' [{{Signature}}]{{#newline}}
{{/each}}

{{#each MethodDefinitions}}
    MethodDefProps [{{ReferenceId}}]: Flags: {{Flags}} Impl: {{Implementation}} RVA: {{RVA}} '{{Name}}' [{{Signature}}]{{#newline}}
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
    InterfaceImplProps [{{ReferenceId}}]: Itf: {{Interface}}{{#newline}}
{{/each}}
{{#if InterfaceDefinitions}}{{#newline}}{{/if}}
{{/each}}
{{#if TypeDefinitions}}{{#newline}}{{/if}}

{{#each Attributes}}
Attribute: {{Name}}::[{{ReferenceId}} {{TypeToken}}]{{#newline}}
{{/each}}
{{#if Attributes}}{{#newline}}{{/if}}

{{#each UserStrings}}
UserString [{{ReferenceId}}]: '{{Content}}'{{#newline}}
{{/each}}
";
    }
}
