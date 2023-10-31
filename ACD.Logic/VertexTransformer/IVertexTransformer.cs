using System.Numerics;

namespace ACD.Logic.VertexTransformer;

public interface IVertexTransformer
{
    Vector4 Transform(Vector4 vertex);
}