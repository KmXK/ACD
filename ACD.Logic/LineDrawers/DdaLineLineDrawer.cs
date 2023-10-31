using System.Drawing;
using ACD.Logic.Bitmap;

namespace ACD.Logic.LineDrawers;

public class DdaLineLineDrawer : LineDrawerBase
{
    public DdaLineLineDrawer(IBitmap bitmap) : base(bitmap)
    {
    }
    
    protected override void DrawLineImpl(float x1, float y1, float x2, float y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        
        var steps = (int)MathF.Round(MathF.MaxMagnitude(Math.Abs(dx), Math.Abs(dy)));

        Bitmap.DrawPixel((int)MathF.Round(x1), (int)MathF.Round(y1), Color.White);

        for (var step = 1; step < steps; step++)
        {
            x1 += dx / steps;
            y1 += dy / steps;
            Bitmap.DrawPixel((int)MathF.Round(x1), (int)MathF.Round(y1), Color.White); 
        }
    }
}