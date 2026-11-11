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

    // System.Version is used nowhere else in this assembly: its type reference only exists for this base type
    public class DerivedFromGenericOfExternalType : GenericBase<System.Version>
    {
    }

    // the generic definition of the base is in another assembly
    public class DerivedFromExternalGeneric : TestNFClassLibrary.ExternalGenericBase<System.Version>
    {
    }
}
