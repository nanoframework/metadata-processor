// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Extensions
{
    [TestClass]
    public class ByteArrayExtensionsTests
    {
        [TestMethod]
        public void BufferToHexStringTest()
        {
            var bytes = new byte[] { 0x00, 0x01, 0xfe, 0xff };

            // test
            var r = bytes.BufferToHexString();

            Assert.AreEqual("0001FEFF", r);
        }
    }
}
