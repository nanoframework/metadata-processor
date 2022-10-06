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

        /// <summary>
        /// Bitmap types supported by the native code found in "CLR_GFX_BitmapDescription".
        /// </summary>
        private enum BitmapType : byte
        {
            /// <summary>
            /// Format of nanoFramework bitmap is 16-bit rgb565 format.
            /// </summary>
            nanoCLRBitmap = 0,

            /// <summary>
            /// Format of bitmap is GIF.
            /// </summary>
            Gif = 1,

            /// <summary>
            ///Format of bitmap JPEG. 
            /// </summary>
            Jpeg = 2,

            /// <summary>
            /// Format of bitmap is Windows bitmap. 
            /// </summary>
            /// <remarks>
            /// There is support for compressed bitmaps in the native code, but the conversion of resources to Format16bppRgb565 by the metadata processor eliminates this code being used.
            /// </remarks>
            WindowsBmp = 3,
        }

        public nanoBitmapProcessor(Bitmap bitmap)
        {
            _bitmap = bitmap;
        }

        public void Process(nanoBinaryWriter writer)
        {
            // CLR_GFX_BitmapDescription header as required by the native side
            writer.WriteUInt32((uint)_bitmap.Width);
            writer.WriteUInt32((uint)_bitmap.Height);

            // flags
            writer.WriteUInt16(0x00);  

            var nanoImageFormat = GetnanoImageFormat(_bitmap.RawFormat);

            // For GIF and JPEG, we do not convert
            if (nanoImageFormat == BitmapType.Gif || nanoImageFormat == BitmapType.Jpeg)
            {
                // write value of Bytes Per Pixel (bpp): 1
                writer.WriteByte(0x01);
                // encoded format
                writer.WriteByte((byte)nanoImageFormat);

                // now the data
                _bitmap.Save(writer.BaseStream, _bitmap.RawFormat);
            }
            else
            {
                // write value of Bytes Per Pixel (bpp): 16
                writer.WriteByte(16);
                
                // encoded format
                writer.WriteByte((byte)nanoImageFormat);

                try
                {
                    Bitmap clone = new Bitmap(
                        _bitmap.Width,
                        _bitmap.Height,
                        PixelFormat.Format16bppRgb565);

                    using (Graphics graphicsDrawing = Graphics.FromImage(clone))
                    {
                        graphicsDrawing.DrawImageUnscaled(_bitmap, 0, 0);
                    }

                    Rectangle rect = new Rectangle(
                        0,
                        0,
                        clone.Width,
                        clone.Height);

                    BitmapData bitmapData = clone.LockBits(
                        rect,
                        ImageLockMode.ReadOnly,
                        PixelFormat.Format16bppRgb565);

                    //  Format16bppRgb565 == 2 bytes per pixel
                    byte[] data = new byte[clone.Width * clone.Height * 2];

                    System.Runtime.InteropServices.Marshal.Copy(
                        bitmapData.Scan0,
                        data,
                        0,
                        data.Length);

                    clone.UnlockBits(bitmapData);

                    // now the data
                    writer.WriteBytes(data);
                }
                catch
                {
                    throw new NotSupportedException($"PixelFormat ({_bitmap.PixelFormat}) could not be converted to Format16bppRgb565.");
                }
            }
        }

        private BitmapType GetnanoImageFormat(ImageFormat rawFormat)
        {
            if (rawFormat.Equals(ImageFormat.Gif))
            {
                return BitmapType.Gif;
            }
            else if (rawFormat.Equals(ImageFormat.Jpeg))
            {
                return BitmapType.Jpeg;
            }
            else
            {
                // Any windows bitmap format is marked for conversion to nanoCLRBitmap ( i.e. Format16bppRgb565 ) 
                return BitmapType.nanoCLRBitmap;
            }
        }
    }
}
