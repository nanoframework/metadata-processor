﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace TestNFApp
{
    public class ComplexAttribute : Attribute
    {
        private readonly uint _max;
        private readonly string _s;
        private readonly bool _b;

        public uint Max => _max;

        public string S => _s;
        public bool B => _b;

        public ComplexAttribute(uint m, string s, bool b)
        {
            _max = m;
            _s = s;
            _b = b;
        }
    }
}
