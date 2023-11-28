using System.Numerics;

namespace ACD.Infrastructure.Vectors;

public readonly record struct Vector3Int(int X, int Y, int Z)
{
    public Vector2Int ToVector2Int()
    {
        return new Vector2Int(X, Y);
    }

    public Vector3 ToVector3()
    {
        return new Vector3(X, Y, Z);
    }
}