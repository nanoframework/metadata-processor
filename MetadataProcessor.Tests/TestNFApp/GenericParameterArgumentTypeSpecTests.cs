// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Regression test for: a TypeSpec entry describing a generic instance whose own argument is a bare
// generic parameter (VAR/MVAR) rather than a closed type -- e.g. Pair<T,int> used inside the body of
// the generic type Container<T> that declares T. Before the fix, BuildTypeSpecArg (metadata processor,
// Pdbx/PdbxFileHelpers.cs) fell through to its ordinary-class handling for such an argument, which
// calls TypeReference.Resolve() on the bare GenericParameter (returns null, since it has no
// TypeDefinition of its own) and records a meaningless, unqualified ClassName ("T") with no token --
// losing which parameter it is and whose type or method declares it. Exercises both the type-owned
// (VAR) and method-owned (MVAR) cases in one closed instantiation each so the corresponding metadata
// processor unit tests (Core/Tables/nanoTypeSpecArgGenericParameterTests.cs) have real TypeSpec rows
// to inspect.

using System;

namespace TestNFApp
{
    // A struct defined in this assembly, used as the nested generic whose own argument is a generic
    // parameter of the enclosing type/method.
    public struct GenParamPair<TKey, TValue>
    {
        public TKey Key;
        public TValue Value;

        public GenParamPair(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }
    }

    public class GenParamContainer<T>
    {
        // Field type GenParamPair<T,int>: the TypeSpec's own generic argument list is [T, int], where
        // T is a type-owned generic parameter (VAR) -- the case BuildTypeSpecArg must not fall through
        // to ordinary-class handling for.
        public GenParamPair<T, int> Slot;

        public GenParamContainer(T value)
        {
            Slot = new GenParamPair<T, int>(value, 0);
        }

        // Method type parameter U closes GenParamPair<U,T>: this TypeSpec's argument list is [U, T],
        // where U is method-owned (MVAR) and T is type-owned (VAR) -- both cases in one TypeSpec.
        public GenParamPair<U, T> Wrap<U>(U value)
        {
            return new GenParamPair<U, T>(value, Slot.Key);
        }
    }

    public class GenericParameterArgumentTypeSpecTests
    {
        public GenericParameterArgumentTypeSpecTests()
        {
            Console.WriteLine("++++++++++++++++++++++++++++++++++++++++++++");
            Console.WriteLine("++ GenericParameterArgumentTypeSpec Tests  ++");
            Console.WriteLine("++++++++++++++++++++++++++++++++++++++++++++");

            var container = new GenParamContainer<string>("outer");

            if (container.Slot.Key != "outer" || container.Slot.Value != 0)
            {
                throw new Exception(
                    $"Slot mismatch: Key={container.Slot.Key}, Value={container.Slot.Value}");
            }

            GenParamPair<int, string> wrapped = container.Wrap(42);

            if (wrapped.Key != 42 || wrapped.Value != "outer")
            {
                throw new Exception(
                    $"Wrap mismatch: Key={wrapped.Key}, Value={wrapped.Value}");
            }

            Console.WriteLine($"  Slot = [{container.Slot.Key}] = {container.Slot.Value}");
            Console.WriteLine($"  Wrap = [{wrapped.Key}] = {wrapped.Value}");
        }
    }
}
