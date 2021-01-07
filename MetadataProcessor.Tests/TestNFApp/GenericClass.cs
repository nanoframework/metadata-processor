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
            m.InstanceGenericDo(1);
            m.InstanceGenericDo2(1, "OK");

            var m2 = new GenericClass<string>();
            m2.InstanceGenericDo("OK");
            m2.InstanceGenericDo2("OK", "Now");

            StaticGenericDo(new IntDo(), new StringDo());

            StaticGenericDo(new StringDo(), new IntDo());
        }
        public interface TInt
        {
            void Do1();
            void Do2();
        }

        public static void StaticGenericDo<T1, T2>(T1 val, T2 val2) where T1 : TInt where T2 : TInt
        {
            Debug.WriteLine($">> {nameof(StaticGenericDo)}");
            
            val.Do1();
            val.Do2();
            val2.Do1();
            val2.Do2();
        }

        public class IntDo : TInt
        {
            public void Do1()
            {
                Debug.WriteLine($"{nameof(IntDo.Do1)}");
            }

            public void Do2()
            {
                Debug.WriteLine($"{nameof(IntDo.Do2)}");
            }
        }

        public class StringDo : TInt
        {
            public void Do1()
            {
                Debug.WriteLine($"{nameof(StringDo.Do1)}");
            }

            public void Do2()
            {
                Debug.WriteLine($"{nameof(StringDo.Do2)}");
            }
        }

        class GenericClass<T>
        {
            public void InstanceGenericDo(T t)
            {
                T t2 = t;

                Debug.WriteLine($"{nameof(InstanceGenericDo)}-{t2}");
            }

            public void InstanceGenericDo2<T2>(T t, T2 t2)
            {
                T _t = t;

                Debug.WriteLine($"{nameof(InstanceGenericDo2)}- {_t}-{t2}");
            }
        }
    }
}
