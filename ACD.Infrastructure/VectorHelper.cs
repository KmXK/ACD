using System.Numerics;

namespace ACD.Infrastructure;

public static class VectorHelper
{
    public static Vector3 ToVector3(this Vector4 v)
    {
        return new Vector3(v.X, v.Y, v.Z);
    }
}