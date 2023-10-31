using System.Numerics;
using ACD.Infrastructure;
using ACD.Logic.LineDrawers;
using ACD.Logic.VertexTransformer;

namespace ACD.Logic.ModelDrawer;

public class ModelDrawer : IModelDrawer
{
    private readonly Model _model;
    private readonly Vector4[] _vertices;

    public ModelDrawer(Model model)
    {
        _model = model;

        _vertices = new Vector4[model.Polygons.Count * model.MaxPolygonVertices];
    }
    
    public void DrawModel(
        ILineDrawer lineDrawer,
        IVertexTransformer vertexTransformer)
    {
        Parallel.ForEach(_model.Polygons, (polygon, _, pi) =>
        {
            for (var vi = 0; vi < polygon.Vertices.Count; vi++)
            {
                var (vertex, _, _) = polygon.Vertices[vi];
                var v = vertexTransformer.Transform(vertex);
                _vertices[pi * _model.MaxPolygonVertices + vi] = v;
            }
        });

        for (var pi = 0; pi < _model.Polygons.Count; pi++)
        {
            Vector4? prevV = null;
            var vi = 0;
            var verticesCount = 3; // _model.Polygons[pi].Vertices.Count;

            var baseIndex = pi * _model.MaxPolygonVertices;

            while (vi < verticesCount)
            {
                var v = _vertices[baseIndex + vi];
                
                if (prevV.HasValue)
                {
                    lineDrawer.DrawLine(v.X, v.Y, prevV.Value.X, prevV.Value.Y);
                }
                
                prevV = v;

                vi++;
            }
            
            lineDrawer.DrawLine(_vertices[baseIndex].X, _vertices[baseIndex].Y, prevV!.Value.X, prevV.Value.Y);
        }
    }
}