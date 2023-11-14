namespace ACD.Infrastructure.Vectors;

public readonly record struct Vector3Int(int X, int Y, int Z)
{
    public Vector2Int ToVector2Int()
    {
        return new Vector2Int(X, Y);
    }
}