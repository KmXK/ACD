using System.Windows;
using System.Windows.Media.Imaging;
using ACD.Infrastructure;
using ACD.Logic.Bitmap;

namespace ACD.WPF;

public sealed class WritableBitmapAdapter : IBitmap
{
    private static Color[,]? _map;
    private static int[,]? _mapZ;
    private readonly WriteableBitmap _bitmap;

    public WritableBitmapAdapter(WriteableBitmap bitmap)
    {
        _bitmap = bitmap;
        Width = bitmap.PixelWidth;
        Height = bitmap.PixelHeight;

        if (_map == null || _map.GetLength(0) != Width || _map.GetLength(1) != Height)
        {
            _map = new Color[Width, Height];
            _mapZ = new int[Width, Height];
        }

        var fillColor = new Color(0, 0, 0, 0);
        
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                _map[x, y] = fillColor;
                _mapZ![x, y] = int.MaxValue;
            }
        }
    }

    public int Width { get; }

    public int Height { get; }

    public void DrawPixel(int x, int y, Color color, int z = 0)
    {
        if (_mapZ![x, y] > z)
        {
            _map![x, y] = color;
            _mapZ[x, y] = z;
        }
    }

    public void DrawBitmap()
    {
        unsafe
        {
            var pBackBuffer = _bitmap.BackBuffer;

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var color = _map![x, y];
                    
                    uint colorData = 0x00000000;
                    colorData |= color.B;
                    colorData |= (uint)color.G << 8;
                    colorData |= (uint)color.R << 16;
                    colorData |= (uint)color.A << 24;
                
                    *(uint*)pBackBuffer = colorData;

                    pBackBuffer += 4;
                }
            }
        }
        
        _bitmap.AddDirtyRect(new Int32Rect(0, 0, Width, Height));
    }
}