//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

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
