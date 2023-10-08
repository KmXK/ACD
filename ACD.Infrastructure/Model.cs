namespace ACD.Infrastructure;

public class Model
{
    private readonly List<Polygon> _polygons;

    public IReadOnlyCollection<Polygon> Polygons => _polygons;

    public Model(IEnumerable<Polygon> polygons)
    {
        _polygons = polygons.ToList();
    }
}