//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System.Runtime.CompilerServices;

namespace TestNFClassLibrary
{
    public class ClassWithNativeImplementation
    {
        private static int _staticField;
        private int _field;

        public static int StaticProperty1 { get; set; }

        public int Property1 { get; set; }

        public void ManagedMethod1()
        {
            NativeMethod1();
        }

        public void ManagedMethod2()
        {
            NativeMethod2();
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void NativeMethod1();

        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern void NativeMethod2();
    }
}
