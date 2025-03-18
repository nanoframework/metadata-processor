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
            // as a nested type
            IOException.IOExceptionErrorCode dummyEnum = IOException.IOExceptionErrorCode.DirectoryNotFound;

            _ = new IOException(
                    string.Empty,
                    (int)dummyEnum);

            // This test checks if MDP can minimize the assembly using an enum that is defined in another assembly
            Base64FormattingOptions formattingOptions = Base64FormattingOptions.InsertLineBreaks;

            byte[] testBytes = new byte[] {0x01, 0x03, 0x05, 0x07, 0x09 };

            _ = Convert.ToBase64String(
                testBytes,
                0,
                testBytes.Length,
                formattingOptions);
        }
    }
}
