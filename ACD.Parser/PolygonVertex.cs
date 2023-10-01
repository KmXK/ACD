using System.Numerics;

namespace ACD.Parser;

public record struct PolygonVertex(
    Vector4 Coordinate,
    Vector3 Texture,
    Vector3 Normal);