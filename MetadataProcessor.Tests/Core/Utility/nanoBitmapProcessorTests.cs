//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Utility
{
    [TestClass]
    public class nanoBitmapProcessorTests
    {
        [TestMethod]
        public void ProcessBmpTest()
        {
            DoProcessTest("bmp.bmp", "bmp_expected_result.bin");
        }

        [TestMethod]
        public void ProcessJpegTest()
        {
            DoProcessTest("jpeg.jpg", "jpeg_expected_result.bin");
        }

        [TestMethod]
        public void ProcessGifTest()
        {
            DoProcessTest("gif.gif", "gif_expected_result.bin");
        }


        private void DoProcessTest(string sourceResourceName, string expectedResultResourceName)
        {
            using (var resourceStream = TestObjectHelper.GetResourceStream(sourceResourceName))
            {
                var bmp = new Bitmap(resourceStream);

                var iut = new nanoBitmapProcessor(bmp);

                var bytesWritten = TestObjectHelper.DoWithNanoBinaryWriter((bw) => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw), (ms, bw, writer) =>
                {
                    // test 
                    iut.Process(writer);
                });

                var expected = TestObjectHelper.GetResourceStreamContent(expectedResultResourceName);
                CollectionAssert.AreEqual(expected, bytesWritten);


            }

        }
    }
}
