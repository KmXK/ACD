using System;
using System.Windows.Media.Imaging;
using ACD.Infrastructure;
using ACD.Parser;

namespace ACD.WPF;

public class ImagePixelParser : IImagePixelsParser
{
    private byte[]? _pixelData;
    
    public Color[,] GetImagePixels(string imagePath)
    {
        try
        {
            var bitmapDecoder = BitmapDecoder.Create(
                new Uri(imagePath),
                BitmapCreateOptions.None,
                BitmapCacheOption.Default);

            var bitmapFrame = bitmapDecoder.Frames[0];

            var writeableBitmap = new WriteableBitmap(bitmapFrame);

            var width = writeableBitmap.PixelWidth;
            var height = writeableBitmap.PixelHeight;
            var bytesPerPixel = (writeableBitmap.Format.BitsPerPixel + 7) / 8;
            var stride = bytesPerPixel * width;

            if (_pixelData == null || _pixelData.Length != stride * height)
            {
                _pixelData = new byte[stride * height];
            }

            writeableBitmap.CopyPixels(_pixelData, stride, 0);

            var rgbArray = new Color[width, height];

            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    var index = y * stride + x * bytesPerPixel;
                    var blue = _pixelData[index];
                    var green = bytesPerPixel > 1 ? _pixelData[index + 1] : (byte)0;
                    var red = bytesPerPixel > 2 ? _pixelData[index + 2] : (byte)0;

                    rgbArray[x, y] = new Color(red, green, blue);
                }
            }

            return rgbArray;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw new InvalidOperationException("Invalid image format", e);
        }
    }
}