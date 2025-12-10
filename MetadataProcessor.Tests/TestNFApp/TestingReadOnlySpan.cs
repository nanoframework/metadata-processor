// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using TestNFClassLibrary;

namespace TestNFApp
{
    public class TestingReadOnlySpan
    {
        public TestingReadOnlySpan()
        {
            int length = 3;

            // base type - ReadOnlySpan<int>
            Console.WriteLine("+++ ReadOnlySpan<int> tests");
            Console.WriteLine();

            int[] numbersArray = new int[length];
            for (int i = 0; i < length; i++)
            {
                numbersArray[i] = i;
            }

            ReadOnlySpan<int> numbers = new ReadOnlySpan<int>(numbersArray);

            // Test indexer access (uses ref readonly modreq return type)
            for (int i = 0; i < numbers.Length; i++)
            {
                Console.WriteLine($">>numbers[{i}]: {numbers[i]}");
            }

            // Test Length property
            Console.WriteLine($">>Length: {numbers.Length}");

            // Test IsEmpty property
            Console.WriteLine($">>IsEmpty: {numbers.IsEmpty}");

            // string type - ReadOnlySpan<string>
            Console.WriteLine("+++ ReadOnlySpan<string> tests");
            Console.WriteLine();

            string[] stringsArray = new string[length];
            for (int i = 0; i < length; i++)
            {
                stringsArray[i] = $"string_{i * 10}";
            }

            ReadOnlySpan<string> strings = new ReadOnlySpan<string>(stringsArray);

            // Test indexer with reference types
            for (int i = 0; i < strings.Length; i++)
            {
                Console.WriteLine($">>strings[{i}]: '{strings[i]}'");
            }

            // class type - ReadOnlySpan<ClassOnAnotherAssembly>
            Console.WriteLine("+++ ReadOnlySpan<ClassOnAnotherAssembly> tests");
            Console.WriteLine();

            ClassOnAnotherAssembly[] classArray = new ClassOnAnotherAssembly[length];
            for (int i = 0; i < length; i++)
            {
                classArray[i] = new ClassOnAnotherAssembly(i * 100);
            }

            ReadOnlySpan<ClassOnAnotherAssembly> spanOfClass = new ReadOnlySpan<ClassOnAnotherAssembly>(classArray);

            // Test indexer with custom class (tests modreq handling)
            for (int i = 0; i < spanOfClass.Length; i++)
            {
                Console.WriteLine($">>spanOfClass[{i}].DummyProperty: '{spanOfClass[i].DummyProperty}'");
            }

            // Test constructor with start and length
            Console.WriteLine("+++ ReadOnlySpan with start/length tests");
            Console.WriteLine();

            ReadOnlySpan<int> subspan = new ReadOnlySpan<int>(numbersArray, 1, 2);
            for (int i = 0; i < subspan.Length; i++)
            {
                Console.WriteLine($">>subspan[{i}]: {subspan[i]}");
            }

            // Test ToArray method
            Console.WriteLine("+++ ReadOnlySpan.ToArray() tests");
            Console.WriteLine();

            int[] copiedArray = numbers.ToArray();
            for (int i = 0; i < copiedArray.Length; i++)
            {
                Console.WriteLine($">>copiedArray[{i}]: {copiedArray[i]}");
            }

            // Test empty span
            Console.WriteLine("+++ Empty ReadOnlySpan tests");
            Console.WriteLine();

            ReadOnlySpan<int> emptySpan = new ReadOnlySpan<int>(new int[0]);
            Console.WriteLine($">>emptySpan.IsEmpty: {emptySpan.IsEmpty}");
            Console.WriteLine($">>emptySpan.Length: {emptySpan.Length}");

            // Test equality operators
            Console.WriteLine("+++ ReadOnlySpan equality tests");
            Console.WriteLine();

            ReadOnlySpan<int> span1 = new ReadOnlySpan<int>(numbersArray);
            ReadOnlySpan<int> span2 = new ReadOnlySpan<int>(numbersArray);
            Console.WriteLine($">>span1 == span2: {span1 == span2}");
            Console.WriteLine($">>span1 != span2: {span1 != span2}");

            // Test implicit conversion from array
            Console.WriteLine("+++ Implicit conversion tests");
            Console.WriteLine();

            ReadOnlySpan<int> implicitSpan = numbersArray;
            Console.WriteLine($">>implicitSpan.Length: {implicitSpan.Length}");
            Console.WriteLine($">>implicitSpan[0]: {implicitSpan[0]}");
        }
    }
}
