using System.Diagnostics;

namespace TestNFApp
{
    public class GenericClassTests
    {
        public GenericClassTests()
        {
            Debug.WriteLine("++++++++++++++++++++");
            Debug.WriteLine("++ Generics Tests ++");
            Debug.WriteLine("++++++++++++++++++++");
            Debug.WriteLine("");
            
            var m = new GenericClass<int>();
            m.InstanceGenericDoOne(1);
            m.InstanceGenericDoTwo(1, "OK");

            var m2 = new GenericClass<string>();
            m2.InstanceGenericDoOne("OK");
            m2.InstanceGenericDoTwo("OK", "Now");

            StaticGenericDo(new ClassDoInt(), new ClassDoString());

            StaticGenericDo(new ClassDoString(), new ClassDoInt());

            StaticGenericDoThisAndThat(new ClassDoThis(), new ClassDoThatInt());
        }
        private interface IDo
        {
            void Do1();
            void Do2();
        }

        private interface IDoThat
        {
            void DoThat1();
            void DoThat2();
        }

        private class ClassDoThis : IDo
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

        private static void StaticGenericDo<T1, T2>(T1 val, T2 val2) where T1 : IDo where T2 : IDo
        {
            Debug.WriteLine($">> {nameof(StaticGenericDo)}");
            
            val.Do1();
            val.Do2();
            val2.Do1();
            val2.Do2();
        }
        private static void StaticGenericDoThisAndThat<T1, T2>(T1 val, T2 val2) where T1 : ClassDoThis, IDo where T2 : ClassDoThatInt
        {
            Debug.WriteLine($">> {nameof(StaticGenericDoThisAndThat)}");

            val.DoThis1();
            val.DoThis1();
            val2.DoThat1();
            val2.DoThat2();
        }

        private class ClassDoInt : IDo
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

        private class ClassDoThatInt : IDo, IDoThat
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

        private class ClassDoThisAndInt : ClassDoThis, IDo
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
            public void InstanceGenericDoOne(T t)
            {
                T t2 = t;

                Debug.WriteLine($"{nameof(InstanceGenericDoOne)}-{t2}");
            }

            public void InstanceGenericDoTwo<T2>(T t, T2 t2)
            {
                T _t = t;

                Debug.WriteLine($"{nameof(InstanceGenericDoTwo)}- {_t}-{t2}");
            }

            public void InstanceGenericDoOneThat<T>(T t)
            {
                T t1 = t;

                Debug.WriteLine($"{nameof(InstanceGenericDoOneThat)}-{t1}");
            }
        }
    }
}
