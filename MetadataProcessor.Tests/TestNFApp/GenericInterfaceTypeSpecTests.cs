// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Regression test for: TypeSpec entries used only as interface implementations
// (no member references) being incorrectly removed during assembly minimization,
// causing "Can't find entry in type reference table" when writing interface
// signatures in the second pass.

using System;
using System.Collections;

namespace TestNFApp
{
    // A struct defined in this assembly, used as a generic type argument.
    // Analogous to KeyValuePair<TKey,TValue> in System.Collections.
    public struct Pair<TKey, TValue>
    {
        public TKey Key;
        public TValue Value;

        public Pair(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }
    }

    // An interface that inherits the non-generic System.Collections.IEnumerable.
    // The C# compiler adds System.Collections.IEnumerable to InterfaceImpls —
    // a non-generic TypeSpec with no member references in this assembly.
    public interface IPairCollection<TKey, TValue> : IEnumerable
    {
        int Count { get; }
        Pair<TKey, TValue> GetAt(int index);
    }

    // A concrete implementation — exercises the TypeSpec in the interface list.
    public class PairCollection<TKey, TValue> : IPairCollection<TKey, TValue>
    {
        private Pair<TKey, TValue>[] _items;
        private int _count;

        public PairCollection()
        {
            _items = new Pair<TKey, TValue>[4];
            _count = 0;
        }

        public int Count => _count;

        public void Add(TKey key, TValue value)
        {
            if (_count == _items.Length)
            {
                var bigger = new Pair<TKey, TValue>[_items.Length * 2];
                for (int i = 0; i < _count; i++) bigger[i] = _items[i];
                _items = bigger;
            }

            _items[_count++] = new Pair<TKey, TValue>(key, value);
        }

        public Pair<TKey, TValue> GetAt(int index) => _items[index];

        public IEnumerator GetEnumerator() => _items.GetEnumerator();
    }

    public class GenericInterfaceTypeSpecTests
    {
        public GenericInterfaceTypeSpecTests()
        {
            Console.WriteLine("++++++++++++++++++++++++++++++++++++++++");
            Console.WriteLine("++ GenericInterfaceTypeSpec Tests      ++");
            Console.WriteLine("++++++++++++++++++++++++++++++++++++++++");

            var pairs = new PairCollection<int, string>();
            pairs.Add(1, "one");
            pairs.Add(2, "two");
            pairs.Add(3, "three");
            pairs.Add(4, "four");
            pairs.Add(5, "five"); // forces _items growth: exercises new Pair<TKey,TValue>[] allocation

            // Validate resize occurred: _count must be 5 and all previous elements preserved
            if (pairs.Count != 5) throw new Exception($"Expected Count 5 but got {pairs.Count}");

            string[] expected = { "one", "two", "three", "four", "five" };
            for (int i = 0; i < pairs.Count; i++)
            {
                Pair<int, string> p = pairs.GetAt(i);
                if (p.Key != i + 1 || p.Value != expected[i])
                    throw new Exception($"Element at {i} mismatch: [{p.Key}]={p.Value}");
                Console.WriteLine($"  [{p.Key}] = {p.Value}");
            }
        }
    }
}
