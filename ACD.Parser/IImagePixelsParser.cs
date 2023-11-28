using ACD.Infrastructure;

namespace ACD.Parser;

public interface IImagePixelsParser
{
    Color[,] GetImagePixels(string imagePath);
}