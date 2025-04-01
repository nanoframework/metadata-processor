// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace TestNFApp
{
    public class AuthorAttribute : Attribute
    {
        private readonly string _author;

        public string Author => _author;

        public AuthorAttribute(string author)
        {
            _author = author;
        }
    }
}
