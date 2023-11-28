namespace ACD.Infrastructure;

public record MtlMaterial(
    string Name,
    Color[,] DiffuseMap,
    Color[,]? NormalMap,
    Color[,]? MirrorMap);