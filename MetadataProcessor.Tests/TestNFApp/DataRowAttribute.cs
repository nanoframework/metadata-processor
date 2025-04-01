// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace TestNFApp
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class DataRowAttribute : Attribute
    {
        public DataRowAttribute(params object[] args)
        {
            Arguments = args;
        }

        public object[] Arguments { get; }
    }
}
