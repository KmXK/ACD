using System.Numerics;
using ACD.Infrastructure.Vectors;

namespace ACD.Infrastructure;

public static class VectorHelper
{
    public static Vector3 ToVector3(this Vector4 v)
    {
        return new Vector3(v.X, v.Y, v.Z);
    }
    
    public static Vector2 ToVector2(this Vector3 v)
    {
        return new Vector2(v.X, v.Y);
    }
    
    public static Vector4 ToVector4(this Vector3 v)
    {
        return new Vector4(v.X, v.Y, v.Z, 0);
    }

    public static Vector2 ToVector2(this Vector2Int v)
    {
        return new Vector2(v.X, v.Y);
    }

    public static Vector2Int ToVector2Int(this Vector2 v)
    {
        return new Vector2Int((int)v.X, (int)v.Y);
    }
    
    public static Vector2Int ToVector2Int(this Vector3 v)
    {
        return new Vector2Int((int)v.X, (int)v.Y);
    }
}