﻿//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System;

namespace TestNFApp
{
    public class MaxAttribute : Attribute
    {
        private readonly uint _max;

        public uint Max  => _max;

        public MaxAttribute(uint m)
        {
            _max = m;
        }
    }
}
