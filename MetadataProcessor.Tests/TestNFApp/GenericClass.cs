// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

namespace TestNFApp
{
    interface IDo
    {
        void Do1();
        void Do2();
    }

    interface IDoThat
    {
        void DoThat1();
        void DoThat2();
    }

    class ClassDoThis : IDo
    {
        public void Do1()
        {
            Console.WriteLine($"{nameof(Do1)}");
        }

        public void Do2()
        {
            Console.WriteLine($"{nameof(Do2)}");
        }

        public void DoThis1()
        {
            Console.WriteLine($"{nameof(DoThis1)}");
        }

        public void DoThis2()
        {
            Console.WriteLine($"{nameof(DoThis2)}");
        }
    }

    class ClassDoInt : IDo
    {
        public void Do1()
        {
            Console.WriteLine($"{nameof(Do1)}");
        }

        public void Do2()
        {
            Console.WriteLine($"{nameof(Do2)}");
        }
    }

    class ClassDoThatInt : IDo, IDoThat
    {
        public void Do1()
        {
            Console.WriteLine($"{nameof(Do1)}");
        }

        public void Do2()
        {
            Console.WriteLine($"{nameof(Do2)}");
        }

        public void DoThat1()
        {
            Console.WriteLine($"{nameof(DoThat1)}");
        }

        public void DoThat2()
        {
            Console.WriteLine($"{nameof(DoThat2)}");
        }

    }

    class ClassDoThisAndThat : ClassDoThis, IDoThat
    {
        public void DoThat1()
        {
            Console.WriteLine($"{nameof(DoThat1)}");
        }

        public void DoThat2()
        {
            Console.WriteLine($"{nameof(DoThat2)}");
        }
    }

    public class ClassDoString : IDo
    {
        public void Do1()
        {
            Console.WriteLine($"{nameof(Do1)}");
        }

        public void Do2()
        {
            Console.WriteLine($"{nameof(Do2)}");
        }
    }

    class GenericClass<T>
    {
        public int NativeField;

        public T GenericField;

        public void NoGenerics()
        {
            int v = 1;
        }

        public void InstanceGenericDoOne(T t)
        {
            T v = t;

            Console.WriteLine($"{nameof(InstanceGenericDoOne)} --> {v} is <{v.GetType()}>");
        }

        public void InstanceGenericDoTwo<T2>(T p1, T2 p2)
        {
            T v1 = p1;
            T2 v2 = p2;

            Console.WriteLine($"{nameof(InstanceGenericDoTwo)}<{v2.GetType()}> --> {v1},{v2} is <{v1.GetType()},{v2.GetType()}>");
        }

        public void InstanceGenericDoOneOther<T1>(T1 t)
        {
            T1 v1 = t;

            Console.WriteLine($"{nameof(InstanceGenericDoOneOther)}<{v1.GetType()}> --> {v1} is <{v1.GetType()}>");
        }
    }

    class AnotherGenericClass<T, T1>
    {
        public int NativeField;

        public T GenericField;
        public T1 AnotherGenericField;

        public void InstanceGenericDoOne(T t)
        {
            T v = t;

            Console.WriteLine($"{nameof(InstanceGenericDoOne)} --> {v} is <{typeof(T).FullName}>");
        }

        public void InstanceGenericDoTwo<T2>(T p1, T1 p2, T2 p3)
        {
            T v1 = p1;
            T1 v2 = p2;
            T2 v3 = p3;

            Console.WriteLine($"{nameof(InstanceGenericDoTwo)}<{v3.GetType()}> --> {v1},{v2},{v3} is <{v1.GetType()},{v2.GetType()},{v3.GetType()}>");
        }

        public void InstanceGenericDoOneOther<T2>(T1 p1, T2 p2)
        {
            T1 v1 = p1;
            T2 v2 = p2;

            Console.WriteLine($"{nameof(InstanceGenericDoOneOther)}<{v1.GetType()}> --> {v1},{v2} is <{typeof(T1).FullName},{typeof(T2).FullName}>");
        }
    }

    public class GenericClassTests
    {
        private static void StaticGenericDo<T1, T2>(T1 val, T2 val2) where T1 : IDo where T2 : IDo
        {
            Console.WriteLine($">> {nameof(StaticGenericDo)}<{val.GetType()},{val2.GetType()}>");

            val.Do1();
            val.Do2();
            val2.Do1();
            val2.Do2();
        }

        private static void StaticGenericDoThisAndThat<T1, T2>(T1 val, T2 val2) where T1 : ClassDoThis, IDo where T2 : ClassDoThatInt
        {
            Console.WriteLine($">> {nameof(StaticGenericDoThisAndThat)}<{val.GetType()},{val2.GetType()}>");

            val.DoThis1();
            val.DoThis2();
            val2.DoThat1();
            val2.DoThat2();
        }

        public GenericClassTests()
        {
            Console.WriteLine("++++++++++++++++++++");
            Console.WriteLine("++ Generics Tests ++");
            Console.WriteLine("++++++++++++++++++++");
            Console.WriteLine("");


            Console.WriteLine("++ ClassDoString ++");

            var other = new ClassDoString();
            other.Do1();
            other.Do2();

            Console.WriteLine("++ GenericClass<int> ++");

            var gc1 = new GenericClass<int>();
            gc1.NoGenerics();
            gc1.InstanceGenericDoOne(1);
            gc1.InstanceGenericDoTwo(1, "TWO");
            gc1.InstanceGenericDoOneOther(false);
            gc1.GenericField = 10;

            Console.WriteLine("++ AnotherGenericClass<byte, bool> ++");

            var agc1 = new AnotherGenericClass<byte, bool>();
            agc1.InstanceGenericDoOne(22);
            agc1.InstanceGenericDoTwo(33, false, "NINE");
            agc1.InstanceGenericDoOneOther(true, 44);
            agc1.GenericField = 11;
            agc1.AnotherGenericField = false;

            Console.WriteLine("++ GenericClass<string> ++");

            var gc2 = new GenericClass<string>();
            gc2.InstanceGenericDoOne("ONE");
            gc2.InstanceGenericDoTwo("ONE", "TWO");
            gc2.InstanceGenericDoOneOther(33.33);
            gc2.GenericField = "TEN";

            Console.WriteLine("++ StaticGenericDo Int and String ++");

            StaticGenericDo(new ClassDoInt(), new ClassDoString());

            Console.WriteLine("++ StaticGenericDo String and Int ++");

            StaticGenericDo(new ClassDoString(), new ClassDoInt());

            Console.WriteLine("++ StaticGenericDoThisAndThat This and That ++");

            StaticGenericDoThisAndThat(new ClassDoThis(), new ClassDoThatInt());

            Console.WriteLine("++ StaticGenericDoThisAndThat ThisAndThat and ThatInt ++");

            StaticGenericDoThisAndThat(new ClassDoThisAndThat(), new ClassDoThatInt());
        }
    }
}
