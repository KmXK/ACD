using System.Drawing;
using ACD.Logic.Bitmap;

namespace ACD.Logic.LineDrawers;

public abstract class LineDrawerBase : ILineDrawer
{
    protected IBitmap Bitmap { get; }

    protected LineDrawerBase(IBitmap bitmap)
    {
        Bitmap = bitmap;
    }

    protected abstract void DrawLineImpl(float x1, float y1, float x2, float y2, Color color);
    
    public void DrawLine(float x1, float y1, float x2, float y2, Color color)
    {
        if (x1 < 0 || x2 < 0 || x1 > Bitmap.Width || x2 > Bitmap.Width ||
            y1 < 0 || y2 < 0 || y1 > Bitmap.Height || y2 > Bitmap.Height)
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
        
        DrawLineImpl(x1, y1, x2, y2, color);
    }
    
    private (float X, float Y) ClampStartPoint(float x, float y, float dx, float dy)
    {
        dx = MathF.Abs(dx);
        
        if (x < 0)
        {
            var g = y + dy / dx * -x;

            if (g >= 0 && g < Bitmap.Height)
            {
                return (0, g);
            }
        }
        
        dy = MathF.Abs(dy);
        
        if (y < 0)
        {
            var b = x + dx / dy * -y;

            if (b >= 0 && b < Bitmap.Width)
            {
                return (b, 0);
            }
        }
        
        if (y > Bitmap.Height)
        {
            var b = x + dx / dy * (y - Bitmap.Height);

            if (b >= 0 && b < Bitmap.Width)
            {
                return (b, Bitmap.Height);
            }
        }

        return (x, y);
    }
}