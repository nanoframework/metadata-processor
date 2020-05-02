using System;
using System.Text;

namespace TestNFApp
{
    [DummyCustomAttribute1]
    [DummyCustomAttribute2]
    public class AttributeDecoratedClass
    {
        [DummyCustomAttribute1]
        [DummyCustomAttribute2]
        private readonly string dummyField = "dummy";

        [DummyCustomAttribute1]
        [DummyCustomAttribute2]
        public void DummyMethod()
        {
        }

    }

    public class DummyCustomAttribute1 : Attribute
    {
    }

    public class DummyCustomAttribute2 : Attribute
    {
    }
}
