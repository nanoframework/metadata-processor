//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

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
            Debug.WriteLine($"{nameof(ClassDoThis.Do1)}");
        }

        public void Do2()
        {
            Debug.WriteLine($"{nameof(ClassDoThis.Do2)}");
        }

        public void DoThis1()
        {
            Debug.WriteLine($"{nameof(ClassDoThis.DoThis1)}");
        }

        public void DoThis2()
        {
            Debug.WriteLine($"{nameof(ClassDoThis.DoThis2)}");
        }
    }

    class ClassDoInt : IDo
    {
        public void Do1()
        {
            Debug.WriteLine($"{nameof(ClassDoInt.Do1)}");
        }

        public void Do2()
        {
            Debug.WriteLine($"{nameof(ClassDoInt.Do2)}");
        }
    }

    class ClassDoThatInt : IDo, IDoThat
    {
        public void Do1()
        {
            Debug.WriteLine($"{nameof(ClassDoThatInt.Do1)}");
        }

        public void Do2()
        {
            Debug.WriteLine($"{nameof(ClassDoThatInt.Do2)}");
        }

        public void DoThat1()
        {
            Debug.WriteLine($"{nameof(ClassDoThatInt.DoThat1)}");
        }

        public void DoThat2()
        {
            Debug.WriteLine($"{nameof(ClassDoThatInt.DoThat2)}");
        }

    }

    class ClassDoThisAndThat : ClassDoThis, IDoThat
    {
        public void DoThat1()
        {
            Debug.WriteLine($"{nameof(ClassDoThisAndThat.DoThat1)}");
        }

        public void DoThat2()
        {
            Debug.WriteLine($"{nameof(ClassDoThisAndThat.DoThat2)}");
        }
    }

    public class ClassDoString : IDo
    {
        public void Do1()
        {
            Debug.WriteLine($"{nameof(ClassDoString.Do1)}");
        }

        public void Do2()
        {
            Debug.WriteLine($"{nameof(ClassDoString.Do2)}");
        }
    }

    class GenericClass<T>
    {
        public int NativeField;

        public T GenericField;

        public void InstanceGenericDoOne(T t)
        {
            T v = t;

            Debug.WriteLine($"{nameof(InstanceGenericDoOne)} --> {v}");
        }

        public void InstanceGenericDoTwo<T2>(T p1, T2 p2)
        {
            T v1 = p1;
            T2 v2 = p2;

            Debug.WriteLine($"{nameof(InstanceGenericDoTwo)}<{v2.GetType()}> --> {v1},{v2}");
        }

        public void InstanceGenericDoOneOther<T1>(T1 t)
        {
            T1 v1 = t;

            Debug.WriteLine($"{nameof(InstanceGenericDoOneOther)}<{v1.GetType()}> --> {v1}");
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

            Debug.WriteLine($"{nameof(InstanceGenericDoOne)} --> {v}");
        }

        public void InstanceGenericDoTwo<T2>(T p1, T1 p2, T2 p3)
        {
            T v1 = p1;
            T1 v2 = p2;
            T2 v3 = p3;

            Debug.WriteLine($"{nameof(InstanceGenericDoTwo)}<{v3.GetType()}> --> {v1},{v2},{v3}");
        }

        public void InstanceGenericDoOneOther<T2>(T1 p1, T2 p2)
        {
            T1 v1 = p1;
            T2 v2 = p2;

            Debug.WriteLine($"{nameof(InstanceGenericDoOneOther)}<{v1.GetType()}> --> {v1},{v2}");
        }
    }

    public class GenericClassTests
    {
        private static void StaticGenericDo<T1, T2>(T1 val, T2 val2) where T1 : IDo where T2 : IDo
        {
            Debug.WriteLine($">> {nameof(StaticGenericDo)}<{val.GetType()},{val2.GetType()}>");

            val.Do1();
            val.Do2();
            val2.Do1();
            val2.Do2();
        }

        private static void StaticGenericDoThisAndThat<T1, T2>(T1 val, T2 val2) where T1 : ClassDoThis, IDo where T2 : ClassDoThatInt
        {
            Debug.WriteLine($">> {nameof(StaticGenericDoThisAndThat)}<{val.GetType()},{val2.GetType()}>");

            val.DoThis1();
            val.DoThis2();
            val2.DoThat1();
            val2.DoThat2();
        }

        public GenericClassTests()
        {
            Debug.WriteLine("++++++++++++++++++++");
            Debug.WriteLine("++ Generics Tests ++");
            Debug.WriteLine("++++++++++++++++++++");
            Debug.WriteLine("");


            var other = new ClassDoString();
            other.Do1();
            other.Do2();

            var gc1 = new GenericClass<int>();
            gc1.InstanceGenericDoOne(1);
            gc1.InstanceGenericDoTwo(1, "TWO");
            gc1.InstanceGenericDoOneOther(false);
            gc1.GenericField = 10;

            var agc1 = new AnotherGenericClass<byte, bool>();
            agc1.InstanceGenericDoOne(22);
            agc1.InstanceGenericDoTwo(33, false, "NINE");
            agc1.InstanceGenericDoOneOther(true, 44);
            agc1.GenericField = 11;
            agc1.AnotherGenericField = false;

            var gc2 = new GenericClass<string>();
            gc2.InstanceGenericDoOne("ONE");
            gc2.InstanceGenericDoTwo("ONE", "TWO");
            gc2.InstanceGenericDoOneOther(33.33);
            gc2.GenericField = "TEN";

            StaticGenericDo(new ClassDoInt(), new ClassDoString());

            StaticGenericDo(new ClassDoString(), new ClassDoInt());

            StaticGenericDoThisAndThat(new ClassDoThis(), new ClassDoThatInt());

            StaticGenericDoThisAndThat(new ClassDoThisAndThat(), new ClassDoThatInt());
        }
    }
}
