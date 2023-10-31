using System.Numerics;
using ACD.Logic.Bitmap;
using ACD.Logic.VertexTransformer;

namespace ACD.Logic.ModelDrawer;

public interface IRenderer
{
    public void DrawModel(
        IBitmap bitmap,
        IVertexTransformer vertexTransformer,
        Vector3 cameraPosition);
}