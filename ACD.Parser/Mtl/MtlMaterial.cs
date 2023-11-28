using ACD.Infrastructure;

namespace ACD.Parser.Mtl;

public record MtlMaterial(
    string Name,
    Color[,] DiffuseMap,
    Color[,]? NormalMap,
    Color[,]? MirrorMap);