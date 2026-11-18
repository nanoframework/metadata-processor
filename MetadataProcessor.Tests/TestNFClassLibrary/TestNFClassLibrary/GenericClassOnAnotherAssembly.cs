// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace TestNFClassLibrary
{
    // generic base class of a type in another assembly: its definition is not part of the assembly that derives from it
    public class ExternalGenericBase<T>
    {
    }

    // generic types that only show up as arguments of the signatures of ExternalGenericSignatures
    public class ExternalReturnArgument<T>
    {
    }

    public class ExternalFieldArgument<T>
    {
    }

    public class ExternalGenericSignatures
    {
        public static IEnumerable<ExternalFieldArgument<int>> Field;

        public static IEnumerable<ExternalReturnArgument<int>> Items()
        {
            return null;
        }
    }
}