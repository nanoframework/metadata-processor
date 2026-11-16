// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace TestNFApp
{
    public interface IBoundMarker<T>
    {
    }

    public interface IBoundOut<out T>
    {
        T Get();
    }

    public interface IBoundIn<in T>
    {
        void Set(T value);
    }

    public class TypeOfFirst<T>
    {
        public Type Get()
        {
            return typeof(T);
        }

        public Type Method<U>()
        {
            return typeof(U);
        }
    }

    public class TypeOfSecond<T>
    {
        public Type Get()
        {
            return typeof(T);
        }
    }

    public class ConstrainedClass<T> where T : class, IBoundMarker<T>
    {
    }

    public struct ConstrainedStruct<T> where T : struct
    {
    }

    public class NotConstrained<T>
    {
    }

    public class ImplementsOwnParameters<T, U> : IBoundMarker<T>
    {
    }

    // field types keep their TypeSpec rows
    public class UsesClosedInstances
    {
        public IBoundMarker<int> First;
        public IBoundMarker<string> Second;
        public IBoundMarker<IBoundMarker<int>> Nested;
    }
}
