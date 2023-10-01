using System.Globalization;
using System.Numerics;

namespace ACD.Parser;

public class ObjParser : IParser
{
    public ParseResult Parse(IEnumerable<string> parseData)
    {
        var vertices = new List<Vector4>();
        var vertexTextures = new List<Vector3>();
        var vertexNormals = new List<Vector3>();
        var polygons = new List<Polygon>();
        
        foreach (var tokens in parseData.Select(x => x.Trim().Split(' ')))
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
        
        return new ParseResult(polygons);
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
        // TODO parse vertex texture
        return new Vector3();
    }

    private static Vector3 ParseVertexNormal(IReadOnlyList<string> tokens)
    {
        // TODO parse vertex normal
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
            
            if (int.TryParse(units[0], out var vertexIndex))
            {
                if (vertexIndex >= 0)
                {
                    vertexIndex--;
                }

                vertices.Add(new PolygonVertex(
                    readVertices[vertexIndex],
                    readVertexTextures[vertexIndex],
                    readVertexNormals[vertexIndex]));
            }
        }
        
        return new Polygon(vertices);
    }

    private static bool ConvertToFloat(string value, out float result)
    {
        return float.TryParse(value, CultureInfo.InvariantCulture, out result);
    }
}