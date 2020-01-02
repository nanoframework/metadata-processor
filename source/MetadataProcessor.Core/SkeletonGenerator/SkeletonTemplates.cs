//
// Copyright (c) 2019 The nanoFramework project contributors
// See LICENSE file in the project root for full license information.
//

namespace nanoFramework.Tools.MetadataProcessor
{
    internal partial class SkeletonTemplates
    {
        internal static string AssemblyHeaderTemplate = 
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

#ifndef _{{ShortNameUpper}}_H_
#define _{{ShortNameUpper}}_H_

#include <nanoCLR_Interop.h>
#include <nanoCLR_Runtime.h>
#include <corlib_native.h>

{{#Classes}}
struct Library_{{AssemblyName}}_{{Name}}
{
	{{#StaticFields}}
    static const int FIELD_STATIC__{{Name}} = {{ReferenceIndex}};
	{{/StaticFields}}

    {{#InstanceFields}}
		{{#FieldWarning}}
		{{FieldWarning}}
		{{/FieldWarning}}
    static const int FIELD__{{Name}} = {{ReferenceIndex}};
	{{/InstanceFields}}

	{{#Methods}}
    NANOCLR_NATIVE_DECLARE({{Declaration}});
	{{/Methods}}

    //--//

};

{{/Classes}}

extern const CLR_RT_NativeAssemblyData g_CLR_AssemblyNative_{{Name}};

#endif  //_{{ShortNameUpper}}_H_
";

        internal static string AssemblyLookupTemplate =
@"#include ""{{HeaderFileName}}.h\""

static const CLR_RT_MethodHandler method_lookup[] =
{
{{#LookupTable}}
    {{Declaration}},
{{/LookupTable}}
};

const CLR_RT_NativeAssemblyData g_CLR_AssemblyNative_{{AssemblyName}} =
{
    ""{{Name}}"",
    {{NativeCRC32}},
    method_lookup,
    ////////////////////////////////////////////////////////////////////////////////////
    // check if the version bellow matches the one in AssemblyNativeVersion attribute //
    ////////////////////////////////////////////////////////////////////////////////////
    { {{NativeVersion.Major}}, {{NativeVersion.Minor}}, {{NativeVersion.Build}}, {{NativeVersion.Revision}} }
};
";

        internal static string ClassStubTemplate =
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

{{#Functions}}
HRESULT {{Declaration}}( CLR_RT_StackFrame& stack )
{
    NANOCLR_HEADER();

    NANOCLR_SET_AND_LEAVE(stack.NotImplementedStub());

    NANOCLR_NOCLEANUP();
}

{{/Functions}}
";
    }
}
