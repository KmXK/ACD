﻿using System.Drawing;
using System.Numerics;
using ACD.Infrastructure;
using ACD.Infrastructure.Vectors;
using ACD.Logic.Bitmap;
using ACD.Logic.VertexTransformer;
using Color = ACD.Infrastructure.Color;

namespace ACD.Logic.ModelDrawer;

public class LambertIlluminationRenderer : IRenderer
{
    private readonly Model _model;
    private readonly VertexTransform[] _vertices;
    private int[,]? _zBuffer;

    public LambertIlluminationRenderer(Model model)
    {
        _model = model;

        _vertices = new VertexTransform[model.Polygons.Count * model.MaxPolygonVertices];
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
                var (vertex, _, _) = polygon.Vertices[vi];
                var v = vertexTransformer.Transform(vertex);
                _vertices[pi * _model.MaxPolygonVertices + vi] = v;
            }
        });

        Span<Vector3Int> points = stackalloc Vector3Int[_model.MaxPolygonVertices];
        Span<Line> lines = stackalloc Line[3];
        
        Span<Vector3Int> coords = stackalloc Vector3Int[2];
        
        for (var pi = 0; pi < _model.Polygons.Count; pi++)
        {
            var polygon = _model.Polygons[pi];

            if (!IsPolygonVisible(polygon, cameraPosition)) continue;

            var color = GetPolygonColor(new Color(255, 255, 255), new Color(255, 255, 255), lightPosition, polygon);
            
            var baseIndex = pi * _model.MaxPolygonVertices;

            for (var i = 0; i < polygon.Vertices.Count; i++)
            {
                points[i] = new Vector3Int(
                    (int)_vertices[baseIndex + i].ScreenSpace.X,
                    (int)_vertices[baseIndex + i].ScreenSpace.Y,
                    (int)_vertices[baseIndex + i].ClipSpace.Z);
            }

            for (var i = 0; i < polygon.Vertices.Count - 2; i++)
            {
                var maxY = Math.Max(Math.Max(points[0].Y, points[i + 1].Y), points[i + 2].Y);
                var minY = Math.Min(Math.Min(points[0].Y, points[i + 1].Y), points[i + 2].Y);

                maxY = Math.Clamp(maxY, 0, bitmap.Height - 1);
                minY = Math.Clamp(minY, 0, bitmap.Height - 1);
                
                if (minY != maxY)
                {
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
                                bitmap.DrawPixel(x, y, color);
                            }

                            z += deltaZ;
                        }
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
        return new Color(r, g, b);
    }
    
    private static bool IsPolygonVisible(Polygon polygon, Vector3 cameraPosition)
    {
        var target = polygon.Vertices[0].Coordinate.ToVector3() - cameraPosition;
        return Vector3.Dot(polygon.Normal, target) < 0;
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
