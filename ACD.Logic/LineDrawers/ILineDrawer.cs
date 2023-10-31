using System.Drawing;

namespace ACD.Logic.LineDrawers;

public interface ILineDrawer
{
    public void DrawLine(float x1, float y1, float x2, float y2, Color color);
}