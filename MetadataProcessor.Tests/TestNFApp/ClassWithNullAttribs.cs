// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;

namespace TestNFApp
{
    public class ClassWithNullAttribs
    {
        [DoesNotReturn]
        public int MethodDecoratedWithDoesNotReturn()
        {
            throw new Exception();
        }

        public string MethoWIthParamNotNullAttrib([NotNull] string value)
        {
            return value;
        }
    }
}
