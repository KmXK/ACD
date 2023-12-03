namespace ACD.Infrastructure;

public class Model
{
    private readonly List<Polygon> _polygons;

    public int MaxPolygonVertices { get; }
    
    public IReadOnlyList<Polygon> Polygons => _polygons;

    public float MinX { get; }
    public float MinY { get; }
    public float MinZ { get; }
    public float MaxX { get; }
    public float MaxY { get; }
    public float MaxZ { get; }

    public Model(IEnumerable<Polygon> polygons)
    {
        _polygons = polygons.ToList();
        MaxPolygonVertices = _polygons.Max(polygon => polygon.Vertices.Count);

        MaxX = _polygons.Max(x => x.Vertices.Max(y => y.Coordinate.X));
        MaxY = _polygons.Max(x => x.Vertices.Max(y => y.Coordinate.Y));
        MaxZ = _polygons.Max(x => x.Vertices.Max(y => y.Coordinate.Z));
        
        MinX = _polygons.Min(x => x.Vertices.Min(y => y.Coordinate.X));
        MinY = _polygons.Min(x => x.Vertices.Min(y => y.Coordinate.Y));
        MinZ = _polygons.Min(x => x.Vertices.Min(y => y.Coordinate.Z));
    }
}