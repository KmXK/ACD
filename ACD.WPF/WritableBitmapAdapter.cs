using System.Windows;
using System.Windows.Media.Imaging;
using ACD.Infrastructure;
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
            
            var colorData = 0xFF000000;
            colorData |= color.B;
            colorData |= (uint)(color.G << 8);
            colorData |= (uint)(color.R << 16);
            colorData |= 1 << 24;
            
            *(uint*)pBackBuffer = colorData;
        }
        
        _bitmap.AddDirtyRect(new Int32Rect(x, y, 1, 1));
    }
}