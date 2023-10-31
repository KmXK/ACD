using System.Drawing;
using System.Windows;
using System.Windows.Media.Imaging;
using ACD.Logic.Bitmap;

namespace ACD.WPF;

public class WritableBitmapAdapter : IBitmap
{
    private readonly WriteableBitmap _bitmap;

    public WritableBitmapAdapter(WriteableBitmap bitmap)
    {
        _bitmap = bitmap;
    }

    public int Width => _bitmap.PixelWidth;
    
    public int Height => _bitmap.PixelHeight;
    
    public void DrawPixel(int x, int y, Color color)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return;
        }
        
        unsafe
        {
            var pBackBuffer = _bitmap.BackBuffer;
        
            pBackBuffer += y * _bitmap.BackBufferStride;
            pBackBuffer += x * 4;
        
            var colorData = color.B << 8;
            colorData |= color.G << 16;
            colorData |= color.R << 24;
            colorData |= 255;
            
            *(int*)pBackBuffer = colorData;
        }
        
        _bitmap.AddDirtyRect(new Int32Rect(x, y, 1, 1));
    }
}