using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;
using ACD.Infrastructure;
using Microsoft.VisualBasic;

namespace ACD.Parser;

public class ObjParser : IParser
{
    public Model? Parse(IEnumerable<string> parseData)
    {
        var vertices = new List<Vector4>();
        var vertexTextures = new List<Vector3>();
        var vertexNormals = new List<Vector3>();
        var polygons = new List<Polygon>();
        
        foreach (var tokens in parseData.Select(x => x.Trim().Split(' ').Where(x => x.Length > 0).ToArray()))
        {
            if (tokens.Length < 1) continue;
            
            try
            {
                switch (tokens[0])
                {
                    case "v":
                        vertices.Add(ParseVertex(tokens));
                        break;
                    case "vt":
                        vertexTextures.Add(ParseVertexTexture(tokens));
                        break;
                    case "vn":
                        vertexNormals.Add(ParseVertexNormal(tokens));
                        break;
                    case "f":
                        polygons.Add(ParsePolygon(
                            tokens,
                            vertices,
                            vertexTextures,
                            vertexNormals));
                        break;
                }
            }
            catch
            {
                // ignored
            }
        }
        
        return new Model(polygons);
    }

    private static Vector4 ParseVertex(IReadOnlyList<string> tokens)
    {
        if (tokens.Count < 4 ||
            !ConvertToFloat(tokens[1], out var x) ||
            !ConvertToFloat(tokens[2], out var y) ||
            !ConvertToFloat(tokens[3], out var z))
        {
            throw new InvalidOperationException("Invalid vertex syntax");
        }
        
        if (tokens.Count == 5 && ConvertToFloat(tokens[4], out var w))
        {
            return new Vector4(x, y, z, w);
        }
        
        return new Vector4(x, y, z, 1);
    }

    private static Vector3 ParseVertexTexture(IReadOnlyList<string> tokens)
    {
        if (tokens.Count is not (3 or 4))
        {
            throw new InvalidOperationException("Invalid vertex texture syntax");
        }

        if (ConvertToFloat(tokens[1], out var u) &&
            ConvertToFloat(tokens[2], out var v))
        {
            var w = 0f;
            
            if (tokens.Count > 3)
                ConvertToFloat(tokens[3], out w);
            
            return new Vector3(u, v, w);
        }

        return new Vector3();
    }

    private static Vector3 ParseVertexNormal(IReadOnlyList<string> tokens)
    {
        if (tokens.Count != 4)
        {
            throw new InvalidOperationException("Invalid polygon syntax");
        }

        if (ConvertToFloat(tokens[1], out var x) &&
            ConvertToFloat(tokens[2], out var y) &&
            ConvertToFloat(tokens[3], out var z))
        {
            return new Vector3(x, y, z);
        }

        return new Vector3();
    }

    private static Polygon ParsePolygon(
        IReadOnlyList<string> tokens,
        IReadOnlyList<Vector4> readVertices,
        IReadOnlyList<Vector3> readVertexTextures,
        IReadOnlyList<Vector3> readVertexNormals)
    {
        if (tokens.Count < 4)
        {
            throw new InvalidOperationException("Invalid polygon syntax");
        }

        var vertices = new List<PolygonVertex>();
        
        for (var i = 1; i < tokens.Count; i++)
        {
            var units = tokens[i].Split('/');

            var vertex = Get(0, readVertices);
            var texture = Get(1, readVertexTextures);
            var normal = Get(2, readVertexNormals);

            if (vertex.HasValue)
            {
                vertices.Add(new PolygonVertex(vertex.Value, texture, normal));
            }

            T? Get<T>(int unitIndex, IReadOnlyList<T> collection) where T : struct
            {
                if (units!.Length < unitIndex + 1)
                {
                    return null;
                }
                
                if (int.TryParse(units[unitIndex], out var index))
                {
                    if (index >= 0)
                    {
                        index--;
                    }
                
                    return collection[index];
                }

                return null;
            }
        }
        
        return new Polygon(vertices);
    }

    private static bool ConvertToFloat(string value, out float result)
    {
        return float.TryParse(value, CultureInfo.InvariantCulture, out result);
    }
}