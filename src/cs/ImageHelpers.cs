using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;


namespace BizDeck {
    public class ImageHelpers {

        public static Image ResizeImage(byte[] buffer, int width, int height) {
            (Image original_image, MemoryStream original_stream) = GetImage(buffer);
            var target_rectangle = new Rectangle(0, 0, width, height);
            var target_image = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            target_image.SetResolution(original_image.HorizontalResolution, original_image.VerticalResolution);

            using (var graphics = Graphics.FromImage(target_image)) {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(original_image, target_rectangle, 0, 0, original_image.Width,
                                                    original_image.Height, GraphicsUnit.Pixel);
            }

            // TODO: I am not sure if every image needs to be rotated, but
            // in my limited experiments, this seems to be the case.
            target_image.RotateFlip(RotateFlipType.Rotate180FlipNone);
            return target_image;
        }

        public static byte[] GetJpegFromImage(Image image) { 
            using var buffer_stream = new MemoryStream();
            image.Save(buffer_stream, ImageFormat.Jpeg);
            return buffer_stream.ToArray();
        }

        // Image.Save(...) fails if the MemoryStream is closed before the Save
        // https://stackoverflow.com/questions/336387/image-save-throws-a-gdi-exception-because-the-memory-stream-is-closed
        public static (Image, MemoryStream) GetImage(byte[] buffer) {
            MemoryStream ms = new(buffer);
            Image image = Image.FromStream(ms);
            return (image, ms);
        }

        public static byte[] GetImageBuffer(Image image) {
            ImageConverter converter = new();
            byte[] buffer = (byte[])converter.ConvertTo(image, typeof(byte[]));
            return buffer;
        }

        public static byte[] GetImageBuffer(Icon icon) {
            using MemoryStream stream = new();
            icon.Save(stream);
            return stream.ToArray();
        }

        public static Bitmap GetFileIcon(string fileName, int width, int height, SIIGBF options) {
            IntPtr hBitmap = GetBitmapPointer(fileName, width, height, options);
            try {
                return GetBitmapFromHBitmap(hBitmap);
            }
            finally {
                Win32.DeleteObject(hBitmap);
            }
        }

        private static Bitmap GetBitmapFromHBitmap(IntPtr nativeHBitmap) {
            Bitmap bitmap = Image.FromHbitmap(nativeHBitmap);
            if (Image.GetPixelFormatSize(bitmap.PixelFormat) < 32) {
                return bitmap;
            }
            return CreateAlphaBitmap(bitmap, PixelFormat.Format32bppArgb);
        }

        // Refer to Stack Overflow answer: https://stackoverflow.com/a/21752100
        // and https://stackoverflow.com/a/42178963
        private static Bitmap CreateAlphaBitmap(Bitmap sourceBitmap, PixelFormat targetPixelFormat) {
            Bitmap outputBitmap = new(sourceBitmap.Width, sourceBitmap.Height, targetPixelFormat);
            Rectangle boundary = new(0, 0, sourceBitmap.Width, sourceBitmap.Height);
            BitmapData sourceBitmapData = sourceBitmap.LockBits(boundary, ImageLockMode.ReadOnly, sourceBitmap.PixelFormat);

            try {
                for (int i = 0; i <= sourceBitmapData.Height - 1; i++) {
                    for (int j = 0; j <= sourceBitmapData.Width - 1; j++) {
                        Color pixelColor = Color.FromArgb(Marshal.ReadInt32(sourceBitmapData.Scan0,
                                                            (sourceBitmapData.Stride * i) + (4 * j)));
                        outputBitmap.SetPixel(j, i, pixelColor);
                    }
                }
            }
            finally {
                sourceBitmap.UnlockBits(sourceBitmapData);
            }
            return outputBitmap;
        }

        private static IntPtr GetBitmapPointer(string fileName, int width, int height, SIIGBF options) {
            Guid itemIdentifier = new(Win32.IID_IShellItem2);
            int returnCode = Win32.SHCreateItemFromParsingName(fileName, IntPtr.Zero, ref itemIdentifier,
                                                                            out IShellItem nativeShellItem);
            if (returnCode != 0) {
                throw Marshal.GetExceptionForHR(returnCode);
            }

            SIZE nativeSize = default;
            nativeSize.Width = width;
            nativeSize.Height = height;
            HResult hr = ((IShellItemImageFactory)nativeShellItem).GetImage(nativeSize, options, out IntPtr hBitmap);
            Marshal.ReleaseComObject(nativeShellItem);
            if (hr == HResult.S_OK) {
                return hBitmap;
            }
            throw Marshal.GetExceptionForHR((int)hr);
        }

        // Adapted from this snippet: https://stackoverflow.com/a/2070493/303696
        internal static Image GenerateTestImageFromText(string text, Font font, Color textColor, Color backgroundColor) {
            Image img = new Bitmap(1, 1);
            Graphics drawing = Graphics.FromImage(img);
            SizeF textSize = drawing.MeasureString(text, font);
            img.Dispose();
            drawing.Dispose();
            img = new Bitmap((int)textSize.Width, (int)textSize.Height);
            drawing = Graphics.FromImage(img);
            drawing.Clear(backgroundColor);
            Brush textBrush = new SolidBrush(textColor);
            drawing.TextRenderingHint = TextRenderingHint.AntiAlias;
            drawing.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            drawing.DrawString(text, font, textBrush, 0, 0);
            drawing.Save();

            textBrush.Dispose();
            drawing.Dispose();

            return img;
        }
    }
}
