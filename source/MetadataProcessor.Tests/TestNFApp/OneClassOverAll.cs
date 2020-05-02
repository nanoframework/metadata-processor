using System;
using System.Text;

namespace TestNFApp
{
    [DummyCustomAttribute1]
    [DummyCustomAttribute2]
    public class OneClassOverAll : IOneClassOverAll
    {
        [DummyCustomAttribute1]
        [DummyCustomAttribute2]
        public int DummyProperty { get; set; }

        [DummyCustomAttribute1]
        [DummyCustomAttribute2]
        private string dummyField = "dummy";

        [DummyCustomAttribute1]
        [DummyCustomAttribute2]
        public void DummyMethod()
        {
            dummyField = "just keeping compiler happy";
        }

    }

}
