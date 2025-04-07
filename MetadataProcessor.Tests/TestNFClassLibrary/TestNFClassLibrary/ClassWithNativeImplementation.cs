// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// this class should be added only for unit tests in MDP
#if MDP_UNIT_TESTS_BUILD

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

#endif
