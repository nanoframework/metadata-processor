//
// Copyright (c) .NET Foundation and Contributors
// Original work from Oleg Rakhmatulin.
// See LICENSE file in the project root for full license information.
//

using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace nanoFramework.Tools.MetadataProcessor
{
    internal sealed class nanoBitmapProcessor
    {
        private readonly Bitmap _bitmap;

        // Bitmap types supported by the native code found in "CLR_GFX_BitmapDescription"
        private enum BitmapType
        {
            // Format of bitmap is 16-bit rgb565 format
            nanoCLRBitmap = 0,
            // Format of bitmap is GIF
            Gif = 1,
            // Format of bitmap JPEG
            Jpeg = 2,
            // Format of bitmap is Windows bitmap
            // NOTE: There is support for compressed bitmaps in the native code, but the conversion of resources to Format16bppRgb565 
            //       by the metadata processor eliminates this code being used.
            WindowsBmp = 3,
            // Not supported or unknown bitmap type
            UnKnown = 255
        }

        public nanoBitmapProcessor(
                Bitmap bitmap)
        {
            _bitmap = bitmap;
        }

        public void Process(
            nanoBinaryWriter writer)
        {
            // CLR_GFX_BitmapDescription header as required by the native side
            writer.WriteUInt32((uint)_bitmap.Width);
            writer.WriteUInt32((uint)_bitmap.Height);

            writer.WriteUInt16(0x00);   // flags

            var nanoImageFormat = GetnanoImageFormat(_bitmap.RawFormat);

            // For GIF and JPEG, we do not convert
            if (nanoImageFormat != 0)
            {
                writer.WriteByte(0x01);
                writer.WriteByte((byte)nanoImageFormat);
                _bitmap.Save(writer.BaseStream, _bitmap.RawFormat);
            }
            else
            {
                byte bitsPerPixel = 16;
                writer.WriteByte(bitsPerPixel);
                writer.WriteByte((byte)nanoImageFormat);

                try
                {
                    Bitmap clone = new Bitmap(_bitmap.Width, _bitmap.Height, PixelFormat.Format16bppRgb565);
                    using (Graphics gr = Graphics.FromImage(clone))
                    {
                        gr.DrawImageUnscaled(_bitmap, 0, 0);
                    }

                    Rectangle rect = new Rectangle(0, 0, clone.Width, clone.Height);
                    BitmapData bitmapData = clone.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format16bppRgb565);

                    //  Format16bppRgb565 == 2 bytes per pixel
                    byte[] data = new byte[clone.Width * clone.Height * 2];

                    System.Runtime.InteropServices.Marshal.Copy(bitmapData.Scan0, data, 0, data.Length);
                    clone.UnlockBits(bitmapData);
                    writer.WriteBytes(data);

                }
                catch
                {
                    throw new NotSupportedException($"PixelFormat ({_bitmap.PixelFormat.ToString()}) could not be converted to Format16bppRgb565.");
                }
            }
        }

        private BitmapType GetnanoImageFormat(
            ImageFormat rawFormat)
        {
            // Any windows bitmap format is marked for conversion to nanoCLRBitmap ( i.e. Format16bppRgb565 ) 
            if (rawFormat.Equals(ImageFormat.Bmp))
            {
                return BitmapType.nanoCLRBitmap;
            }
            else if (rawFormat.Equals(ImageFormat.Gif))
            {
                return BitmapType.Gif;
            }
            else if (rawFormat.Equals(ImageFormat.Jpeg))
            {
                return BitmapType.Jpeg;
            }
            else
            {
                return BitmapType.UnKnown;
            }
        }
    }
}
