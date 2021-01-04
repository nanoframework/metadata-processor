using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Endianness
{
    [TestClass]
    public class nanoBinaryWriterTests
    {
        [TestMethod]
        public void LittleEndianBinaryWriterMetadataTokenTest()
        {
            DoBinaryWriterMetadataTokenTest(bw => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw));
        }

        [TestMethod]
        public void BigEndianBinaryWriterMetadataTokenTest()
        {
            DoBinaryWriterMetadataTokenTest(bw => nanoBinaryWriter.CreateBigEndianBinaryWriter(bw));
        }

        private void DoBinaryWriterMetadataTokenTest(Func<BinaryWriter, nanoBinaryWriter> writerCreatorFunc)
        {
            DoWriterWriteTest(writerCreatorFunc,
            (iut) =>
            {
                // test
                iut.WriteMetadataToken(0x0001); // <= 0x7f -> 0x01
                iut.WriteMetadataToken(0x0100); // <= 0x3fff -> 0x81, 0x00
                iut.WriteMetadataToken(0x9876); // > 0x3ffff -> 0xc0, 0x00, 0x98, 0x76
            },
            new byte[] { 0x01, 0x81, 0x00, 0xc0, 0x00, 0x98, 0x76 });
        }

        [TestMethod]
        public void LittleEndianBinaryWriterSByteTest()
        {
            DoBinaryWriterSByteTest(bw => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw));
        }

        [TestMethod]
        public void BigEndianBinaryWriterSByteTest()
        {
            DoBinaryWriterSByteTest(bw => nanoBinaryWriter.CreateBigEndianBinaryWriter(bw));
        }

        private void DoBinaryWriterSByteTest(Func<BinaryWriter, nanoBinaryWriter> writerCreatorFunc)
        {
            DoWriterWriteTest(writerCreatorFunc,
            (iut) =>
            {
                // test
                iut.WriteSByte(100);
                iut.WriteSByte(-100);
            },
            new byte[] { 0x64, 0x9c });
        }

        [TestMethod]
        public void LittleEndianBinaryWriterStringTest()
        {
            DoBinaryWriterStringTest(bw => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw));
        }

        [TestMethod]
        public void BigEndianBinaryWriterStringTest()
        {
            DoBinaryWriterStringTest(bw => nanoBinaryWriter.CreateBigEndianBinaryWriter(bw));
        }

        private void DoBinaryWriterStringTest(Func<BinaryWriter, nanoBinaryWriter> writerCreatorFunc)
        {
            var str = "Árvíztűrőtükörfúrógép";
            var expected = Encoding.UTF8.GetBytes(str).Concat(new byte[] { 0x00 }).ToArray();

            DoWriterWriteTest(writerCreatorFunc,
            (iut) =>
            {
                // test
                iut.WriteString(str);
            },
            expected);
        }


        [TestMethod]
        public void LittleEndianBinaryWriterVersionTest()
        {
            DoBinaryWriterVersionTest(bw => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw), new byte[] { 0x01, 0x00, 0x02, 0x00, 0x03, 0x00, 0x04, 0x00 });
        }

        [TestMethod]
        public void BigEndianBinaryWriterVersionTest()
        {
            DoBinaryWriterVersionTest(bw => nanoBinaryWriter.CreateBigEndianBinaryWriter(bw), new byte[] { 0x00, 0x01, 0x00, 0x02, 0x00, 0x03, 0x00, 0x04 });
        }

        private void DoBinaryWriterVersionTest(Func<BinaryWriter, nanoBinaryWriter> writerCreatorFunc, byte[] expected)
        {
            DoWriterWriteTest(writerCreatorFunc,
            (iut) =>
            {
                // test
                iut.WriteVersion(new Version(1,2,3,4));
            },
            expected);
        }


        [TestMethod]
        public void LittleEndianBinaryWriterBytesTest()
        {
            DoBinaryWriterBytesTest(bw => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw));
        }

        [TestMethod]
        public void BigEndianBinaryWriterBytesTest()
        {
            DoBinaryWriterBytesTest(bw => nanoBinaryWriter.CreateBigEndianBinaryWriter(bw));
        }

        private void DoBinaryWriterBytesTest(Func<BinaryWriter, nanoBinaryWriter> writerCreatorFunc)
        {
            var expected = new byte[] { 0x00, 0x01, 0xfe, 0xff };

            DoWriterWriteTest(writerCreatorFunc,
            (iut) =>
            {
                // test
                iut.WriteBytes(expected);
            },
            expected);
        }


        [TestMethod]
        public void LittleEndianBinaryWriterByteTest()
        {
            DoBinaryWriterByteTest(bw => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw));
        }

        [TestMethod]
        public void BigEndianBinaryWriterByteTest()
        {
            DoBinaryWriterByteTest(bw => nanoBinaryWriter.CreateBigEndianBinaryWriter(bw));
        }

        private void DoBinaryWriterByteTest(Func<BinaryWriter, nanoBinaryWriter> writerCreatorFunc)
        {
            DoWriterWriteTest(writerCreatorFunc,
            (iut) =>
            {
                // test
                iut.WriteByte(0xfe);
                iut.WriteByte(0x01);
            },
            new byte[] { 0xfe, 0x01 });
        }




        [TestMethod]
        public void LittleEndianBinaryWriterIntTest()
        {
            DoWriterWriteTest(bw => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw),
            (iut) =>
            {
                // test
                iut.WriteInt16(0x0102);
                iut.WriteInt32(0x03040506);
                iut.WriteInt64(0x0708091011121314);
            },
            new byte[] { 0x02, 0x01, 0x06, 0x05, 0x04, 0x03, 0x14, 0x13, 0x012, 0x011, 0x10, 0x09, 0x08, 0x07 });
        }

        [TestMethod]
        public void LittleEndianBinaryWriterUIntTest()
        {
            DoWriterWriteTest(bw => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw),
            (iut) =>
            {
                // test
                iut.WriteUInt16(0x0102);
                iut.WriteUInt32(0x03040506);
                iut.WriteUInt64(0x0708091011121314);
            },
            new byte[] { 0x02, 0x01, 0x06, 0x05, 0x04, 0x03, 0x14, 0x13, 0x012, 0x011, 0x10, 0x09, 0x08, 0x07 });
        }

        [TestMethod]
        public void LittleEndianBinaryWriterSingleTest()
        {
            var v = (float)123;

            DoWriterWriteTest(bw => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw),
            (iut) =>
            {
                // test
                iut.WriteSingle(v);
            },
            BitConverter.GetBytes(v));
        }

        [TestMethod]
        public void LittleEndianBinaryWriterDoubleTest()
        {
            var v = (double)123;

            DoWriterWriteTest(bw => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw),
            (iut) =>
            {
                // test
                iut.WriteDouble(v);
            },
            BitConverter.GetBytes(v));
        }


        [TestMethod]
        public void BigEndianBinaryWriterIntTest()
        {
            DoWriterWriteTest(bw => nanoBinaryWriter.CreateBigEndianBinaryWriter(bw),
            (iut) =>
            {
                // test
                iut.WriteInt16(0x0102);
                iut.WriteInt32(0x03040506);
                iut.WriteInt64(0x0708091011121314);
            },
            new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x10, 0x11, 0x12, 0x13, 0x14 });
        }

        [TestMethod]
        public void BigEndianBinaryWriterUIntTest()
        {
            DoWriterWriteTest(bw => nanoBinaryWriter.CreateBigEndianBinaryWriter(bw),
            (iut) =>
            {
                // test
                iut.WriteUInt16(0x0102);
                iut.WriteUInt32(0x03040506);
                iut.WriteUInt64(0x0708091011121314);
            },
            new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x10, 0x11, 0x12, 0x13, 0x14 });
        }

        [TestMethod]
        public void BigEndianBinaryWriterSingleTest()
        {
            var v = (float)123;

            DoWriterWriteTest(bw => nanoBinaryWriter.CreateBigEndianBinaryWriter(bw),
            (iut) =>
            {
                // test
                iut.WriteSingle(v);
            },
            BitConverter.GetBytes(v).Reverse().ToArray());
        }

        [TestMethod]
        public void BigEndianBinaryWriterDoubleTest()
        {
            var v = (double)123;

            DoWriterWriteTest(bw => nanoBinaryWriter.CreateBigEndianBinaryWriter(bw),
            (iut) =>
            {
                // test
                iut.WriteDouble(v);
            },
            BitConverter.GetBytes(v).Reverse().ToArray());
        }



        private void DoWriterWriteTest(Func<BinaryWriter, nanoBinaryWriter> writerCreatorFunc, Action<nanoBinaryWriter> writerAction, byte[] expectedBytesWritten)
        {
            var bytesWritten = TestObjectHelper.DoWithNanoBinaryWriter(writerCreatorFunc, (ms, bw, writer) => writerAction(writer));
            CollectionAssert.AreEqual(expectedBytesWritten, bytesWritten, BitConverter.ToString(bytesWritten));
        }

        [TestMethod]
        public void LittleEndianBinaryWriterGetMemoryBasedCloneTest()
        {
            DoWriterGetMemoryBasedCloneTest(bw => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw));
        }

        [TestMethod]
        public void BigEndianBinaryWriterGetMemoryBasedCloneTest()
        {
            DoWriterGetMemoryBasedCloneTest(bw => nanoBinaryWriter.CreateBigEndianBinaryWriter(bw));
        }


        private void DoWriterGetMemoryBasedCloneTest(Func<BinaryWriter, nanoBinaryWriter> writerCreatorFunc)
        {
            TestObjectHelper.DoWithNanoBinaryWriter(writerCreatorFunc, (ms, bw, iut) =>
            {
                using (var ms2 = new MemoryStream())
                {
                    // test
                    var r = iut.GetMemoryBasedClone(ms2);

                    Assert.IsNotNull(r);
                    Assert.AreNotSame(iut, r);
                    Assert.AreSame(ms2, r.BaseStream);
                }
            });
        }

        [TestMethod]
        public void LittleEndianBinaryWriterBaseStreamTest()
        {
            DoWriterBaseStreamTest(bw => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw));
        }

        [TestMethod]
        public void BigEndianBinaryWriterBaseStreamTest()
        {
            DoWriterBaseStreamTest(bw => nanoBinaryWriter.CreateBigEndianBinaryWriter(bw));
        }


        private void DoWriterBaseStreamTest(Func<BinaryWriter, nanoBinaryWriter> writerCreatorFunc)
        {
            TestObjectHelper.DoWithNanoBinaryWriter(writerCreatorFunc, (ms, bw, iut) =>
            {
                // test
                Assert.AreSame(ms, iut.BaseStream);
            });
        }


        [TestMethod]
        public void LittleEndianBinaryWriterIsBigEndianTest()
        {
            DoWriterIsBigEndianTest(bw => nanoBinaryWriter.CreateLittleEndianBinaryWriter(bw), false);
        }

        [TestMethod]
        public void BigEndianBinaryWriterIsBigEndianTest()
        {
            DoWriterIsBigEndianTest(bw => nanoBinaryWriter.CreateBigEndianBinaryWriter(bw), true);
        }


        private void DoWriterIsBigEndianTest(Func<BinaryWriter, nanoBinaryWriter> writerCreatorFunc, bool expectedIsBigEndianValue)
        {
            TestObjectHelper.DoWithNanoBinaryWriter(writerCreatorFunc, (ms, bw, iut) =>
                {
                    // test
                    Assert.AreEqual(expectedIsBigEndianValue, iut.IsBigEndian);
                });
        }

    }
}
