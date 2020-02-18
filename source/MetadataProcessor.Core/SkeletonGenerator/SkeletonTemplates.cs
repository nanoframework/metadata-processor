//
// Copyright (c) 2019 The nanoFramework project contributors
// See LICENSE file in the project root for full license information.
//

namespace nanoFramework.Tools.MetadataProcessor
{
    internal partial class SkeletonTemplates
    {
        internal const string AssemblyHeaderTemplate =
@"//-----------------------------------------------------------------------------{{#newline}}
//{{#newline}}
//                   ** WARNING! ** {{#newline}}
//    This file was generated automatically by a tool.{{#newline}}
//    Re-running the tool will overwrite this file.{{#newline}}
//    You should copy this file to a custom location{{#newline}}
//    before adding any customization in the copy to{{#newline}}
//    prevent loss of your changes when the tool is{{#newline}}
//    re-run.{{#newline}}
//{{#newline}}
//-----------------------------------------------------------------------------{{#newline}}
{{#newline}}

#ifndef _{{ShortNameUpper}}_H_{{#newline}}
#define _{{ShortNameUpper}}_H_{{#newline}}
{{#newline}}

#include <nanoCLR_Interop.h>{{#newline}}
#include <nanoCLR_Runtime.h>{{#newline}}
#include <corlib_native.h>{{#newline}}
{{#newline}}

{{#each Classes}}
struct Library_{{AssemblyName}}_{{Name}}{{#newline}}
{{{#newline}}

{{#each StaticFields}}
    static const int FIELD_STATIC__{{Name}} = {{ReferenceIndex}};{{#newline}}
{{/each}}
{{#if StaticFields}}{{#newline}}{{/if}}

{{#each InstanceFields}}
{{#if FieldWarning}}{{FieldWarning}}{{/if}}
    static const int FIELD__{{Name}} = {{ReferenceIndex}};{{#newline}}
{{/each}}
{{#if InstanceFields}}{{#newline}}{{/if}}

{{#each Methods}}
    NANOCLR_NATIVE_DECLARE({{Declaration}});{{#newline}}
{{/each}}
{{#if Methods}}{{#newline}}{{/if}}

    //--//{{#newline}}
{{#newline}}
};{{#newline}}
{{#newline}}
{{/each}}
extern const CLR_RT_NativeAssemblyData g_CLR_AssemblyNative_{{Name}};{{#newline}}
{{#newline}}
#endif  //_{{ShortNameUpper}}_H_{{#newline}}
";

        internal const string AssemblyLookupTemplate =
@"#include ""{{HeaderFileName}}.h""{{#newline}}
{{#newline}}

static const CLR_RT_MethodHandler method_lookup[] ={{#newline}}
{{{#newline}}
{{#each LookupTable}}
    {{Declaration}},{{#newline}}
{{/each}}
};{{#newline}}
{{#newline}}

const CLR_RT_NativeAssemblyData g_CLR_AssemblyNative_{{AssemblyName}} ={{#newline}}
{{{#newline}}
    ""{{Name}}"",{{#newline}}
    {{NativeCRC32}},{{#newline}}
    method_lookup,{{#newline}}
    ////////////////////////////////////////////////////////////////////////////////////{{#newline}}
    // check if the version bellow matches the one in AssemblyNativeVersion attribute //{{#newline}}
    ////////////////////////////////////////////////////////////////////////////////////{{#newline}}
    { {{NativeVersion.Major}}, {{NativeVersion.Minor}}, {{NativeVersion.Build}}, {{NativeVersion.Revision}} }{{#newline}}
};{{#newline}}
";

        internal const string ClassStubTemplate =
@"//-----------------------------------------------------------------------------
//
//                   ** WARNING! ** 
//    This file was generated automatically by a tool.
//    Re-running the tool will overwrite this file.
//    You should copy this file to a custom location
//    before adding any customization in the copy to
//    prevent loss of your changes when the tool is
//    re-run.
//
//-----------------------------------------------------------------------------

#include ""{{HeaderFileName}}.h""

{{#each Functions}}
HRESULT {{Declaration}}( CLR_RT_StackFrame& stack )
{
    NANOCLR_HEADER();

    NANOCLR_SET_AND_LEAVE(stack.NotImplementedStub());

    NANOCLR_NOCLEANUP();
}
{{/each}}";
    }
}
