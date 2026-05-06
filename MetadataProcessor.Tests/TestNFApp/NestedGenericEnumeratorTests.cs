// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

namespace TestNFApp
{
    /// <summary>
    /// Regression test for nested generic TypeSpec encoding bug.
    /// Tests that List-like and Dictionary-like generic containers with nested Enumerator types
    /// are correctly distinguished in metadata (their TypeSpecs must have different signatures).
    /// </summary>
    public class NestedGenericEnumeratorTests
    {
        /// <summary>
        /// Generic container with single type parameter, similar to List&lt;T&gt;.
        /// Contains a nested non-generic Enumerator struct.
        /// </summary>
        public class Container1<T>
        {
            private T[] _items;
            private int _count;

            public Container1()
            {
                _items = new T[4];
                _count = 0;
            }

            public void Add(T item)
            {
                if (_count == _items.Length)
                {
                    T[] newArray = new T[_items.Length * 2];
                    for (int i = 0; i < _count; i++)
                    {
                        newArray[i] = _items[i];
                    }
                    _items = newArray;
                }
                _items[_count++] = item;
            }

            public Enumerator GetEnumerator()
            {
                return new Enumerator(this);
            }

            /// <summary>
            /// Nested non-generic enumerator struct.
            /// When instantiated as Container1&lt;int&gt;.Enumerator, this becomes a TypeSpec
            /// that must include the parent's concrete type argument [int] in its signature.
            /// </summary>
            public struct Enumerator
            {
                private readonly Container1<T> _container;
                private int _index;
                private T _current;

                internal Enumerator(Container1<T> container)
                {
                    _container = container;
                    _index = 0;
                    _current = default(T);
                }

                public T Current => _current;

                public bool MoveNext()
                {
                    if (_index < _container._count)
                    {
                        _current = _container._items[_index];
                        _index++;
                        return true;
                    }
                    return false;
                }

                public void Reset()
                {
                    _index = 0;
                    _current = default(T);
                }
            }
        }

        /// <summary>
        /// Generic container with two type parameters, similar to Dictionary&lt;TKey, TValue&gt;.
        /// Contains a nested non-generic Enumerator struct with the SAME NAME as Container1's.
        /// </summary>
        public class Container2<TKey, TValue>
        {
            private TKey[] _keys;
            private TValue[] _values;
            private int _count;

            public Container2()
            {
                _keys = new TKey[4];
                _values = new TValue[4];
                _count = 0;
            }

            public void Add(TKey key, TValue value)
            {
                if (_count == _keys.Length)
                {
                    TKey[] newKeys = new TKey[_keys.Length * 2];
                    TValue[] newValues = new TValue[_values.Length * 2];
                    for (int i = 0; i < _count; i++)
                    {
                        newKeys[i] = _keys[i];
                        newValues[i] = _values[i];
                    }
                    _keys = newKeys;
                    _values = newValues;
                }
                _keys[_count] = key;
                _values[_count] = value;
                _count++;
            }

            public Enumerator GetEnumerator()
            {
                return new Enumerator(this);
            }

            /// <summary>
            /// Nested non-generic enumerator struct with SAME NAME as Container1.Enumerator.
            /// When instantiated as Container2&lt;int,string&gt;.Enumerator, this becomes a TypeSpec
            /// that must include the parent's concrete type arguments [int, string] in its signature.
            /// 
            /// CRITICAL: The metadata processor must NOT confuse this with Container1&lt;int&gt;.Enumerator,
            /// even though both have a simple name "Enumerator" and both contain a single type argument [int].
            /// The parent type's identity (Container1 vs Container2) and full generic argument list
            /// must be encoded in the TypeSpec signature.
            /// </summary>
            public struct Enumerator
            {
                private readonly Container2<TKey, TValue> _container;
                private int _index;
                private TKey _currentKey;
                private TValue _currentValue;

                internal Enumerator(Container2<TKey, TValue> container)
                {
                    _container = container;
                    _index = 0;
                    _currentKey = default(TKey);
                    _currentValue = default(TValue);
                }

                public TKey CurrentKey => _currentKey;
                public TValue CurrentValue => _currentValue;

                public bool MoveNext()
                {
                    if (_index < _container._count)
                    {
                        _currentKey = _container._keys[_index];
                        _currentValue = _container._values[_index];
                        _index++;
                        return true;
                    }
                    return false;
                }

                public void Reset()
                {
                    _index = 0;
                    _currentKey = default(TKey);
                    _currentValue = default(TValue);
                }
            }
        }

        public NestedGenericEnumeratorTests()
        {
            TestContainer1WithInt();
            TestContainer1WithString();
            TestContainer2WithIntString();
            TestBothContainersWithInt();
        }

        /// <summary>
        /// Test Container1&lt;int&gt;.Enumerator.MoveNext() encoding.
        /// The IL will contain a call to MoveNext on a TypeSpec representing Container1&lt;int&gt;.Enumerator.
        /// </summary>
        private void TestContainer1WithInt()
        {
            Debug.WriteLine("Testing Container1<int>.Enumerator");

            Container1<int> container = new Container1<int>();
            container.Add(10);
            container.Add(20);
            container.Add(30);

            // This loop causes the compiler to emit:
            // - call Container1<int>::GetEnumerator() returning Container1<int>.Enumerator
            // - call Container1<int>.Enumerator::MoveNext() on the TypeSpec
            // - call Container1<int>.Enumerator::get_Current() on the TypeSpec
            Container1<int>.Enumerator enumerator = container.GetEnumerator();
            while (enumerator.MoveNext())
            {
                Debug.WriteLine($"  Item: {enumerator.Current}");
            }
        }

        /// <summary>
        /// Test Container1&lt;string&gt;.Enumerator.MoveNext() encoding with different type argument.
        /// </summary>
        private void TestContainer1WithString()
        {
            Debug.WriteLine("Testing Container1<string>.Enumerator");

            Container1<string> container = new Container1<string>();
            container.Add("Hello");
            container.Add("World");

            Container1<string>.Enumerator enumerator = container.GetEnumerator();
            while (enumerator.MoveNext())
            {
                Debug.WriteLine($"  Item: {enumerator.Current}");
            }
        }

        /// <summary>
        /// Test Container2&lt;int,string&gt;.Enumerator.MoveNext() encoding.
        /// The IL will contain a call to MoveNext on a TypeSpec representing Container2&lt;int,string&gt;.Enumerator.
        /// 
        /// CRITICAL REGRESSION TEST: This TypeSpec must NOT be confused with Container1&lt;int&gt;.Enumerator,
        /// even though both contain [int] as a type argument and both nested types are named "Enumerator".
        /// </summary>
        private void TestContainer2WithIntString()
        {
            Debug.WriteLine("Testing Container2<int,string>.Enumerator");

            Container2<int, string> container = new Container2<int, string>();
            container.Add(1, "One");
            container.Add(2, "Two");
            container.Add(3, "Three");

            // This loop causes the compiler to emit:
            // - call Container2<int,string>::GetEnumerator() returning Container2<int,string>.Enumerator
            // - call Container2<int,string>.Enumerator::MoveNext() on the TypeSpec
            // - call Container2<int,string>.Enumerator::get_CurrentKey/CurrentValue() on the TypeSpec
            Container2<int, string>.Enumerator enumerator = container.GetEnumerator();
            while (enumerator.MoveNext())
            {
                Debug.WriteLine($"  Key: {enumerator.CurrentKey}, Value: {enumerator.CurrentValue}");
            }
        }

        /// <summary>
        /// Test both containers with int type argument in the same method.
        /// This ensures both TypeSpecs are present in the same assembly and must be distinguished.
        /// </summary>
        private void TestBothContainersWithInt()
        {
            Debug.WriteLine("Testing both containers with int");

            // Container1<int>.Enumerator - single type argument [int]
            Container1<int> container1 = new Container1<int>();
            container1.Add(100);
            Container1<int>.Enumerator enum1 = container1.GetEnumerator();
            if (enum1.MoveNext())
            {
                Debug.WriteLine($"  Container1: {enum1.Current}");
            }

            // Container2<int,int>.Enumerator - two type arguments [int, int]
            // Even though both type args are int, the parent type is different (Container2 vs Container1)
            // and the generic arity is different (2 vs 1)
            Container2<int, int> container2 = new Container2<int, int>();
            container2.Add(200, 300);
            Container2<int, int>.Enumerator enum2 = container2.GetEnumerator();
            if (enum2.MoveNext())
            {
                Debug.WriteLine($"  Container2: Key={enum2.CurrentKey}, Value={enum2.CurrentValue}");
            }
        }
    }
}
