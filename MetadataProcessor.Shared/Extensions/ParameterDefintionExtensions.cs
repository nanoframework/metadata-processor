// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Mono.Cecil;

namespace nanoFramework.Tools.MetadataProcessor.Core.Extensions
{
    internal static class ParameterDefintionExtensions
    {
        public static string TypeToString(this ParameterDefinition parameter)
        {
            if (parameter.ParameterType is ByReferenceType byReference)
            {
                // pointer to native type
                return " *" + byReference.TypeSignatureAsString();
            }
            else if (parameter.ParameterType is ArrayType arrayType)
            {
                return "CLR_RT_TypedArray_" + arrayType.ElementType.TypeSignatureAsString();
            }
            else if (parameter.ParameterType.IsValueType)
            {
                return parameter.ParameterType.Resolve().TypeSignatureAsString();
            }
            else
            {
                return parameter.ParameterType.TypeSignatureAsString();
            }
        }
    }
}
