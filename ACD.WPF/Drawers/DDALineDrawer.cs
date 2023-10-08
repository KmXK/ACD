using System;
using System.Drawing;
using System.Windows.Media.Imaging;

namespace ACD.WPF.Drawers;

public class DdaLineDrawer : DrawerBase
{
    private readonly float _width;
    private readonly float _height;

    public DdaLineDrawer(WriteableBitmap bitmap) : base(bitmap)
    {
        _width = Bitmap.PixelWidth;
        _height = Bitmap.PixelHeight;
    }
    
    protected override void DrawLineImpl(float x1, float y1, float x2, float y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        
        var steps = (int)MathF.Round(MathF.MaxMagnitude(Math.Abs(dx), Math.Abs(dy)));

        DrawPixel((int)MathF.Round(x1), (int)MathF.Round(y1), Color.White);

        for (var step = 1; step < steps; step++)
        {
            x1 += dx / steps;
            y1 += dy / steps;
            DrawPixel((int)MathF.Round(x1), (int)MathF.Round(y1), Color.White); 
        }
    }
}