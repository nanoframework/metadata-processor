// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace TestNFApp
{
    public class GenericBase<T>
    {
        public T Value;
    }

    public class NonGenericBase
    {
    }

    public class DerivedFromNonGeneric : NonGenericBase
    {
    }

    public class DerivedFromClosedGeneric : GenericBase<int>
    {
    }

    public class DerivedFromOpenGeneric<U> : GenericBase<U>
    {
    }
}
