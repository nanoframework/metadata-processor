//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

namespace System.IO
{
    public class TestEnumInAnotherAssembly
    {
        public void CallTestEnumInAnotherAssembly()
        {
            // This test checks if MDP can minimize the assembly using an enum that is defined in another assembly
            // and the class calling it is in a different assembly BUT in the same namespace.
            IOException.IOExceptionErrorCode dummyEnum = IOException.IOExceptionErrorCode.DirectoryNotFound;

            _ = new IOException(
                    string.Empty,
                    (int)dummyEnum);
        }
    }
}
