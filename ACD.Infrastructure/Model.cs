namespace ACD.Infrastructure;

public class Model
{
    private readonly List<Polygon> _polygons;

    public int MaxPolygonVertices { get; }
    
    public IReadOnlyList<Polygon> Polygons => _polygons;

    public Model(IEnumerable<Polygon> polygons)
    {
        _polygons = polygons.ToList();
        MaxPolygonVertices = _polygons.Max(polygon => polygon.Vertices.Count);
    }
}