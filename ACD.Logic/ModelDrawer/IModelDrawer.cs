using ACD.Logic.LineDrawers;
using ACD.Logic.VertexTransformer;

namespace ACD.Logic.ModelDrawer;

public interface IModelDrawer
{
    public void DrawModel(
        ILineDrawer lineDrawer,
        IVertexTransformer vertexTransformer);
}