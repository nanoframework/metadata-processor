using System;
using System.Runtime.CompilerServices;

namespace GenerationTestNFApp
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
    }
}