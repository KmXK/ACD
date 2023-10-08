using System.Numerics;

namespace ACD.Infrastructure;

public record struct PolygonVertex(
    Vector4 Coordinate,
    Vector3? Texture,
    Vector3? Normal);