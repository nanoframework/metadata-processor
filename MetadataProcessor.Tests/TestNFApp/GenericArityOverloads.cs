// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace TestNFApp
{
    public class GenericArityOverloads
    {
        public string Get() => "plain";

        public string Get<T>() => typeof(T).Name;
    }
}
