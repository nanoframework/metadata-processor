// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Regression test for: when a nested generic struct is used as a local-variable type,
// the emitted TypeSpec signature blob must include ALL cumulative type arguments
// (enclosing type's args first, then the nested type's own args), and the VAR indices
// in field signatures must be absolute (cumulative) rather than relative to the
// declaring type. Without the fix, GetGenericParam(1, TypeSpec) fails at runtime
// because only 1 arg is recorded instead of 2.

using System;

namespace TestNFApp
{
    /// <summary>
    /// Generic container class whose nested struct exercises the cumulative-VAR bug.
    /// VAR(0) = TOuter (from Container&lt;TOuter&gt;)
    /// VAR(1) = TInner (from Entry&lt;TInner&gt;'s own parameter)
    /// </summary>
    public class Container<TOuter>
    {
        /// <summary>
        /// Nested generic struct.  When instantiated as Container&lt;int&gt;.Entry&lt;string&gt;
        /// the TypeSpec must carry 2 args: [int, string].
        /// </summary>
        public struct Entry<TInner>
        {
            /// <summary>
            /// Typed as TOuter — absolute VAR(0).
            /// </summary>
            public TOuter OuterValue;

            /// <summary>
            /// Typed as TInner — absolute VAR(1) (relative position 0 within Entry,
            /// but position 1 cumulatively because Container has one type param before it).
            /// </summary>
            public TInner InnerValue;

            public Entry(TOuter outerValue, TInner innerValue)
            {
                OuterValue = outerValue;
                InnerValue = innerValue;
            }
        }
    }

    /// <summary>
    /// Regression test that is executed at runtime on nanoCLR.
    /// Uses <c>Container&lt;int&gt;.Entry&lt;string&gt;</c> — deliberately choosing
    /// int ≠ string so that swapping VAR(0)/VAR(1) produces an obviously wrong value.
    /// </summary>
    public class NestedGenericTypeSpecTests
    {
        public NestedGenericTypeSpecTests()
        {
            Console.WriteLine("+++++++++++++++++++++++++++++++++++++++++++++++");
            Console.WriteLine("++ NestedGenericTypeSpec Tests               ++");
            Console.WriteLine("+++++++++++++++++++++++++++++++++++++++++++++++");

            // Local variable of nested generic struct type.
            // The TypeSpec for Container<int>.Entry<string> must contain 2 type args:
            //   arg[0] = int  (Container's TOuter)
            //   arg[1] = string (Entry's TInner)
            Container<int>.Entry<string> entry = new Container<int>.Entry<string>(42, "hello");

            if (entry.OuterValue != 42)
            {
                throw new Exception($"OuterValue mismatch: expected 42, got {entry.OuterValue}");
            }

            if (entry.InnerValue != "hello")
            {
                throw new Exception($"InnerValue mismatch: expected 'hello', got {entry.InnerValue}");
            }

            Console.WriteLine($"  entry.OuterValue = {entry.OuterValue}");
            Console.WriteLine($"  entry.InnerValue = {entry.InnerValue}");

            // Also test with value types on both sides to catch size/layout errors.
            Container<bool>.Entry<int> entry2 = new Container<bool>.Entry<int>(true, 99);

            if (entry2.OuterValue != true)
            {
                throw new Exception($"OuterValue2 mismatch: expected true, got {entry2.OuterValue}");
            }

            if (entry2.InnerValue != 99)
            {
                throw new Exception($"InnerValue2 mismatch: expected 99, got {entry2.InnerValue}");
            }

            Console.WriteLine($"  entry2.OuterValue = {entry2.OuterValue}");
            Console.WriteLine($"  entry2.InnerValue = {entry2.InnerValue}");

            Console.WriteLine("++ NestedGenericTypeSpec Tests passed        ++");
        }
    }
}
