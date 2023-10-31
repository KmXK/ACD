using System.Drawing;
using ACD.Logic.Bitmap;

namespace ACD.Logic.LineDrawers
{
    public class BresenhamLineDrawer : LineDrawerBase
    {
        public BresenhamLineDrawer(IBitmap bitmap) : base(bitmap)
        {
        }
        
        protected override void DrawLineImpl(float x1, float y1, float x2, float y2)
        {
            var reverse = false;
            if (Math.Abs(x2 - x1) < Math.Abs(y2 - y1))
            {
                var buffer = x1;
                x1 = y1;
                y1 = buffer;
                buffer = x2;
                x2 = y2;
                y2 = buffer;
                reverse = true;
            }

            var x = (int)MathF.Round(x1);
            var y = (int)MathF.Round(y1);
            var xEnd = (int)MathF.Round(x2);
            var xDelta = (int)Math.Abs(x2 - x1);
            var yDelta = (int)Math.Abs(y2 - y1);
            var xChange = x2 - x1 > 0 ? 1 : -1;
            var yChange = y2 - y1 > 0 ? 1 : -1;
            var error = 0;

            do
            {
                if (reverse)
                {
                    Bitmap.DrawPixel(y, x, Color.White);
                }
                else
                {
                    Bitmap.DrawPixel(x, y, Color.White);
                }

                x += xChange;
                error += yDelta;
                if (2 * error > xDelta)
                {
                    y += yChange;
                    error -= xDelta;
                }
            } while (x * xChange < xEnd * xChange);
        }
    }
}
