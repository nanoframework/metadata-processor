// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace StubsGenerationTestNFApp
{
    internal class NativeMethodGeneration
    {
        public void Method()
        {
            NativeMethod();

            byte a = 0;
            ushort b = 0;
            NativeMethodWithReferenceParameters(ref a, ref b);
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        private extern void NativeMethod();

        [MethodImpl(MethodImplOptions.InternalCall)]
        private extern void NativeMethodWithReferenceParameters(ref byte refByteParam, ref ushort refUshortParam);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern void NativeStaticMethod();

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern byte NativeStaticMethodReturningByte(char charParam);
    }
}