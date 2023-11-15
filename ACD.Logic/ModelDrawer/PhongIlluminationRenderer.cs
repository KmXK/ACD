using System.Drawing;
using System.Numerics;
using ACD.Infrastructure;
using ACD.Infrastructure.Vectors;
using ACD.Logic.Bitmap;
using ACD.Logic.VertexTransformer;
using Color = ACD.Infrastructure.Color;

namespace ACD.Logic.ModelDrawer;

public class PhongIlluminationRenderer : IRenderer
{
    private readonly Model _model;
    private readonly Vector4[] _vertices, _verticesWorldSpace;
    private readonly Vector3[] _normals;
    private int[,]? _zBuffer;

    public PhongIlluminationRenderer(Model model)
    {
        _model = model;

        var maxVerticesCount = model.Polygons.Count * model.MaxPolygonVertices;
        
        _verticesWorldSpace = new Vector4[maxVerticesCount];
        _vertices = new Vector4[maxVerticesCount];
        _normals = new Vector3[maxVerticesCount];
    }
    
    public void DrawModel(
        IBitmap bitmap,
        IVertexTransformer vertexTransformer,
        Vector3 lightPosition,
        Vector3 cameraPosition)
    {
        if (_zBuffer is null || _zBuffer.GetLength(0) != bitmap.Width || _zBuffer.GetLength(1) != bitmap.Height)
        {
            _zBuffer = new int[bitmap.Width, bitmap.Height];
        }

        for (var x = 0; x < bitmap.Width; x++)
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                _zBuffer[x, y] = int.MaxValue;
            }
        }
        
        Parallel.ForEach(_model.Polygons, (polygon, _, pi) =>
        {
            for (var vi = 0; vi < polygon.Vertices.Count; vi++)
            {
                var vertexNumber = pi * _model.MaxPolygonVertices + vi;
                
                var (vertex, _, normal) = polygon.Vertices[vi];
                var v = vertexTransformer.Transform(vertex);

                if (normal is null)
                {
                    throw new Exception($"Normal is null for vertex ({vertex.X}, {vertex.Y}, {vertex.Z})");
                }
                
                var n = Vector3.Normalize(vertexTransformer.ToWorldSpace(normal.Value.ToVector4()).ToVector3());

                _verticesWorldSpace[vertexNumber] = vertexTransformer.ToWorldSpace(vertex);
                _vertices[vertexNumber] = v;
                _normals[vertexNumber] = n;
            }
        });

        Span<Vector3Int> points = stackalloc Vector3Int[_model.MaxPolygonVertices];
        Span<Line> lines = stackalloc Line[3];
        Span<Vector3Int> coords = stackalloc Vector3Int[2];
        
        Span<(Vector2Int vertex, Vector3 normal)> data = stackalloc (Vector2Int vertex, Vector3 normal)[3];
        
        for (var pi = 0; pi < _model.Polygons.Count; pi++)
        {
            var polygon = _model.Polygons[pi];

            if (!IsPolygonVisible(pi, cameraPosition)) continue;
            
            var baseIndex = pi * _model.MaxPolygonVertices;

            for (var i = 0; i < polygon.Vertices.Count; i++)
            {
                points[i] = new Vector3Int(
                    (int)_vertices[baseIndex + i].X,
                    (int)_vertices[baseIndex + i].Y,
                    (int)(_vertices[baseIndex + i].Z * 10_000_000));
            }

            for (var i = 0; i < polygon.Vertices.Count - 2; i++)
            {
                var maxY = Math.Max(Math.Max(points[0].Y, points[i + 1].Y), points[i + 2].Y);
                var minY = Math.Min(Math.Min(points[0].Y, points[i + 1].Y), points[i + 2].Y);

                maxY = Math.Clamp(maxY, 0, bitmap.Height - 1);
                minY = Math.Clamp(minY, 0, bitmap.Height - 1);

                lines[0] = new Line(points[0], points[i + 1]);
                lines[1] = new Line(points[i + 1], points[i + 2]);
                lines[2] = new Line(points[i + 2], points[0]);

                for (var y = maxY; y >= minY; y--)
                {
                    var c = 0;

                    for (var li = 0; li < 3 && c < 2; li++)
                    {
                        var line = lines[li];

                        if (line.From.Y >= y && line.To.Y < y)
                        {
                            var dy = y - line.From.Y;
                            var dx = (line.To.X - line.From.X) * dy / (line.To.Y - line.From.Y);
                            var dz = (line.To.Z - line.From.Z) * dy / (line.To.Y - line.From.Y);

                            coords[c++] = new Vector3Int(line.From.X + dx, y, line.From.Z + dz);
                        }
                        else if (line.From.Y == line.To.Y && line.To.Y == y)
                        {
                            coords[0] = line.From;
                            coords[1] = line.To;
                            break;
                        }
                    }

                    var from = Math.Clamp(coords[0].X, 0, bitmap.Width - 1);
                    var to = Math.Clamp(coords[1].X, 0, bitmap.Width - 1);

                    if (from > to)
                    {
                        (from, to) = (to, from);
                    }

                    var z = coords[0].Z * 1f;
                    var deltaZ = from == to ? 0f : (coords[1].Z - coords[0].Z + 0f) / (to - from);

                    for (var x = from; x <= to; x++)
                    {
                        if (_zBuffer[x, y] > z)
                        {
                            _zBuffer[x, y] = (int)z;

                            data[0] = (points[0].ToVector2Int(), _normals[baseIndex + 0]);
                            data[1] = (points[i + 1].ToVector2Int(), _normals[baseIndex + i + 1]);
                            data[2] = (points[i + 2].ToVector2Int(), _normals[baseIndex + i + 2]);

                            var normal = InterpolateNormal(data, new Vector2Int(x, y));

                            var color = GetVertexColor(new Color(255,255,255), new Color(255,255,255), lightPosition, cameraPosition, normal);
                            
                            bitmap.DrawPixel(x, y, color);
                        }

                        z += deltaZ;
                    }
                }
            }
        }
    }

    private static Vector3 InterpolateNormal(
        Span<(Vector2Int vertex, Vector3 normal)> data,
        Vector2Int vertex)
    {
        var (v1, v2, v3) = (data[0].vertex.ToVector2(), data[1].vertex.ToVector2(), data[2].vertex.ToVector2());
        var (n1, n2, n3) = (data[0].normal, data[1].normal, data[2].normal);

        if ((v2.Y - v3.Y) * (v1.X - v3.X) + (v3.X - v2.X) * (v1.Y - v3.Y) == 0) return n1;

        var w1 = ((v2.Y - v3.Y) * (vertex.X - v3.X) + (v3.X - v2.X) * (vertex.Y - v3.Y)) /
                 ((v2.Y - v3.Y) * (v1.X - v3.X) + (v3.X - v2.X) * (v1.Y - v3.Y));
        var w2 = ((v3.Y - v1.Y) * (vertex.X - v3.X) + (v1.X - v3.X) * (vertex.Y - v3.Y)) /
                 ((v2.Y - v3.Y) * (v1.X - v3.X) + (v3.X - v2.X) * (v1.Y - v3.Y));
        var w3 = 1 - w1 - w2;

        return Vector3.Normalize((n1 * w1 + n2 * w2 + n3 * w3) / (w1 + w2 + w3));
    }
    
    private static Color GetVertexColor(Color lightColor, Color surfaceColor, Vector3 lightPosition, Vector3 cameraPosition, Vector3 normal)
    {
        var ambientColor = surfaceColor;
        var diffuseColor = GetDiffuseColor(surfaceColor, lightPosition, normal);
        var specularColor = GetSpecularColor(lightColor, lightPosition, cameraPosition, normal);

        return ambientColor * 0.3 + diffuseColor * 0.5 + specularColor * 0.8;
    }

    private static Color GetDiffuseColor(Color surfaceColor, Vector3 lightPosition, Vector3 normal)
    {
        var intensity = Math.Max(Vector3.Dot(Vector3.Normalize(lightPosition), Vector3.Normalize(normal)), 0);
        return surfaceColor * intensity;
    }
    
    private static Color GetSpecularColor(Color lightColor, Vector3 lightPosition, Vector3 cameraPosition, Vector3 normal)
    {
        lightPosition = Vector3.Normalize(-lightPosition);
        cameraPosition = Vector3.Normalize(-cameraPosition);
        
        var reflectionVector = lightPosition - 2 * Vector3.Dot(lightPosition, normal) * normal;

        var dot = Vector3.Dot(reflectionVector, cameraPosition);

        if (dot > 0)
        {
            return new Color(0, 0, 0);
        }
        
        return lightColor * Math.Pow(dot, 30);
    }
    
    private bool IsPolygonVisible(int polygonNumber, Vector3 cameraPosition)
    {
        var polygon = _model.Polygons[polygonNumber];
        
        var baseIndex = polygonNumber * _model.MaxPolygonVertices;

        for (var i = 0; i < polygon.Vertices.Count; i++)
        {
            var target = _verticesWorldSpace[baseIndex + i].ToVector3() - cameraPosition;
            if (Vector3.Dot(polygon.Normal, target) > 0) return false;
        }

        return true;
    }
    
    private struct Line
    {
        public readonly Vector3Int From;
        public readonly Vector3Int To;
    
        public Line(Vector3Int from, Vector3Int to)
        {
            if (from.Y < to.Y)
            {
                (from, to) = (to, from);
            }

            From = from;
            To = to;
        }
    }
}
