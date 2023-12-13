using System.Collections;
using System.Numerics;

namespace ACD.Infrastructure;

public record struct Polygon(List<PolygonVertex> Vertices, MtlMaterial? Material)
{
    public readonly Vector3 Normal = Vector3.Normalize(
        Vector3.Cross(
            (Vertices[1].Coordinate - Vertices[0].Coordinate).ToVector3(),
            (Vertices[2].Coordinate - Vertices[1].Coordinate).ToVector3()));

    public Enumerator GetEnumerator() => new(this);

    public struct Enumerator(Polygon polygon) : IEnumerator<(int Index1, int Index2, int Index3)>
    {
        private int _lastVertexIndex = 1;
        
        public bool MoveNext()
        {
            _lastVertexIndex++;
            
            Current = (0, _lastVertexIndex - 1, _lastVertexIndex);

            return _lastVertexIndex < polygon.Vertices.Count;
        }

        public void Reset()
        {
            _lastVertexIndex = 0;
        }

        public (int Index1, int Index2, int Index3) Current { get; private set; }

        object IEnumerator.Current => Current;

        public void Dispose()
        { }
    }
}