using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Utility
{
    [TestClass]
    public class nanoStringsConstantsTests
    {
        [TestMethod]
        public void TryGetStringIndexTests()
        {
            ushort index;

            // test
            var r = nanoStringsConstants.TryGetStringIndex("DateTime", out index);

            Assert.IsTrue(r);
            Assert.AreEqual(0xFFFF-0x0058, index);

            // test
            r = nanoStringsConstants.TryGetStringIndex(Guid.NewGuid().ToString(), out index);

            Assert.IsFalse(r);
        }

        [TestMethod]
        public void TryGetStringTest()
        {
            // test
            var r = nanoStringsConstants.TryGetString(0xFFFF - 0x00A2);

            Assert.AreEqual("FromBase64String", r);

            // test
            r = nanoStringsConstants.TryGetString(0);

            Assert.IsNull(r);
        }
    }
}
