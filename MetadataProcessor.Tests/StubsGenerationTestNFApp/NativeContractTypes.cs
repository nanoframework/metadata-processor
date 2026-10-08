// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;

namespace StubsGenerationTestNFApp
{
    /// <summary>
    /// Type declaring native methods with static and instance fields.
    /// </summary>
    internal class NativeWithFields
    {
        private static int s_counter;

        private int _value;
        private byte _flag;

        public int Value => _value + _flag + s_counter;

        [MethodImpl(MethodImplOptions.InternalCall)]
        private extern int NativeGetValue();

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern void NativeReset();
    }

    /// <summary>
    /// Type with native methods deriving from a type in another assembly (mscorlib) with instance fields.
    /// </summary>
    internal class NativeException : Exception
    {
        private int _nativeErrorCode;

        public int NativeErrorCode => _nativeErrorCode;

        [MethodImpl(MethodImplOptions.InternalCall)]
        private extern void NativeFill();
    }

    /// <summary>
    /// Managed-only type with fields: still gets field constants and is part of the native contract.
    /// </summary>
    internal class ManagedOnlyWithFields
    {
        private int _managedValue;
        private string _managedName;

        public int ManagedValue => _managedValue;

        public string ManagedName => _managedName;
    }

    internal delegate int IntProvider();

    /// <summary>
    /// Managed-only type using closures: the compiler-generated types (display classes)
    /// are not part of the native contract.
    /// </summary>
    internal class CompilerGeneratedTypesHost
    {
        private int _seed;

        public IntProvider GetProvider(int offset)
        {
            // capturing lambda -> compiler-generated display class with instance fields
            return () => _seed + offset;
        }

        public IntProvider GetMultiplier(int factor)
        {
            int local = factor * 2;

            // lambda capturing only locals -> another compiler-generated display class
            // (iterators aren't used here: they currently fail MDP minimization with the test mscorlib)
            return () => local + factor;
        }
    }
}
