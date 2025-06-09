// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

namespace TestNFApp
{
    public class SimpleList<T>
    {
        private T[] _items;
        private int _count;

        public T[] Enumerable
        {
            get
            {
                T[] values = new T[_count];
                Array.Copy(_items, values, _count);
                return values;
            }
        }
        public SimpleList()
        {
            _items = new T[4]; // Initial capacity
            _count = 0;
        }

        public void Add(T item)
        {
            if (_count == _items.Length)
            {
                ResizeArray(); // Resize when full
            }
            _items[_count++] = item;
        }

        public bool Remove(T item)
        {
            for (int i = 0; i < _count; i++)
            {
                if (item.Equals(_items[i]))
                {
                    for (int j = i; j < _count - 1; j++)
                    {
                        _items[j] = _items[j + 1];
                    }
                    _items[--_count] = default(T);
                    return true;
                }
            }
            return false;
        }

        private void ResizeArray()
        {
            int newSize = _items.Length * 2;
            T[] newArray = new T[newSize];


            for (int i = 0; i < _count; i++)
            {
                newArray[i] = _items[i];
            }

            _items = newArray;
        }


        public int Count => _count;
    }

    public class SimpleListTests
    {
        public SimpleListTests()
        {
            SimpleList<int> list = new SimpleList<int>();

            for (int i = 0; i < 10; i++)
            {
                list.Add(i);
            }

            foreach (int i in list.Enumerable)
            {
                Console.WriteLine($">> {i}");
            }
        }
    }
}
