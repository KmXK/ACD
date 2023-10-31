using System.Drawing;

namespace ACD.Logic.Bitmap;

public interface IBitmap
{
    int Width { get; }
    
    int Height { get; }
    
    void DrawPixel(int x, int y, Color color);
}