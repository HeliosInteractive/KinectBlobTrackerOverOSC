using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Kinect;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Emgu.CV;
using PixelFormat = System.Windows.Media.PixelFormat;
using Point = System.Windows.Point;

namespace KinectWPFOpenCV
{
    public static class ImageHelpers
    {

        private const int MaxDepthDistance = 4000;
        private const int MinDepthDistance = 850;
        private const int MaxDepthDistanceOffset = 3150;

        public static BitmapSource SliceDepthImage( DepthFrame image, int min = 20, int max = 1000)
        {
            int width = image.FrameDescription.Width;
            int height = image.FrameDescription.Height;

            //var depthFrame = image.Image.Bits;
            ushort[] rawDepthData = new ushort[image.FrameDescription.LengthInPixels];
            image.CopyFrameDataToArray(rawDepthData);

            var pixels = new byte[height * width * 4];

            const int BlueIndex = 0;
            const int GreenIndex = 1;
            const int RedIndex = 2;

            for (int depthIndex = 0, colorIndex = 0;
                depthIndex < rawDepthData.Length && colorIndex < pixels.Length;
                depthIndex++, colorIndex += 4) {

                // Calculate the distance represented by the two depth bytes
                int depth = rawDepthData[depthIndex];

                // Map the distance to an intesity that can be represented in RGB
                var intensity = CalculateIntensityFromDistance(depth);

                if (depth > min && depth < max) {
                    // Apply the intensity to the color channels
                    pixels[colorIndex + BlueIndex] = intensity; //blue
                    pixels[colorIndex + GreenIndex] = intensity; //green
                    pixels[colorIndex + RedIndex] = intensity; //red                    
                }
            }

            return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, pixels, width * 4);
        }

        public static byte CalculateIntensityFromDistance(int distance)
        {
            // This will map a distance value to a 0 - 255 range
            // for the purposes of applying the resulting value
            // to RGB pixels.
            int newMax = distance - MinDepthDistance;
            if (newMax > 0)
                return (byte)(255 - (255 * newMax
                / (MaxDepthDistanceOffset)));
            else
                return (byte)255;
        }


        public static System.Drawing.Bitmap ToBitmap(this BitmapSource bitmapsource)
        {
            System.Drawing.Bitmap bitmap;
            using (var outStream = new MemoryStream()) {
                // from System.Media.BitmapImage to System.Drawing.Bitmap
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapsource));
                enc.Save(outStream);
                bitmap = new System.Drawing.Bitmap(outStream);
                return bitmap;
            }
        }

        public static FormatConvertedBitmap ConvertToBgr32Format(BitmapSource source)
        {
            // Convert to Pbgra32 if it's a different format
            //if (source.Format == PixelFormats.Bgr32) {
            //    return new WriteableBitmap(source);
            //}
            var formattedBitmapSource = new FormatConvertedBitmap();
            formattedBitmapSource.BeginInit();
            formattedBitmapSource.Source = source;
            formattedBitmapSource.DestinationFormat = PixelFormats.Bgr32;
            formattedBitmapSource.EndInit();
            //formattedBitmapSource.CopyPixels();
            return formattedBitmapSource;
            //WriteableBitmap wb = new WriteableBitmap(formatedBitmapSource);
            ////delete

            //formatedBitmapSource = null;
            //GC.Collect();
            //return wb;
        }

        /// <summary>
        /// Convert bmpSource to format declared in pixFormat.
        /// </summary>
        /// <param name="bmpSource"></param>
        /// <param name="pixFormat"></param>
        /// <returns></returns>
        public static FormatConvertedBitmap BitmapToFormat(BitmapSource bmpSource,
                                           PixelFormat pixFormat)
        {
            if (bmpSource == null) { return null; }
            FormatConvertedBitmap fcb = new FormatConvertedBitmap();

            fcb.BeginInit();
            fcb.Source = bmpSource;
            fcb.DestinationFormat = pixFormat;
            fcb.EndInit();
            return fcb;
        }



        public static System.Drawing.Bitmap GetBitmap(this BitmapSource source)
        {
            Bitmap bmp = new Bitmap(
              source.PixelWidth,
              source.PixelHeight,
              System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            BitmapData data = bmp.LockBits(
              new Rectangle(System.Drawing.Point.Empty, bmp.Size),
              ImageLockMode.WriteOnly,
              System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            source.CopyPixels(
                Int32Rect.Empty,
                data.Scan0,
                data.Height * data.Stride,
                data.Stride);
            bmp.UnlockBits(data);
            return bmp;
        }

        [DllImport("gdi32")]
        private static extern int DeleteObject(IntPtr o);

        /// <summary>
        /// Convert an IImage to a WPF BitmapSource. The result can be used in the Set Property of Image.Source
        /// </summary>
        /// <param name="image">The Emgu CV Image</param>
        /// <returns>The equivalent BitmapSource</returns>
        public static BitmapSource ToBitmapSource(IImage image)
        {
            using (System.Drawing.Bitmap source = image.Bitmap) {
                IntPtr ptr = source.GetHbitmap(); //obtain the Hbitmap

                BitmapSource bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    ptr,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

                DeleteObject(ptr); //release the HBitmap
                return bs;
            }
        }



        public static BitmapSource CreateBitmapSourceFromBitmap(IImage image)
        {
            using (System.Drawing.Bitmap bitmap = image.Bitmap) {
                if (bitmap == null)
                    throw new ArgumentNullException("bitmap");

                if (Application.Current.Dispatcher == null)
                    return null; // Is it possible?

                try {
                    using (MemoryStream memoryStream = new MemoryStream()) {
                        // You need to specify the image format to fill the stream. 
                        // I'm assuming it is PNG
                        bitmap.Save(memoryStream, ImageFormat.Png);
                        memoryStream.Seek(0, SeekOrigin.Begin);

                        // Make sure to create the bitmap in the UI thread
                        if (InvokeRequired)
                            return (BitmapSource)Application.Current.Dispatcher.Invoke(
                                new Func<Stream, BitmapSource>(CreateBitmapSourceFromBitmap),
                                DispatcherPriority.Normal,
                                memoryStream);

                        return CreateBitmapSourceFromBitmap(memoryStream);
                    }
                }
                catch (Exception) {
                    return null;
                }
            }
        }

        private static bool InvokeRequired
        {
            get { return Dispatcher.CurrentDispatcher != Application.Current.Dispatcher; }
        }

        private static BitmapSource CreateBitmapSourceFromBitmap(Stream stream)
        {
            BitmapDecoder bitmapDecoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            // This will disconnect the stream from the image completely...
            WriteableBitmap writable = new WriteableBitmap(bitmapDecoder.Frames.Single());
            writable.Freeze();

            return writable;
        }


    }
}
