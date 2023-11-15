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
    
    public VertexTransform Transform(Vector4 vertex)
    {
        var worldSpace = ToWorldSpace(vertex);

        var viewSpace = ToViewSpace(worldSpace);
        var clipSpace = ToClipSpace(viewSpace);

        var screenSpace = clipSpace;
        
        screenSpace /= screenSpace.W;

        screenSpace = ToScreenSpace(screenSpace);

        return new VertexTransform(worldSpace, viewSpace, clipSpace, screenSpace);
    }

    public Vector4 ToWorldSpace(Vector4 modelSpaceVertex)
    {
        return Vector4.Transform(modelSpaceVertex, _modelTransform.Transformation);
    }

    public Vector4 ToViewSpace(Vector4 worldSpaceVertex)
    {
        return Vector4.Transform(worldSpaceVertex, _camera.View);
    }
    
    public Vector4 ToClipSpace(Vector4 viewSpaceVertex)
    {
        return Vector4.Transform(viewSpaceVertex, _camera.Projection);
    }
    
    public Vector4 ToScreenSpace(Vector4 clipSpaceVertex)
    {
        return Vector4.Transform(clipSpaceVertex, _camera.ViewPort);
    }
}