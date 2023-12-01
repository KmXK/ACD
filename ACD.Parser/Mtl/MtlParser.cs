using System.Globalization;
using ACD.Infrastructure;

namespace ACD.Parser.Mtl;

public class MtlParser(IImagePixelsParser imagePixelsParser)
{
    public Dictionary<string, MtlMaterial> Parse(string filePath)
    {
        var localPath = Path.GetDirectoryName(filePath);

        var lines = File.ReadAllLines(filePath);

        var tokenLines = lines.Select(x => x.Trim().Split(' ').Where(x => x.Any()).ToArray());

        string? name = null;
        Color[,]? diffuseMap = null, normalMap = null, mirrorMap = null;

        var materials = new List<MtlMaterial>();
        
        foreach (var tokens in tokenLines)
        {
            if (tokens.Length == 0)
            {
                continue;
            }
            
            switch (tokens[0])
            {
                case "newmtl":
                    CreateMaterial();
                    name = tokens[1];
                    break;
                case "Kd":
                    diffuseMap = new[,]
                    {
                        { ParseColor(tokens) }
                    };
                    break;
                case "map_Kd":
                    diffuseMap = imagePixelsParser.GetImagePixels(Path.Combine(localPath!, tokens[1]));
                    break;
                case "norm":
                    normalMap = imagePixelsParser.GetImagePixels(Path.Combine(localPath!, tokens[1]));
                    break;
                case "map_MRAO":
                    mirrorMap = imagePixelsParser.GetImagePixels(Path.Combine(localPath!, tokens[1]));
                    break;
            }
        }
        
        CreateMaterial();
        return materials.ToDictionary(x => x.Name);
        
        void CreateMaterial()
        {
            if (name != null)
            {
                if (diffuseMap == null)
                {
                    throw new InvalidOperationException("Invalid MTL material: Diffuse Map is required.");
                }

                materials.Add(new MtlMaterial(
                    name,
                    diffuseMap,
                    normalMap,
                    mirrorMap));
            }
        }
    }

    private static Color ParseColor(IReadOnlyList<string> tokens)
    {
        if (tokens.Count != 4 ||
            !byte.TryParse(tokens[1], out var r) ||
            !byte.TryParse(tokens[2], out var g) ||
            !byte.TryParse(tokens[3], out var b))
        {
            throw new InvalidOperationException();
        }

        return new Color(r, g, b);
    }
}