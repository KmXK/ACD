using System;
using System.Drawing;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ACD.WPF.Drawers;

public abstract class DrawerBase
{
    private readonly int _width;
    private readonly int _height;
    protected WriteableBitmap Bitmap { get; }

    protected DrawerBase(WriteableBitmap bitmap)
    {
        Bitmap = bitmap;
        _width = bitmap.PixelWidth;
        _height = bitmap.PixelHeight;
    }

    protected abstract void DrawLineImpl(float x1, float y1, float x2, float y2);
    
    public void DrawLine(float x1, float y1, float x2, float y2)
    {
        if (x1 < 0 || x2 < 0 || x1 > _width || x2 > _width ||
            y1 < 0 || y2 < 0 || y1 > _height || y2 > _height)
        {
            return;
        }
        
        if (x1 > x2)
        {
            // swap
            (x1, y1, x2, y2) = (x2, y2, x1, y1);
        }
        
        var dx = x2 - x1;
        var dy = y2 - y1;
        
        (x1, y1) = ClampStartPoint(x1, y1, dx, dy);

        if (x1 < 0 || y1 < 0)
        {
            return;
        }
        
        DrawLineImpl(x1, y1, x2, y2);
    }
    
    protected void DrawPixel(int x, int y, Color color)
    {
        if (x < 0 || x >= Bitmap.PixelWidth || y < 0 || y >= Bitmap.PixelHeight)
        {
            return;
        }
        
        unsafe
        {
            var pBackBuffer = Bitmap.BackBuffer;
        
            pBackBuffer += y * Bitmap.BackBufferStride;
            pBackBuffer += x * 4;
        
            var colorData = color.B << 8;
            colorData |= color.G << 16;
            colorData |= color.R << 24;
            colorData |= 255;
            
            *(int*)pBackBuffer = colorData;
        }

        Bitmap.AddDirtyRect(new Int32Rect(x, y, 1, 1));
    }
    
    private (float X, float Y) ClampStartPoint(float x, float y, float dx, float dy)
    {
        dx = MathF.Abs(dx);
        
        if (x < 0)
        {
            var g = y + dy / dx * -x;

            if (g >= 0 && g < _height)
            {
                return (0, g);
            }
        }
        
        dy = MathF.Abs(dy);
        
        if (y < 0)
        {
            var b = x + dx / dy * -y;

            if (b >= 0 && b < _width)
            {
                return (b, 0);
            }
        }
        
        if (y > _height)
        {
            var b = x + dx / dy * (y - _height);

            if (b >= 0 && b < _width)
            {
                return (b, _height);
            }
        }

        return (x, y);
    }
}