using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
