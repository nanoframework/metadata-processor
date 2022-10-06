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
        public void ProcessJpegTest()
        {
            ExecuteResourceProcessing("jpeg.jpg", "jpeg_expected_result.bin");
        }

        [TestMethod]
        public void ProcessGifTest()
        {
            ExecuteResourceProcessing("gif.gif", "gif_expected_result.bin");
        }

        [TestMethod]
        public void ProcessBmpTest()
        {
            ExecuteResourceProcessing("bmp.bmp", "bmp_expected_result.bin");
        }

        [TestMethod]
        public void ProcessIcoTest()
        {
            ExecuteResourceProcessing("favicon.ico", "favicon_expected_result.bin");
        }

        private void ExecuteResourceProcessing(string sourceResourceName, string expectedResultResourceName)
        {
            using (var resourceStream = TestObjectHelper.GetResourceStream(sourceResourceName))
            {
                var bitmapResource = new Bitmap(resourceStream);

                var bitmapProcessor = new nanoBitmapProcessor(bitmapResource);

                var bytesWritten = TestObjectHelper.ExecuteWithNanoBinaryWriter((bw) => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw), (ms, bw, writer) =>
                {
                    // test 
                    bitmapProcessor.Process(writer);
                });

                ////////////////////////////////////////////////////////////////////////
                ////////////////////////////////////////////////////////////////////////
                // use this code spinet to writing expected results for new resources //
                //using (FileStream writeStream = File.OpenWrite($"..\\..\\{expectedResultResourceName}"))
                //{
                //    using (BinaryWriter writer = new BinaryWriter(writeStream))
                //    {
                //        writer.Write(bytesWritten);
                //    }
                //}
                ////////////////////////////////////////////////////////////////////////
                ////////////////////////////////////////////////////////////////////////

                var expected = TestObjectHelper.GetResourceStreamContent(expectedResultResourceName);
                CollectionAssert.AreEqual(expected, bytesWritten);
            }
        }
    }
}
