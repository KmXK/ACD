using System.Numerics;

namespace ACD.Infrastructure;

public record struct Polygon(List<PolygonVertex> Vertices)
{
    public readonly Vector3 Normal = Vector3.Normalize(
        Vector3.Cross(
            (Vertices[1].Coordinate - Vertices[0].Coordinate).ToVector3(),
            (Vertices[2].Coordinate - Vertices[1].Coordinate).ToVector3()));
}