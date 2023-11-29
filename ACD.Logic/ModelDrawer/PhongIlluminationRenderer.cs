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
    private readonly VertexTransform[] _vertices;
    private readonly Vector3[] _normals;
    private float[,]? _zBuffer;
    private readonly Vector3?[] _textureCoords;

    public PhongIlluminationRenderer(Model model)
    {
        _model = model;

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
            _zBuffer = new float[bitmap.Width, bitmap.Height];
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

                _textureCoords[vertexNumber] = texture;
            }
        });

        Span<Vector3> points = stackalloc Vector3[_model.MaxPolygonVertices];
        Span<Line> lines = stackalloc Line[3];
        Span<Vector3> coords = stackalloc Vector3[3];
        
        Span<(Vector2Int vertex, Vector3 normal)> data = stackalloc (Vector2Int vertex, Vector3 normal)[3];
        Span<(Vector2Int vertex, double value)> dataValue = stackalloc (Vector2Int vertex, double value)[3];
        
        for (var pi = 0; pi < _model.Polygons.Count; pi++)
        {
            var polygon = _model.Polygons[pi];

            if (!IsPolygonVisible(pi, cameraPosition)) continue;
            
            var baseIndex = pi * _model.MaxPolygonVertices;

            for (var i = 0; i < polygon.Vertices.Count; i++)
            {
                points[i] = new Vector3(
                    (int)_vertices[baseIndex + i].ScreenSpace.X,
                    (int)_vertices[baseIndex + i].ScreenSpace.Y,
                    (_vertices[baseIndex + i].ClipSpace.Z));
            }

            for (var i = 0; i < polygon.Vertices.Count - 2; i++)
            {
                var maxY = Math.Max(Math.Max((int)points[0].Y, (int)points[i + 1].Y), (int)points[i + 2].Y);
                var minY = Math.Min(Math.Min((int)points[0].Y, (int)points[i + 1].Y), (int)points[i + 2].Y);

                if ((minY < 0 && maxY < 0) || (minY >= bitmap.Height && maxY >= bitmap.Height))
                {
                    continue;
                }
                
                var maxX = Math.Max(Math.Max((int)points[0].X, (int)points[i + 1].X), (int)points[i + 2].X);
                var minX = Math.Min(Math.Min((int)points[0].X, (int)points[i + 1].X), (int)points[i + 2].X);

                if ((minX < 0 && maxX < 0) || (minX >= bitmap.Width && maxX >= bitmap.Width))
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

                        if ((int)line.From.Y == (int)line.To.Y && (int)line.To.Y == y)
                        {
                            coords[0] = line.From;
                            coords[1] = line.To;
                            c = 2;  
                            break;
                        }
                        
                        if (line.From.Y >= y && line.To.Y <= y)
                        {
                            var dy = y - line.From.Y;
                            var dx = (line.To.X - line.From.X) * dy / (line.To.Y - line.From.Y);
                            var dz = (line.To.Z - line.From.Z) * dy / (line.To.Y - line.From.Y);

                            coords[c++] = new Vector3(line.From.X + dx, y, line.From.Z + dz);
                        }
                    }

                    if (c < 2) continue;
                    
                    if ((coords[0].X < 0 && coords[1].X < 0) ||
                        (coords[0].X >= bitmap.Width && coords[1].X >= bitmap.Width))
                    {
                        continue;
                    }
                    
                    if (coords[0].X > coords[1].X)
                    {
                        (coords[0].X, coords[1].X) = (coords[1].X, coords[0].X);
                    }

                    var from = Math.Clamp((int)coords[0].X, 0, bitmap.Width - 1);
                    var to = Math.Clamp((int)coords[1].X, 0, bitmap.Width - 1);

                    var z = coords[0].Z * 1f;
                    var deltaZ = Math.Abs(from - to) < 0.00000001
                        ? 0f
                        : (coords[1].Z - coords[0].Z) / (coords[1].X - coords[0].X);

                    for (var x = from; x <= to; x++)
                    {
                        if (_zBuffer[x, y] > z)
                        {
                            _zBuffer[x, y] = z;

                            data[0] = (points[0].ToVector2().ToVector2Int(), _normals[baseIndex + 0]);
                            data[1] = (points[i + 1].ToVector2().ToVector2Int(), _normals[baseIndex + i + 1]);
                            data[2] = (points[i + 2].ToVector2().ToVector2Int(), _normals[baseIndex + i + 2]);

                            var normal = InterpolateNormal(data, new Vector2Int(x, y));

                            data[0].normal = _vertices[baseIndex + 0].WorldSpace.ToVector3();
                            data[1].normal = _vertices[baseIndex + i + 1].WorldSpace.ToVector3();
                            data[2].normal = _vertices[baseIndex + i + 2].WorldSpace.ToVector3();

                            var interpolatedVertex = InterpolateVertex(data, new Vector2(x, y));

                            var surfaceColor = new Color(255, 255, 255);
                            var specularModifier = 0.4f;
                            
                            if (_textureCoords[baseIndex].HasValue &&
                                _textureCoords[baseIndex + i + 1].HasValue &&
                                _textureCoords[baseIndex + i + 2].HasValue &&
                                polygon.Material != null)
                            {
                                var diffuseMap = polygon.Material.DiffuseMap;
                                
                                dataValue[0] = (points[0].ToVector2().ToVector2Int(), 1 / _vertices[baseIndex + 0].ClipSpace.W);
                                dataValue[1] = (points[i + 1].ToVector2().ToVector2Int(), 1 / _vertices[baseIndex + i + 1].ClipSpace.W);
                                dataValue[2] = (points[i + 2].ToVector2().ToVector2Int(), 1 / _vertices[baseIndex + i + 2].ClipSpace.W);
                                
                                var interpolatedRevZ = InterpolateValue(dataValue, new Vector2Int(x, y));
                                
                                data[0].normal = _textureCoords[baseIndex + 0]!.Value / _vertices[baseIndex + 0].ClipSpace.W;
                                data[1].normal = _textureCoords[baseIndex + i + 1]!.Value / _vertices[baseIndex + i + 1].ClipSpace.W;
                                data[2].normal = _textureCoords[baseIndex + i + 2]!.Value / _vertices[baseIndex + i + 2].ClipSpace.W;

                                var interpolatedTextureCoord = InterpolateVertex(data, new Vector2(x, y));

                                interpolatedTextureCoord /= (float)interpolatedRevZ;

                                surfaceColor = GetColorFromMap(diffuseMap);

                                if (polygon.Material.NormalMap != null)
                                {
                                    var normalColor = GetColorFromMap(polygon.Material.NormalMap);
                                    normal = 
                                        new Vector3(normalColor.R, normalColor.G, normalColor.B) / 255f * 2f -
                                        Vector3.One;
                                }

                                if (polygon.Material.MirrorMap != null)
                                {
                                    var mirrorColor = GetColorFromMap(polygon.Material.MirrorMap);
                                    specularModifier = mirrorColor.R / 255f;
                                }

                                Color GetColorFromMap(Color[,] map)
                                {
                                    var width = map.GetLength(0);
                                    var height = map.GetLength(1);
                                    
                                    var tx = (int)(interpolatedTextureCoord.X * (width - 1));
                                    var ty = (int)((1 - interpolatedTextureCoord.Y) * (height - 1));

                                    if (tx < 0)
                                    {
                                        tx = width - (-tx % width);
                                    }
                                
                                    if (ty < 0)
                                    {
                                        ty = height- (-ty % height);
                                    }

                                    tx %= width;
                                    ty %= height;
                                
                                    return map[
                                        tx % width,
                                        ty % height
                                    ];
                                }
                            }
                            
                            var color = GetVertexColor(
                                new Color(255,255,255),
                                surfaceColor,
                                lightPosition,
                                cameraPosition,
                                normal,
                                interpolatedVertex,
                                specularModifier);
                            
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
        Vector2 vertex)
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
    
    private static double InterpolateValue(
        Span<(Vector2Int vertex, double anchorValue)> data,
        Vector2Int vertex)
    {
        var (v1, v2, v3) = (data[0].vertex.ToVector2(), data[1].vertex.ToVector2(), data[2].vertex.ToVector2());

        if ((v2.Y - v3.Y) * (v1.X - v3.X) + (v3.X - v2.X) * (v1.Y - v3.Y) == 0) return data[0].anchorValue;

        var w1 = ((v2.Y - v3.Y) * (vertex.X - v3.X) + (v3.X - v2.X) * (vertex.Y - v3.Y)) /
                 ((v2.Y - v3.Y) * (v1.X - v3.X) + (v3.X - v2.X) * (v1.Y - v3.Y));
        var w2 = ((v3.Y - v1.Y) * (vertex.X - v3.X) + (v1.X - v3.X) * (vertex.Y - v3.Y)) /
                 ((v2.Y - v3.Y) * (v1.X - v3.X) + (v3.X - v2.X) * (v1.Y - v3.Y));
        var w3 = 1 - w1 - w2;

        var (a1, a2, a3) = (data[0].anchorValue, data[1].anchorValue, data[2].anchorValue);

        return (a1 * w1 + a2 * w2 + a3 * w3) / (w1 + w2 + w3);
    }
    
    private static Color GetVertexColor(
        Color lightColor,
        Color surfaceColor,
        Vector3 lightPosition,
        Vector3 cameraPosition,
        Vector3 normal,
        Vector3 vertex,
        float specularModifier)
    {
        var lightDirection = Vector3.Normalize(vertex - lightPosition);
        var cameraDirection = Vector3.Normalize(vertex - cameraPosition);
        
        var ambientColor = surfaceColor;
        var diffuseColor = GetDiffuseColor(surfaceColor, lightDirection, normal);
        var specularColor = GetSpecularColor(lightColor, lightDirection, cameraDirection, normal);

        return ambientColor * 0.6 + diffuseColor * 0.5 + specularColor * specularModifier;
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
        public readonly Vector3 From;
        public readonly Vector3 To;
    
        public Line(Vector3 from, Vector3 to)
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
