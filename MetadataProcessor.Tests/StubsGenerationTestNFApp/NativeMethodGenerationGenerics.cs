// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace StubsGenerationTestNFApp
{
    internal class NativeMethodGenerationGenerics<T>
    {
        public void Method<U>()
        {
            NativeMethod();

            byte a = 0;
            ushort b = 0;
            U genParam = default;

            NativeMethodWithReferenceParameters<T, U>(ref a, ref b);
            
            NativeStaticMethod<U>(default);
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        private extern void NativeMethod();

        [MethodImpl(MethodImplOptions.InternalCall)]
        private extern void NativeMethodWithReferenceParameters<U, V>(ref byte refByteParam, ref ushort refUshortParam);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern void NativeStaticMethod<U>(T genParam);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern T NativeStaticMethodReturningType(char charParam);
    }
}
