// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace TestNFApp
{
    public class Stack<T>
    {
        private readonly T[] _items;
        private int _count;

        public Stack(int size) => _items = new T[size];

        public void Push(T item) => _items[_count++] = item;

        public T Pop() => _items[--_count];
    }

    public class StatckTests
    {
        public StatckTests()
        {
            // Create a stack of integers
            Stack<int> intStack = new Stack<int>(5);

            intStack.Push(1);
            intStack.Push(2);

            Console.WriteLine($"First value is{intStack.Pop()}");
            Console.WriteLine($"Second value is{intStack.Pop()}");

            // Create a stack of strings
            Stack<string> stringStack = new Stack<string>(5);

            stringStack.Push("Hello");
            stringStack.Push("World");

            Console.WriteLine($"First value is {stringStack.Pop()}");
            Console.WriteLine($"Second value is {stringStack.Pop()}");
        }
    }
}
