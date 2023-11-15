using System.Numerics;

namespace ACD.Logic.VertexTransformer;

public interface IVertexTransformer
{
    VertexTransform Transform(Vector4 vertex);

    public Vector4 ToWorldSpace(Vector4 modelSpaceVertex);

    public Vector4 ToViewSpace(Vector4 worldSpaceVertex);

    public Vector4 ToClipSpace(Vector4 viewSpaceVertex);

    public Vector4 ToScreenSpace(Vector4 clipSpaceVertex);
}

public readonly record struct VertexTransform(
    Vector4 WorldSpace,
    Vector4 ViewSpace,
    Vector4 ClipSpace,
    Vector4 ScreenSpace);