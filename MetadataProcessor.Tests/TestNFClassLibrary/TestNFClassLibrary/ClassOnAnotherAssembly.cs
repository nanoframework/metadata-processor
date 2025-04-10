// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace TestNFClassLibrary
{
    public class ClassOnAnotherAssembly
    {
        private readonly int _dummyParameter;

        public int DummyProperty => _dummyParameter;

        public ClassOnAnotherAssembly()
        {

        }

        public ClassOnAnotherAssembly(int dummyParameter)
        {
            _dummyParameter = dummyParameter;
        }
    }
}
