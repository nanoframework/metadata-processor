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

            if (nanoImageFormat != 0)  // For GIF and JPEG, we do not convert 
            {
                writer.WriteByte(0x01);     // bpp
                writer.WriteByte(nanoImageFormat);
                _bitmap.Save(writer.BaseStream, _bitmap.RawFormat);
            }
            else
            {
                byte bitsPerPixel = 16;
                writer.WriteByte(bitsPerPixel);     // bpp
                writer.WriteByte(nanoImageFormat);

                try
                {
                    Bitmap clone = new Bitmap(_bitmap.Width, _bitmap.Height, PixelFormat.Format16bppRgb565);
                    using (Graphics gr = Graphics.FromImage(clone))
                {
                        gr.DrawImageUnscaled(_bitmap, 0, 0);
                    }

                    Rectangle rect = new Rectangle(0, 0, clone.Width, clone.Height);
                    BitmapData bitmapData = clone.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format16bppRgb565);

                    byte[] data = new byte[clone.Width * clone.Height * 2]; // 2 bytes per pixel

                    System.Runtime.InteropServices.Marshal.Copy(bitmapData.Scan0, data, 0, data.Length);
                    clone.UnlockBits(bitmapData);
                    writer.WriteBytes(data);

                }
                catch
                    {
                    throw new NotSupportedException($"PixelFormat ({_bitmap.PixelFormat.ToString()}) could not be converted to Format16bppRgb565.");
                };
            }
        }

        private byte GetnanoImageFormat(
            ImageFormat rawFormat)
        {
            if (rawFormat.Equals(ImageFormat.Bmp)) // Native == byte c_TypeBitmap = 0;
            {
                return 0;
            }
            if (rawFormat.Equals(ImageFormat.Gif))   // Native == byte c_TypeGif = 1;
            {
                return 1;
            }
            
            else if (rawFormat.Equals(ImageFormat.Jpeg))  // Native == byte c_TypeJpeg = 2;
            {
                return 2;
            }

            return 255;
        }
    }
}
