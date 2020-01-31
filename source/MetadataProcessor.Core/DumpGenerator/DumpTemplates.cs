//
// Copyright (c) 2020 The nanoFramework project contributors
// See LICENSE file in the project root for full license information.
//

namespace nanoFramework.Tools.MetadataProcessor
{
    internal partial class DumpTemplates
    {
        internal static string DumpAllTemplate =
@"
{{#AssemblyReferences}}
AssemblyRefProps [{{ReferenceId}}]: Flags: {{Flags}} '{{Name}}'
{{/AssemblyReferences}}
{{#TypeReferences}}
TypeRefProps [{{ReferenceId}}]: Scope: {{Scope}} '{{Name}}'
{{#MemberReferences}}
    MemberRefProps [{{ReferenceId}}]: '{{Name}}' [{{Signature}}]
{{/MemberReferences}}
{{/TypeReferences}}
{{#TypeDefinitions}}
TypeDefProps [{{ReferenceId}}]: Flags: {{Flags}} Extends: {{ExtendsType}} Enclosed: {{EnclosedType}} '{{Name}}'
{{#FieldDefinitions}}
    FieldDefProps [{{ReferenceId}}]: Attr: {{Attributes}} Flags: {{Flags}} '{{Name}}' [{{Signature}}]
{{/FieldDefinitions}}
{{#MethodDefinitions}}
    MethodDefProps [{{ReferenceId}}]: Flags: {{Flags}} Impl: {{Implementation}} RVA: {{RVA}}  '{{Name}}' [{{Signature}}]
        {{#Locals}}
        Locals {{Locals}}
        {{/Locals}}
        {{#ExceptionHandlers}}
        EH: {{ExceptionHandler}}
        {{/ExceptionHandlers}}
        {{#ILCodeInstructionsCount}}
        IL count: {{ILCodeInstructionsCount}}
        {{/ILCodeInstructionsCount}}
           {{#ILCode}}
           {{IL}}
           {{/ILCode}}
{{/MethodDefinitions}}
{{#InterfaceDefinitions}}
    InterfaceImplProps [{{ReferenceId}}]: Itf: {{Interface}}
{{/InterfaceDefinitions}}

{{/TypeDefinitions}}
{{#UserStrings}}
UserString [{{ReferenceId}}]: '{{Content}}'
{{/UserStrings}}
";

    }
}
