//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System.Threading;

namespace StubsGenerationTestNFApp
{
    public class Program
    {
        public static void Main()
        {
            var nativeMethods = new NativeMethodGeneration();
            nativeMethods.Method();

            Thread.Sleep(Timeout.Infinite);
        }
    }
}