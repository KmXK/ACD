using System.Diagnostics;
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
    private readonly Vector3?[] _normals;
    private float[,]? _zBuffer;
    private readonly Vector3?[] _textureCoords;
    
    private readonly Dictionary<Polygon, int> _polygonIndices;
    private IBitmap _bitmap;
    private IVertexTransformer _vertexTransformer;
    private Vector3 _lightPosition;
    private Vector3 _cameraPosition;

    public PhongIlluminationRenderer(Model model)
    {
        _model = model;

        var maxVerticesCount = model.Polygons.Count * model.MaxPolygonVertices;
        
        _vertices = new VertexTransform[maxVerticesCount];
        _normals = new Vector3?[maxVerticesCount];
        _textureCoords = new Vector3?[maxVerticesCount];
        
        _polygonIndices = _model.Polygons.Select((polygon, index) => (polygon, index)).ToDictionary(x => x.polygon, x => x.index);
    }
    
    public void DrawModel(
        IBitmap bitmap,
        IVertexTransformer vertexTransformer,
        Vector3 lightPosition,
        Vector3 cameraPosition)
    {
        InitializeZBuffer(bitmap);
        UpdateModelData(vertexTransformer);

        _bitmap = bitmap;
        _vertexTransformer = vertexTransformer;
        _lightPosition = lightPosition;
        _cameraPosition = cameraPosition;
        
        //foreach (var polygon in _model.Polygons)
        
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Debugger.IsAttached ? 1 : Environment.ProcessorCount
        };
        
        Parallel.ForEach(_model.Polygons, options, (polygon, _, _) =>
        {
            if (!IsPolygonVisible(polygon, cameraPosition)) return;

            DrawPolygon(polygon);
        });
    }

    private void DrawPolygon(Polygon polygon)
    {
        Span<Vector2Int> screenCoords = stackalloc Vector2Int[3];
        Span<float> screenZ = stackalloc float[3];
        Span<int> vertexLocalIndices = stackalloc int[3];
        Span<int> vertexGlobalIndices = stackalloc int[3];

        Span<Vector2> lines = stackalloc Vector2[3];
        
        Span<Vector2Int> verticesScreenCoords = stackalloc Vector2Int[_model.MaxPolygonVertices];
        Span<float> verticesZ = stackalloc float[_model.MaxPolygonVertices];
        
        for (var i = 0; i < polygon.Vertices.Count; i++)
        {
            var vertexIndex = GetVertexIndex(polygon, i);

            verticesScreenCoords[i] = new Vector2Int(
                (int)_vertices[vertexIndex].ScreenSpace.X,
                (int)_vertices[vertexIndex].ScreenSpace.Y);
            
            verticesZ[i] = _vertices[vertexIndex].ScreenSpace.Z;
        }
        
        foreach (var (i1, i2, i3) in polygon)
        {
            vertexLocalIndices[0] = i1;
            vertexLocalIndices[1] = i2;
            vertexLocalIndices[2] = i3;

            for (var i = 0; i < 3; i++)
            {
                screenZ[i] = verticesZ[vertexLocalIndices[i]];
                screenCoords[i] = verticesScreenCoords[vertexLocalIndices[i]];
                vertexGlobalIndices[i] = GetVertexIndex(polygon, vertexLocalIndices[i]);
            }
            
            if (IsTriangleIsOnScreen(screenCoords, out var minY, out var maxY) == false)
            {
                continue;
            }

            maxY = Math.Clamp(maxY, 0, _bitmap.Height - 1);
            minY = Math.Clamp(minY, 0, _bitmap.Height - 1);

            lines[0] = screenCoords[1].ToVector2() - screenCoords[0].ToVector2();
            lines[1] = screenCoords[2].ToVector2() - screenCoords[1].ToVector2();
            lines[2] = screenCoords[0].ToVector2() - screenCoords[2].ToVector2();

            var minX = Math.Min(screenCoords[0].X, Math.Min(screenCoords[1].X, screenCoords[2].X));
            var maxX = Math.Max(screenCoords[0].X, Math.Max(screenCoords[1].X, screenCoords[2].X));

            minX = Math.Clamp(minX, 0, _bitmap.Width - 1);
            maxX = Math.Clamp(maxX, 0, _bitmap.Width - 1);

            for (var y = maxY; y >= minY; y--)
            {
                if (GetHorizontalLineRasterisationRange(
                        lines,
                        screenCoords,
                        screenZ,
                        minX,
                        maxX,
                        y,
                        out var from, out var to) == false)
                {
                    continue;
                }

                var z = from.Z * 1f;
                var deltaZ = Math.Abs(from.X - to.X) < 0.001
                    ? 0f
                    : (to.Z - from.Z + 0f) / (to.X - from.X);

                for (var x = (int)from.X; x <= to.X; x++)
                {
                    DrawPixel(polygon, x, y, z, screenCoords, vertexGlobalIndices);

                    z += deltaZ;
                }
            }
        }
    }

    private bool GetHorizontalLineRasterisationRange(
        Span<Vector2> lines,
        Span<Vector2Int> screenCoords,
        Span<float> screenZ,
        int minX,
        int maxX, 
        int y, 
        out Vector3 from, out Vector3 to)
    {
        from = to = -Vector3.One;

        var findingMin = true;
        
        for (var x = minX; x <= maxX; x++)
        {
            var result = true;
            
            for (var i = 0; i < 3; i++)
            {
                var z = lines[i].X * (y - screenCoords[i].Y) - lines[i].Y * (x - screenCoords[i].X);

                if (z > 0) result = false;
            }
            
            if (result)
            {
                if (findingMin)
                {
                    from = new Vector3(x, y, 0);
                    findingMin = false;
                }
            }
            else if (findingMin == false)
            {
                break;
            }
            
            
            to = new Vector3(x, y, 0);
        }
        
        if (from.X < 0 || to.X < 0)
            return false;
        
        Span<(Vector2Int vertex, float value)> dataValue = stackalloc (Vector2Int vertex, float value)[3];
        
        for (var i = 0; i < 3; i++)
        {
            dataValue[i].vertex = screenCoords[i];
            dataValue[i].value = screenZ[i];
        }       
        
        from.Z = InterpolateValue(dataValue, from.ToVector2Int());
        to.Z = InterpolateValue(dataValue, to.ToVector2Int());

        return true;
    }

    private void DrawPixel(
        Polygon polygon, 
        int x, int y, float z, 
        Span<Vector2Int> screenCoords,
        Span<int> vertexGlobalIndices)
    {
        if (_zBuffer![x, y] < z)
        {
            return;
        }
        
        Span<(Vector2Int vertex, Vector3 normal)> data = stackalloc (Vector2Int vertex, Vector3 normal)[3];
        Span<(Vector2Int vertex, float value)> dataValue = stackalloc (Vector2Int vertex, float value)[3];

        _zBuffer[x, y] = z;

        var polygonNormal =
            Vector3.Normalize(_vertexTransformer.ToWorldSpace(polygon.Normal.ToVector4()).ToVector3());

        for (var i = 0; i < 3; i++)
        {
            data[i].vertex = screenCoords[i];
            data[i].normal = _normals[vertexGlobalIndices[i]] ?? polygonNormal;
        }

        var normal = InterpolateNormal(data, new Vector2Int(x, y));
        
        for (var i = 0; i < 3; i++)
        {
            dataValue[i].vertex = screenCoords[i];
            dataValue[i].value = 1 / _vertices[vertexGlobalIndices[i]].ClipSpace.W;
        }

        var interpolatedRevZ = InterpolateValue(dataValue, new Vector2Int(x, y));
        
        for (var i = 0; i < 3; i++)
        {
            data[i].normal = _vertices[vertexGlobalIndices[i]].WorldSpace.ToVector3() / 
                             _vertices[vertexGlobalIndices[i]].ClipSpace.W;
        }

        var interpolatedVertex = InterpolateVertex(data, new Vector2(x, y));

        interpolatedVertex /= interpolatedRevZ;
        
        var surfaceColor = new Color(255, 255, 255);
        var specularModifier = 0.4f;

        if (_textureCoords[vertexGlobalIndices[0]].HasValue &&
            _textureCoords[vertexGlobalIndices[1]].HasValue &&
            _textureCoords[vertexGlobalIndices[2]].HasValue &&
            polygon.Material != null)
        {
            var diffuseMap = polygon.Material.DiffuseMap;

            for (var i = 0; i < 3; i++)
            {
                data[i].normal = _textureCoords[vertexGlobalIndices[i]]!.Value / _vertices[vertexGlobalIndices[i]].ClipSpace.W;
            }

            var interpolatedTextureCoord = InterpolateVertex(data, new Vector2(x, y));

            interpolatedTextureCoord /= interpolatedRevZ;

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
                    ty = height - (-ty % height);
                }

                tx %= width;
                ty %= height;

                return map[
                    tx % width,
                    ty % height
                ];
            }
        }

        // Calculate shadow
        var lightColor = new Color(255, 255, 255);

        foreach (var checkPolygon in _model.Polygons)
        {
            if (checkPolygon == polygon) continue;

            foreach (var checkTriangleVertices in checkPolygon)
            {
                if (GetTriangleIntersection(
                        _lightPosition,
                        Vector3.Normalize(interpolatedVertex - _lightPosition),
                        _vertices[GetVertexIndex(checkPolygon, checkTriangleVertices.Index1)].WorldSpace
                            .ToVector3(),
                        _vertices[GetVertexIndex(checkPolygon, checkTriangleVertices.Index2)].WorldSpace
                            .ToVector3(),
                        _vertices[GetVertexIndex(checkPolygon, checkTriangleVertices.Index3)].WorldSpace
                            .ToVector3(),
                        out var distance))
                {
                    if (distance < (interpolatedVertex - _lightPosition).Length() - 1e-3)
                    {
                        lightColor = new Color(0, 0, 0);
                        break;
                    }
                }
            }
        }

        var color = GetVertexColor(
            // new Color(255, 255, 255),
            lightColor,
            surfaceColor,
            _lightPosition,
            _cameraPosition,
            normal,
            interpolatedVertex,
            specularModifier);
                        
        _bitmap.DrawPixel(x, y, color, z);
    }

    private bool IsTriangleIsOnScreen(Span<Vector2Int> screenCoords, out int minY, out int maxY)
    {
        maxY = Math.Max(Math.Max(screenCoords[0].Y, screenCoords[1].Y), screenCoords[2].Y);
        minY = Math.Min(Math.Min(screenCoords[0].Y, screenCoords[1].Y), screenCoords[2].Y);

        if ((minY < 0 && maxY < 0) || (minY >= _bitmap.Height && maxY >= _bitmap.Height))
        {
            return false;
        }

        var maxX = Math.Max(Math.Max(screenCoords[0].X, screenCoords[1].X), screenCoords[2].X);
        var minX = Math.Min(Math.Min(screenCoords[0].X, screenCoords[1].X), screenCoords[2].X);

        if ((minX < 0 && maxX < 0) || (minX >= _bitmap.Width && maxX >= _bitmap.Width))
        {
            return false;
        }

        return true;
    }

    private int GetVertexIndex(Polygon polygon, int localIndex)
    {
        return _polygonIndices[polygon] * _model.MaxPolygonVertices + localIndex;
    }
    
    private void UpdateModelData(IVertexTransformer vertexTransformer)
    {
        Parallel.ForEach(_model.Polygons, (polygon, _, pi) =>
        {
            for (var vi = 0; vi < polygon.Vertices.Count; vi++)
            {
                var vertexNumber = pi * _model.MaxPolygonVertices + vi;

                var (vertex, texture, normal) = polygon.Vertices[vi];
                var v = vertexTransformer.Transform(vertex);

                if (normal.HasValue)
                    normal = Vector3.Normalize(vertexTransformer.ToWorldSpace(normal.Value.ToVector4()).ToVector3());

                _vertices[vertexNumber] = v;
                _normals[vertexNumber] = normal;

                _textureCoords[vertexNumber] = texture;
            }
        });
    }

    private void InitializeZBuffer(IBitmap bitmap)
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
    }

    private static bool GetTriangleIntersection(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 v0, Vector3 v1, Vector3 v2,
        out float distance)
    {
        distance = 0;
        
        var e1 = v1 - v0;
        var e2 = v2 - v0;

        var ray_cross_e2 = Vector3.Cross(rayDirection, e2);
        var det = Vector3.Dot(e1, ray_cross_e2);

        if (det < 1e-8 && det > -1e-8)
        {
            return false;
        }

        var inv_det = 1 / det;
        var s = rayOrigin - v0;
        var u = Vector3.Dot(s, ray_cross_e2) * inv_det;
        if (u < 0 || u > 1)
        {
            return false;
        }

        var s_cross_e1 = Vector3.Cross(s, e1);
        var v = Vector3.Dot(rayDirection, s_cross_e1) * inv_det;
        if (v < 0 || u + v > 1)
        {
            return false;
        }

        distance = Vector3.Dot(e2, s_cross_e1) * inv_det;

        return distance > 1e-8;
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
    
    private static float InterpolateValue(
        Span<(Vector2Int vertex, float anchorValue)> data,
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
        if (lightColor is { R: 0, B: 0, G: 0 }) return surfaceColor * 0.6;
        
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
    
    private bool IsPolygonVisible(Polygon polygon, Vector3 cameraPosition)
    {
        for (var i = 0; i < polygon.Vertices.Count; i++)
        {
            var vertexIndex = GetVertexIndex(polygon, i);
            
            var target = _vertices[vertexIndex].WorldSpace.ToVector3() - cameraPosition;
            if (Vector3.Dot(polygon.Normal, target) > 0) return false;

            if (_vertices[vertexIndex].ClipSpace.Z < 0) return false;
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
