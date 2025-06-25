// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using TestNFClassLibrary;

namespace TestNFApp
{
    public class TestingSpan
    {
        public TestingSpan()
        {
            int length = 3;

            // base type
            Console.WriteLine("+++ Span<int> tests");
            Console.WriteLine();

            Span<int> numbers = new Span<int>(new int[length]);
            for (int i = 0; i < length; i++)
            {
                numbers[i] = i;
            }

            foreach (int number in numbers)
            {
                Console.WriteLine($">>{number.ToString()}");
            }

            // string type
            Console.WriteLine("+++ Span<string> tests");
            Console.WriteLine();

            Span<string> strings = new Span<string>(new string[length]);
            for (int i = 1; i < length; i++)
            {
                strings[i] = $">> '{i * 10}'";
            }

            foreach (string str in strings)
            {
                Console.WriteLine($">> '{str}'");
            }

            // class type
            Console.WriteLine("+++ Span<ClassOnAnotherAssembly> tests");
            Span<ClassOnAnotherAssembly> spanOfClass = new Span<ClassOnAnotherAssembly>(new ClassOnAnotherAssembly[length]);

            for (int i = 0; i < length; i++)
            {
                spanOfClass[i] = new ClassOnAnotherAssembly(i * 100);
            }

            foreach (ClassOnAnotherAssembly item in spanOfClass)
            {
                Console.WriteLine($">> '{item.DummyProperty}'");
            }
        }
    }
}
