using System;
using System.Drawing;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ACD.WPF.Rasterization;

public class DDALineDrawer
{
    private readonly WriteableBitmap _bitmap;

    public DDALineDrawer(WriteableBitmap bitmap)
    {
        _bitmap = bitmap;
    }
    
    public void DrawLine(float xStart, float yStart, float xEnd, float yEnd)
    {
        try
        {
            _bitmap.Lock();

            var xDelta = xEnd - xStart;
            var yDelta = yEnd - yStart;
            var steps = (int)MathF.Round(MathF.MaxMagnitude(Math.Abs(xDelta), Math.Abs(yDelta)));

            DrawPixel((int)MathF.Round(xStart), (int)MathF.Round(yStart), Color.Black);

            for (var step = 1; step < steps; step++)
            {
                xStart += xDelta / steps;
                yStart += yDelta / steps;
                DrawPixel((int)MathF.Round(xStart), (int)MathF.Round(yStart), Color.Black);
            }
        }
        finally
        {
            _bitmap.Unlock();
        }
    }
    
    private void DrawPixel(int x, int y, Color color)
    {
        if (x < 0 || x >= _bitmap.PixelWidth || y < 0 || y >= _bitmap.PixelHeight)
        {
            return;
        }
        
        unsafe
        {
            var pBackBuffer = _bitmap.BackBuffer;
        
            pBackBuffer += y * _bitmap.BackBufferStride;
            pBackBuffer += x * 4;
        
            var colorData = color.R << 8;
            colorData |= color.G << 16;
            colorData |= color.B << 24;
            colorData |= 255;
            
            *(int*)pBackBuffer = colorData;
        }

        // _bitmap.WritePixels(
        //     new Int32Rect(x, y, 1, 1),
        //     new[] { color.R << 8 | color.G << 16 | color.B << 24 | 0xff },
        //     _bitmap.BackBufferStride,
        //     0);

        _bitmap.AddDirtyRect(new Int32Rect(x, y, 1, 1));
    }
}