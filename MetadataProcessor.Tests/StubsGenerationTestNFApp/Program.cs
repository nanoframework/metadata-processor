// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;

namespace StubsGenerationTestNFApp
{
    public class Program
    {
        public static void Main()
        {
            NativeMethodGeneration nativeMethods = new NativeMethodGeneration();
            nativeMethods.Method();

            NativeMethodGenerationGenerics<int> nativeMethodGenerationGenerics = new NativeMethodGenerationGenerics<int>();

            nativeMethodGenerationGenerics.Method<int>();

            Thread.Sleep(Timeout.Infinite);
        }
    }
}
