// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Regression test for: generic-instance TypeSpec entries reached only as an instruction operand
// (castclass, isinst, newarr...) being removed during assembly minimization because the type has no
// member references, causing "Can't find type reference" when writing the IL in the second pass.

using System;
using System.Collections.Generic;

namespace TestNFApp
{
    // Declared in this assembly, with no members and no implementers, so nothing other than the
    // instructions below keeps its instantiations in use.
    public interface IMemberlessGeneric<T>
    {
    }

    public class InstructionOnlyGenericTypeSpecTests
    {
        // Same as IMemberlessGeneric<T>, nested in another type.
        public interface INestedMemberlessGeneric<T>
        {
        }

        // Same instantiations reached through signatures only, with no instruction using them as an operand.
        public class SignatureOnlyHolder
        {
            public IEnumerable<uint> Items;
            public IMemberlessGeneric<uint>[] Memberless;

            public static int Takes(IEnumerable<uint> items, INestedMemberlessGeneric<uint> nested) => 1;
        }

        public InstructionOnlyGenericTypeSpecTests()
        {
            Console.WriteLine("++++++++++++++++++++++++++++++++++++++++");
            Console.WriteLine("++ InstructionOnlyGenericTypeSpec Tests++");
            Console.WriteLine("++++++++++++++++++++++++++++++++++++++++");

            object value = new object();

            // isinst
            if (value is IMemberlessGeneric<int>)
            {
                throw new Exception("object must not be IMemberlessGeneric<int>");
            }

            if (value is INestedMemberlessGeneric<int>)
            {
                throw new Exception("object must not be INestedMemberlessGeneric<int>");
            }

            if (value is IEnumerable<int>)
            {
                throw new Exception("object must not be IEnumerable<int>");
            }

            // castclass, null always succeeds
            object nothing = null;

            if ((IMemberlessGeneric<long>)nothing != null
                || (INestedMemberlessGeneric<long>)nothing != null
                || (IEnumerable<long>)nothing != null)
            {
                throw new Exception("casting null must give null");
            }

            // castclass to an array of generic instances, and to a generic instance as argument of another
            if ((IMemberlessGeneric<sbyte>[])nothing != null
                || (IEnumerable<sbyte>[])nothing != null
                || (IEnumerable<IMemberlessGeneric<byte>>)nothing != null)
            {
                throw new Exception("casting null must give null");
            }

            // newarr
            if (new IMemberlessGeneric<short>[1].Length != 1
                || new INestedMemberlessGeneric<short>[1].Length != 1
                || new IEnumerable<short>[1].Length != 1)
            {
                throw new Exception("arrays must have one element");
            }

            SignatureOnlyHolder holder = new SignatureOnlyHolder();

            if (holder.Items != null || holder.Memberless != null || SignatureOnlyHolder.Takes(null, null) != 1)
            {
                throw new Exception("signature only members have unexpected values");
            }
        }
    }
}
