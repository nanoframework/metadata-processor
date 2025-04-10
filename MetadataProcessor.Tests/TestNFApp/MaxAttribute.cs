// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace TestNFApp
{
    public class MaxAttribute : Attribute
    {
        private readonly uint _max;

        public uint Max => _max;

        public MaxAttribute(uint m)
        {
            _max = m;
        }
    }
}
