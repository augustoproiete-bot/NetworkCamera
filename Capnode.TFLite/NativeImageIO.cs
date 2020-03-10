﻿/*
 * Copyright 2020 Capnode AB
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Drawing;

namespace Capnode.TFLite
{
    /// <summary>
    /// Platform specific implementation of Image IO
    /// </summary>
    public static class NativeImageIO
    {
        /// <summary>
        /// Read an image file, covert the data and save it to the native pointer
        /// </summary>
        /// <typeparam name="T">The type of the data to covert the image pixel values to. e.g. "float" or "byte"</typeparam>
        /// <param name="fileName">The name of the image file</param>
        /// <param name="dest">The native pointer where the image pixels values will be saved to.</param>
        /// <param name="inputHeight">The height of the image, must match the height requirement for the tensor</param>
        /// <param name="inputWidth">The width of the image, must match the width requirement for the tensor</param>
        /// <param name="inputMean">The mean value, it will be subtracted from the input image pixel values</param>
        /// <param name="scale">The scale, after mean is subtracted, the scale will be used to multiply the pixel values</param>
        /// <param name="flipUpSideDown">If true, the image needs to be flipped up side down</param>
        /// <param name="swapBR">If true, will flip the Blue channel with the Red. e.g. If false, the tensor's color channel order will be RGB. If true, the tensor's color channle order will be BGR </param>
        public static void ReadImageFileToTensor<T>(
            String fileName,
            IntPtr dest,
            int inputHeight = -1,
            int inputWidth = -1,
            float inputMean = 0.0f,
            float scale = 1.0f,
            bool flipUpSideDown = false,
            bool swapBR = false)
            where T : struct
        {
            if (!File.Exists(fileName))
                throw new FileNotFoundException(String.Format("File {0} do not exist.", fileName));

            //Read the file using Bitmap class
            Bitmap bmp = new Bitmap(fileName);
            BitmapToTensor<T>(bmp, dest, inputHeight, inputWidth, inputMean, scale, flipUpSideDown, swapBR);
        }

        /// <summary>
        /// Read an image file, covert the data and save it to the native pointer
        /// </summary>
        /// <typeparam name="T">The type of the data to covert the image pixel values to. e.g. "float" or "byte"</typeparam>
        /// <param name="bmp">The image bitmap</param>
        /// <param name="dest">The native pointer where the image pixels values will be saved to.</param>
        /// <param name="inputHeight">The height of the image, must match the height requirement for the tensor</param>
        /// <param name="inputWidth">The width of the image, must match the width requirement for the tensor</param>
        /// <param name="inputMean">The mean value, it will be subtracted from the input image pixel values</param>
        /// <param name="scale">The scale, after mean is subtracted, the scale will be used to multiply the pixel values</param>
        /// <param name="flipUpSideDown">If true, the image needs to be flipped up side down</param>
        /// <param name="swapBR">If true, will flip the Blue channel with the Red. e.g. If false, the tensor's color channel order will be RGB. If true, the tensor's color channle order will be BGR </param>
        public static void BitmapToTensor<T>(
            Bitmap bmp,
            IntPtr dest,
            int inputHeight = -1,
            int inputWidth = -1,
            float inputMean = 0.0f,
            float scale = 1.0f,
            bool flipUpSideDown = false,
            bool swapBR = false)
            where T : struct
        {
            if (inputHeight > 0 || inputWidth > 0)
            {
                //resize bmp
                Bitmap newBmp = new Bitmap(bmp, inputWidth, inputHeight);
                bmp.Dispose();
                bmp = newBmp;
                //bmp.Save("tmp.png");
            }

            if (flipUpSideDown)
            {
                bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
            }

            int bmpWidth = bmp.Width;
            int bmpHeight = bmp.Height;
            System.Drawing.Imaging.BitmapData bd = new System.Drawing.Imaging.BitmapData();
            bmp.LockBits(
                new Rectangle(0, 0, bmpWidth, bmpHeight),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format24bppRgb, bd);
            int stride = bd.Stride;

            byte[] byteValues = new byte[bmpHeight * stride];
            Marshal.Copy(bd.Scan0, byteValues, 0, byteValues.Length);
            bmp.UnlockBits(bd);

            if (typeof(T) == typeof(float))
            {
                int imageSize = bmpWidth * bmpHeight;
                float[] floatValues = new float[imageSize * 3];
                if (swapBR)
                {
                    int idx = 0;
                    int rowOffset = 0;
                    for (int i = 0; i < bmpHeight; ++i)
                    {
                        int rowPtr = rowOffset;
                        for (int j = 0; j < bmpWidth; ++j)
                        {
                            float b = ((float)byteValues[rowPtr++] - inputMean) * scale;
                            float g = ((float)byteValues[rowPtr++] - inputMean) * scale;
                            float r = ((float)byteValues[rowPtr++] - inputMean) * scale;
                            floatValues[idx++] = r;
                            floatValues[idx++] = g;
                            floatValues[idx++] = b;
                        }
                        rowOffset += stride;
                    }
                }
                else
                {
                    int idx = 0;
                    int rowOffset = 0;
                    for (int i = 0; i < bmpHeight; ++i)
                    {
                        int rowPtr = rowOffset;
                        for (int j = 0; j < bmpWidth; ++j)
                        {
                            floatValues[idx++] = ((float)byteValues[rowPtr++] - inputMean) * scale;
                            floatValues[idx++] = ((float)byteValues[rowPtr++] - inputMean) * scale;
                            floatValues[idx++] = ((float)byteValues[rowPtr++] - inputMean) * scale;
                        }
                        rowOffset += stride;
                    }
                }
                Marshal.Copy(floatValues, 0, dest, floatValues.Length);
            }
            else if (typeof(T) == typeof(byte))
            {
                int imageSize = bmp.Width * bmp.Height;
                if (swapBR)
                {
                    int idx = 0;
                    for (int i = 0; i < bmpHeight; ++i)
                    {
                        int offset = i * stride;
                        for (int j = 0; j < bmpWidth; ++j)
                        {
                            byte b = (byte)(((float)byteValues[offset++] - inputMean) * scale);
                            byte g = (byte)(((float)byteValues[offset++] - inputMean) * scale);
                            byte r = (byte)(((float)byteValues[offset++] - inputMean) * scale);
                            byteValues[idx++] = r;
                            byteValues[idx++] = g;
                            byteValues[idx++] = b;
                        }
                    }
                }
                else
                {
                    int idx = 0;
                    for (int i = 0; i < bmpHeight; ++i)
                    {
                        int offset = i * stride;
                        for (int j = 0; j < bmpWidth * 3; ++j)
                        {
                            byteValues[idx++] = (byte)(((float)byteValues[offset++] - inputMean) * scale);
                        }
                    }
                }
                Marshal.Copy(byteValues, 0, dest, imageSize * 3);

            }
            else
            {
                throw new NotImplementedException(String.Format("Destination data type {0} is not supported.", typeof(T).ToString()));
            }
        }

        /// <summary>
        /// Converting raw pixel data to jpeg stream
        /// </summary>
        /// <param name="rawPixel">The raw pixel data</param>
        /// <param name="width">The width of the image</param>
        /// <param name="height">The height of the image</param>
        /// <param name="channels">The number of channels</param>
        /// <returns>The jpeg stream</returns>
        public static byte[] PixelToJpeg(byte[] rawPixel, int width, int height, int channels)
        {
            throw new NotImplementedException("PixelToJpeg Not Implemented in this platform");
        }

        /// <summary>
        /// Read the file and draw rectangles on it.
        /// </summary>
        /// <param name="fileName">The name of the file.</param>
        /// <param name="annotations">Annotations to be add to the image. Can consist of rectangles and labels</param>
        /// <returns>The image in Jpeg stream format</returns>
        public static JpegData ImageFileToJpeg(String fileName, Annotation[] annotations = null)
        {
            Bitmap img = new Bitmap(fileName);

            if (annotations != null)
            {
                using (Graphics g = Graphics.FromImage(img))
                {
                    for (int i = 0; i < annotations.Length; i++)
                    {
                        if (annotations[i].Rectangle != null)
                        {
                            float[] rects = ScaleLocation(annotations[i].Rectangle, img.Width, img.Height);
                            PointF origin = new PointF(rects[0], rects[1]);
                            RectangleF rect = new RectangleF(origin,
                                new SizeF(rects[2] - rects[0], rects[3] - rects[1]));
                            Pen redPen = new Pen(Color.Red, 3);
                            g.DrawRectangle(redPen, Rectangle.Round(rect));

                            String label = annotations[i].Label;
                            if (label != null)
                            {
                                g.DrawString(label, new Font(FontFamily.GenericSansSerif, 20f), Brushes.Red, origin);
                            }
                        }
                    }

                    g.Save();
                }
            }

            using (MemoryStream ms = new MemoryStream())
            {
                img.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                JpegData result = new JpegData
                {
                    Raw = ms.ToArray(),
                    Width = img.Size.Width,
                    Height = img.Size.Height
                };
                return result;
            }
        }

        private static float[] ScaleLocation(float[] location, int imageWidth, int imageHeight)
        {
            float left = location[0] * imageWidth;
            float top = location[1] * imageHeight;
            float right = location[2] * imageWidth;
            float bottom = location[3] * imageHeight;
            return new float[] { left, top, right, bottom };
        }
    }
}