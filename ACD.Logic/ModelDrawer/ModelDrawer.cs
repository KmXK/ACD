using System.Numerics;
using ACD.Infrastructure;
using ACD.Logic.LineDrawers;
using ACD.Logic.VertexTransformer;

namespace ACD.Logic.ModelDrawer;

public class ModelDrawer : IModelDrawer
{
    private readonly Model _model;

    public ModelDrawer(Model model)
    {
        _model = model;
    }
    
    public void DrawModel(
        ILineDrawer lineDrawer,
        IVertexTransformer vertexTransformer)
    {
        Parallel.ForEach(_model.Polygons, polygon =>
        {
            Vector4? prevV = null;
            Vector4? firstV = null;
            
            foreach (var (vertex, _, _) in polygon.Vertices)
            {
                var v = vertexTransformer.Transform(vertex);

                firstV ??= v;

                if (prevV.HasValue)
                {
                    lineDrawer.DrawLine(v.X, v.Y, prevV.Value.X, prevV.Value.Y);
                }

                prevV = v;
            }
            
            lineDrawer.DrawLine(firstV!.Value.X, firstV.Value.Y, prevV!.Value.X, prevV.Value.Y);
        });
    }
}