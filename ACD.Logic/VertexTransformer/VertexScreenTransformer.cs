using System.Numerics;
using ACD.Infrastructure;
using Microsoft.VisualBasic;

namespace ACD.Logic.VertexTransformer;

public class VertexScreenTransformer : IVertexTransformer
{
    private readonly Camera _camera;
    private readonly ModelTransform _modelTransform;

    public VertexScreenTransformer(
        Camera camera,
        ModelTransform modelTransform)
    {
        _camera = camera;
        _modelTransform = modelTransform;
    }
    
    public Vector4 Transform(Vector4 vertex)
    {
        var v = Vector4.Transform(vertex, _modelTransform.Transformation);
        
        v = Vector4.Transform(v, _camera.View);
        v = Vector4.Transform(v, _camera.Projection);
        
        v /= v.W;
        
        v = Vector4.Transform(v, _camera.ViewPort);
        

        return v;
    }

    public Vector4 ToWorldSpace(Vector4 vertex)
    {
        return Vector4.Transform(vertex, _modelTransform.Transformation);
    }
}