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
            switch (tokens[0])
            {
                case "newmtl":
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
                    break;
                case "map_Kd":
                    diffuseMap = imagePixelsParser.GetImagePixels(Path.Combine(localPath!, tokens[0]));
                    break;
                case "norm":
                    normalMap = imagePixelsParser.GetImagePixels(Path.Combine(localPath!, tokens[0]));
                    break;
                case "map_MRAO":
                    mirrorMap = imagePixelsParser.GetImagePixels(Path.Combine(localPath!, tokens[0]));
                    break;
            }
        }

        return materials.ToDictionary(x => x.Name);
    }
}