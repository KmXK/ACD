using System.Drawing;
using System.Numerics;
using System.Runtime.Serialization;
using ACD.Infrastructure;
using ACD.Infrastructure.Vectors;
using ACD.Logic.Bitmap;
using ACD.Logic.VertexTransformer;

namespace ACD.Logic.ModelDrawer;

public class Renderer : IRenderer
{
    private readonly Model _model;
    private readonly Vector4[] _vertices;
    private int[,]? zBuffer;

    public Renderer(Model model)
    {
        _model = model;

        _vertices = new Vector4[model.Polygons.Count * model.MaxPolygonVertices];
    }
    
    public void DrawModel(
        IBitmap bitmap,
        IVertexTransformer vertexTransformer,
        Vector3 cameraPosition)
    {
        if (zBuffer is null || zBuffer.GetLength(0) != bitmap.Width || zBuffer.GetLength(1) != bitmap.Height)
        {
            zBuffer = new int[bitmap.Width, bitmap.Height];
        }

        for (var x = 0; x < bitmap.Width; x++)
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                zBuffer[x, y] = int.MaxValue;
            }
        }
        
        Parallel.ForEach(_model.Polygons, (polygon, _, pi) =>
        {
            for (var vi = 0; vi < polygon.Vertices.Count; vi++)
            {
                var (vertex, _, _) = polygon.Vertices[vi];
                var v = vertexTransformer.Transform(vertex);
                _vertices[pi * _model.MaxPolygonVertices + vi] = v;
            }
        });

        Span<Vector3Int> points = stackalloc Vector3Int[3];
        Span<Line> lines = stackalloc Line[3];
        
        Span<Vector3Int> coords = stackalloc Vector3Int[2];
        
        for (var pi = 0; pi < _model.Polygons.Count; pi++)
        {
            var polygon = _model.Polygons[pi];

            if (!IsPolygonVisible(polygon, cameraPosition)) continue;
            
            var color = GetPolygonColor(Color.White, Color.White, cameraPosition, polygon);
            
            var baseIndex = pi * _model.MaxPolygonVertices;

            for (var i = 0; i < 3; i++)
            {
                points[i] = new Vector3Int(
                    (int)_vertices[baseIndex + i].X,
                    (int)_vertices[baseIndex + i].Y,
                    (int)(_vertices[baseIndex + i].Z * 10_000_000));
            }

            var maxY = Math.Max(Math.Max(points[0].Y, points[1].Y), points[2].Y);
            var minY = Math.Min(Math.Min(points[0].Y, points[1].Y), points[2].Y);

            maxY = Math.Clamp(maxY, 0, bitmap.Height - 1);
            minY = Math.Clamp(minY, 0, bitmap.Height - 1);
            
            if (minY != maxY)
            {
                lines[0] = new Line(points[0], points[1]);
                lines[1] = new Line(points[1], points[2]);
                lines[2] = new Line(points[2], points[0]);
                
                for (var y = maxY; y >= minY; y--)
                {
                    var c = 0;
                    
                    for (var i = 0; i < 3 && c < 2; i++)
                    {
                        var line = lines[i];

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
                        if (zBuffer[x, y] > z)
                        {
                            zBuffer[x, y] = (int)z;
                            bitmap.DrawPixel(x, y, color);
                        }
                        
                        z += deltaZ;
                    }
                }
            }
        }
    }
    
    private static Color GetPolygonColor(Color lightColor, Color surfaceColor, Vector3 lightPosition, Polygon polygon)
    {
        var intensity = Math.Max(Vector3.Dot(Vector3.Normalize(lightPosition), Vector3.Normalize(polygon.Normal)), 0);
        var light = new Vector3(lightColor.R / 255f, lightColor.G / 255f, lightColor.B / 255f);
        var surface = new Vector3(surfaceColor.R / 255f, surfaceColor.G / 255f, surfaceColor.B / 255f);
        var r = (byte)MathF.Round(intensity * light.X * surface.X * 255);
        var g = (byte)MathF.Round(intensity * light.Y * surface.Y * 255);
        var b = (byte)MathF.Round(intensity * light.Z * surface.Z * 255);
        return Color.FromArgb(255, r, g, b);
    }
    
    private static bool IsPolygonVisible(Polygon polygon, Vector3 cameraPosition)
    {
        var target = polygon.Vertices[0].Coordinate.ToVector3() - cameraPosition;
        return Vector3.Dot(polygon.Normal, target) < 0;
    }
}

internal struct Line
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