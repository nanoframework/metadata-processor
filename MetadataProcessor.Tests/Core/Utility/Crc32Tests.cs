using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Utility
{
    [TestClass]
    public class Crc32Tests
    {
        [TestMethod]
        public void ComputeTest()
        {
            var input = Encoding.ASCII.GetBytes("123456789");

            // test
            var r = nanoFramework.Tools.MetadataProcessor.Crc32.Compute(input, 0xFFFFFFFF);

            // it is a CRC-32/MPEG-2 algorithm
            Assert.AreEqual((uint)0x0376e6e7, r);
        }

        [TestMethod]
        public void ComputePartialTest()
        {
            var input1 = Encoding.ASCII.GetBytes("1234");
            var input2 = Encoding.ASCII.GetBytes("56789");

            // test
            var tmp = nanoFramework.Tools.MetadataProcessor.Crc32.Compute(input1, 0xFFFFFFFF);
            var r = nanoFramework.Tools.MetadataProcessor.Crc32.Compute(input2, tmp);

            // it is a CRC-32/MPEG-2 algorithm
            Assert.AreEqual((uint)0x0376e6e7, r);
        }
    }
}
