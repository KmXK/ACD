using System.Globalization;
using System.Numerics;
using ACD.Infrastructure;
using ACD.Parser.Mtl;

namespace ACD.Parser;

public class ObjParser(IImagePixelsParser imagePixelsParser)
{
    private readonly List<Vector4> _vertices = new();
    private readonly List<Vector3> _vertexTextures = new();
    private readonly List<Vector3> _vertexNormals = new();
    private readonly List<Polygon> _polygons = new();

    private Dictionary<string, MtlMaterial>? _materials;
    private MtlMaterial? _selectedMaterial;
    
    public Model Parse(IEnumerable<string> parseData, string? folderPath)
    {
        ClearData();
        
        var tokenLines = parseData.Select(x => x.Trim().Split(' ').ToArray());
        var mtlParser = new MtlParser(imagePixelsParser); 
        
        foreach (var tokens in tokenLines)
        {
            if (tokens.Any() == false) continue;
            
            switch (tokens[0])
            {
                case "v":
                    _vertices.Add(ParseVertex(tokens));
                    break;
                case "vt":
                    _vertexTextures.Add(ParseVertexTexture(tokens));
                    break;
                case "vn":
                    _vertexNormals.Add(ParseVertexNormal(tokens));
                    break;
                case "f":
                    _polygons.Add(ParsePolygon(tokens));
                    break;
                case "mtllib":
                    if (folderPath == null)
                    {
                        throw new InvalidOperationException("Folder path must be specified.");
                    }
                    
                    _materials = mtlParser.Parse(Path.Combine(folderPath, tokens[1]));
                    break;
                case "usemtl":
                    if (_materials == null || !_materials.TryGetValue(tokens[1], out _selectedMaterial))
                    {
                        throw new InvalidOperationException($"Missed MTL material with name {tokens[1]}.");
                    }

                    break;
            }
        }
        
        return new Model(_polygons);
    }

    private void ClearData()
    {
        _vertices.Clear();
        _vertexTextures.Clear();
        _vertexNormals.Clear();
        _polygons.Clear();
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
        if (tokens.Count is not (3 or 4) ||
            !ConvertToFloat(tokens[1], out var u) ||
            !ConvertToFloat(tokens[2], out var v))
        {
            throw new InvalidOperationException("Invalid vertex texture syntax");
        }

        if (tokens.Count > 3 && ConvertToFloat(tokens[3], out var w))
        {
            return new Vector3(u, v, w);
        }

        return new Vector3(u, v, 1);
    }

    private static Vector3 ParseVertexNormal(IReadOnlyList<string> tokens)
    {
        if (tokens.Count != 4 ||
            !ConvertToFloat(tokens[1], out var x) ||
            !ConvertToFloat(tokens[2], out var y) ||
            !ConvertToFloat(tokens[3], out var z))
        {
            throw new InvalidOperationException("Invalid polygon syntax");
        }

        return new Vector3(x, y, z);
    }

    private Polygon ParsePolygon(
        IReadOnlyList<string> tokens)
    {
        if (tokens.Count < 4)
        {
            throw new InvalidOperationException("Invalid polygon syntax");
        }

        var vertices = new List<PolygonVertex>();
        
        for (var i = 1; i < tokens.Count; i++)
        {
            var units = tokens[i].Split('/');

            var vertex = Get(0, _vertices);
            var texture = Get(1, _vertexTextures);
            var normal = Get(2, _vertexNormals);

            if (vertex.HasValue)
            {
                vertices.Add(new PolygonVertex(vertex.Value, texture, normal));
            }

            continue;

            T? Get<T>(int unitIndex, IReadOnlyList<T> collection) where T : struct
            {
                if (units.Length < unitIndex + 1)
                {
                    return null;
                }

                if (int.TryParse(units[unitIndex], out var index) == false ||
                    index == 0)
                {
                    return null;
                }

                if (index > 0)
                {
                    index--;
                }
                
                return collection[index];
            }
        }
        
        return new Polygon(vertices, _selectedMaterial);
    }

    private static bool ConvertToFloat(string value, out float result)
    {
        return float.TryParse(value, CultureInfo.InvariantCulture, out result);
    }
}