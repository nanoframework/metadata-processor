// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Regression test for generic-instance TypeSpecs and the type references behind them being removed during
// assembly minimization when they are only reached from a method return type.
// In Release the compiler emits no local to keep them in use.

using System;
using System.Collections;
using System.Collections.Generic;

namespace TestNFApp
{
    // The calls to TestNFClassLibrary.ExternalGenericSignatures reach the generic instances through member references only:
    // their arguments are not used anywhere else in this assembly.
    // Trivial bodies on purpose: with optimizations there are no locals, so the return types are the only references.
    public class GenericReturnTypeTypeSpecTests : IEnumerable<string>
    {
        public IEnumerator<string> GetEnumerator()
        {
            return null;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return null;
        }

        // external generics used nowhere else in this assembly, so the return type is the only reference to them
        // external generic only as the return type
        public static ICollection<ulong> ReturnsInstance()
        {
            return null;
        }

        // external generic only as the argument of the generic return type
        public static IEnumerable<IComparer<uint>> ReturnsNestedArgument()
        {
            return null;
        }

        // generic instances inside arrays
        public static ICollection<long>[] ReturnsArrayOfInstances()
        {
            return null;
        }

        public GenericReturnTypeTypeSpecTests()
        {
            Console.WriteLine("++++++++++++++++++++++++++++++++++++++++");
            Console.WriteLine("++ GenericReturnTypeTypeSpec Tests ++");
            Console.WriteLine("++++++++++++++++++++++++++++++++++++++++");

            if (GetEnumerator() != null
                || ReturnsInstance() != null
                || ReturnsNestedArgument() != null
                || ReturnsArrayOfInstances() != null
                || TestNFClassLibrary.ExternalGenericSignatures.Items() != null
                || TestNFClassLibrary.ExternalGenericSignatures.Field != null)
            {
                throw new Exception("methods must return null");
            }
        }
    }
}
