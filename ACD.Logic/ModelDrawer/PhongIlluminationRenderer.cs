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
    private readonly Color[,] _diffuseMap;
    private readonly VertexTransform[] _vertices;
    private readonly Vector3[] _normals;
    private int[,]? _zBuffer;
    private readonly Vector3?[] _textureCoords;

    public PhongIlluminationRenderer(Model model, Color[,] diffuseMap)
    {
        _model = model;
        _diffuseMap = diffuseMap;

        var maxVerticesCount = model.Polygons.Count * model.MaxPolygonVertices;
        
        _vertices = new VertexTransform[maxVerticesCount];
        _normals = new Vector3[maxVerticesCount];
        _textureCoords = new Vector3?[maxVerticesCount];
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
                
                var (vertex, texture, normal) = polygon.Vertices[vi];
                var v = vertexTransformer.Transform(vertex);

                if (normal is null)
                {
                    throw new Exception($"Normal is null for vertex ({vertex.X}, {vertex.Y}, {vertex.Z})");
                }
                
                var n = Vector3.Normalize(vertexTransformer.ToWorldSpace(normal.Value.ToVector4()).ToVector3());

                _vertices[vertexNumber] = v;
                _normals[vertexNumber] = n;

                if (texture.HasValue) texture = texture.Value with { Z = 0 };
                
                _textureCoords[vertexNumber] = texture;
            }
        });

        Span<Vector3Int> points = stackalloc Vector3Int[_model.MaxPolygonVertices];
        Span<Line> lines = stackalloc Line[3];
        Span<Vector3> coords = stackalloc Vector3[2];
        
        Span<(Vector2Int vertex, Vector3 normal)> data = stackalloc (Vector2Int vertex, Vector3 normal)[3];
        
        for (var pi = 0; pi < _model.Polygons.Count; pi++)
        {
            var polygon = _model.Polygons[pi];

            if (!IsPolygonVisible(pi, cameraPosition)) continue;
            
            var baseIndex = pi * _model.MaxPolygonVertices;

            for (var i = 0; i < polygon.Vertices.Count; i++)
            {
                points[i] = new Vector3Int(
                    (int)_vertices[baseIndex + i].ScreenSpace.X,
                    (int)_vertices[baseIndex + i].ScreenSpace.Y,
                    (int)(_vertices[baseIndex + i].ScreenSpace.Z * 100_000_000));
            }

            for (var i = 0; i < polygon.Vertices.Count - 2; i++)
            {
                var maxY = Math.Max(Math.Max(points[0].Y, points[i + 1].Y), points[i + 2].Y);
                var minY = Math.Min(Math.Min(points[0].Y, points[i + 1].Y), points[i + 2].Y);

                if ((minY < 0 && maxY < 0) || (minY >= bitmap.Height && maxY >= bitmap.Height))
                {
                    continue;
                }
                
                var maxX = Math.Max(Math.Max(points[0].X, points[i + 1].X), points[i + 2].X);
                var minX = Math.Min(Math.Min(points[0].X, points[i + 1].X), points[i + 2].X);

                if ((minX < 0 && maxX < 0) ||
                    (minX >= bitmap.Width && maxX >= bitmap.Width))
                {
                    continue;
                }
                
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
                            var dx = (line.To.X - line.From.X) * 1f * dy / (line.To.Y - line.From.Y);
                            var dz = (line.To.Z - line.From.Z) * 1f * dy / (line.To.Y - line.From.Y);

                            coords[c++] = new Vector3(line.From.X + dx, y, line.From.Z + dz);
                        }
                        else if (line.From.Y == line.To.Y && line.To.Y == y)
                        {
                            coords[0] = line.From.ToVector3();
                            coords[1] = line.To.ToVector3();
                            c = 2;  
                            break;
                        }
                    }

                    if (c < 2) continue;

                    var from = Math.Clamp(coords[0].X, 0, bitmap.Width - 1);
                    var to = Math.Clamp(coords[1].X, 0, bitmap.Width - 1);

                    if (from > to)
                    {
                        (from, to) = (to, from);
                    }

                    var z = coords[0].Z * 1f;
                    var deltaZ = Math.Abs(from - to) < 0.001
                        ? 0f
                        : (coords[1].Z - coords[0].Z + 0f) / (to - from);

                    for (var x = (int)Math.Ceiling(from); x <= (int)to; x++)
                    {
                        if (_zBuffer[x, y] > z)
                        {
                            _zBuffer[x, y] = (int)z;

                            data[0] = (points[0].ToVector2Int(), _normals[baseIndex + 0]);
                            data[1] = (points[i + 1].ToVector2Int(), _normals[baseIndex + i + 1]);
                            data[2] = (points[i + 2].ToVector2Int(), _normals[baseIndex + i + 2]);

                            var normal = InterpolateNormal(data, new Vector2Int(x, y));

                            data[0].normal = _vertices[baseIndex + 0].WorldSpace.ToVector3();
                            data[1].normal = _vertices[baseIndex + i + 1].WorldSpace.ToVector3();
                            data[2].normal = _vertices[baseIndex + i + 2].WorldSpace.ToVector3();

                            var interpolatedVertex = InterpolateVertex(data, new Vector2Int(x, y));

                            var surfaceColor = new Color(0, 0, 0);
                            
                            if (_textureCoords[baseIndex].HasValue &&
                                _textureCoords[baseIndex + i + 1].HasValue &&
                                _textureCoords[baseIndex + i + 2].HasValue)
                            {
                                data[0].normal = _textureCoords[baseIndex + 0]!.Value;
                                data[1].normal = _textureCoords[baseIndex + i + 1]!.Value;
                                data[2].normal = _textureCoords[baseIndex + i + 2]!.Value;

                                var interpolatedTextureCoord = InterpolateVertex(data, new Vector2Int(x, y));

                                var tx = interpolatedTextureCoord.X * (_diffuseMap.GetLength(0) - 1);
                                var ty = interpolatedTextureCoord.Y * (_diffuseMap.GetLength(1) - 1);

                                if (tx >= _diffuseMap.GetLength(0) || ty >= _diffuseMap.GetLength(1)
                                    || tx < 0 || ty < 0)
                                {
                                    ;
                                } 
                                
                                surfaceColor = _diffuseMap[
                                    (int)tx % _diffuseMap.GetLength(0),
                                    (int)ty % _diffuseMap.GetLength(1)];
                            }
                            
                            var color = GetVertexColor(
                                new Color(255,255,255),
                                surfaceColor,
                                lightPosition,
                                cameraPosition,
                                normal,
                                interpolatedVertex);
                            
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
    
    private static Vector3 InterpolateVertex(
        Span<(Vector2Int vertex, Vector3 anchorVertex)> data,
        Vector2Int vertex)
    {
        var (v1, v2, v3) = (data[0].vertex.ToVector2(), data[1].vertex.ToVector2(), data[2].vertex.ToVector2());

        if ((v2.Y - v3.Y) * (v1.X - v3.X) + (v3.X - v2.X) * (v1.Y - v3.Y) == 0) return data[0].anchorVertex;

        var w1 = ((v2.Y - v3.Y) * (vertex.X - v3.X) + (v3.X - v2.X) * (vertex.Y - v3.Y)) /
                 ((v2.Y - v3.Y) * (v1.X - v3.X) + (v3.X - v2.X) * (v1.Y - v3.Y));
        var w2 = ((v3.Y - v1.Y) * (vertex.X - v3.X) + (v1.X - v3.X) * (vertex.Y - v3.Y)) /
                 ((v2.Y - v3.Y) * (v1.X - v3.X) + (v3.X - v2.X) * (v1.Y - v3.Y));
        var w3 = 1 - w1 - w2;

        var (a1, a2, a3) = (data[0].anchorVertex, data[1].anchorVertex, data[2].anchorVertex);

        return (a1 * w1 + a2 * w2 + a3 * w3) / (w1 + w2 + w3);
    }
    
    private static Color GetVertexColor(
        Color lightColor,
        Color surfaceColor,
        Vector3 lightPosition,
        Vector3 cameraPosition,
        Vector3 normal,
        Vector3 vertex)
    {
        var lightDirection = Vector3.Normalize(vertex - lightPosition);
        var cameraDirection = Vector3.Normalize(vertex - cameraPosition);
        
        var ambientColor = surfaceColor;
        var diffuseColor = GetDiffuseColor(surfaceColor, lightDirection, normal);
        var specularColor = GetSpecularColor(lightColor, lightDirection, cameraDirection, normal);

        return ambientColor * 0.3 + diffuseColor * 0.5 + specularColor * 0.8;
    }

    private static Color GetDiffuseColor(Color surfaceColor, Vector3 lightDirection, Vector3 normal)
    {
        var intensity = Math.Max(0, Vector3.Dot(-lightDirection, normal));
        return surfaceColor * intensity;
    }
    
    private static Color GetSpecularColor(Color lightColor, Vector3 lightDirection, Vector3 cameraDirection, Vector3 normal)
    {
        lightDirection = Vector3.Normalize(lightDirection);
        cameraDirection = Vector3.Normalize(cameraDirection);
        
        var reflectionVector = lightDirection - 2 * Vector3.Dot(lightDirection, normal) * normal;

        var dot = Vector3.Dot(reflectionVector, cameraDirection);

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
            var target = _vertices[baseIndex + i].WorldSpace.ToVector3() - cameraPosition;
            if (Vector3.Dot(polygon.Normal, target) > 0) return false;

            if (_vertices[baseIndex + i].ClipSpace.Z < 0) return false;
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
