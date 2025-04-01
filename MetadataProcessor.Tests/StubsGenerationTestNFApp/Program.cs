// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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