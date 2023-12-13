namespace ACD.Infrastructure;

public readonly record struct Color(byte R, byte G, byte B, byte A = 255)
{
    public static Color operator *(Color color, double value)
    {
        var r = (byte)Math.Round(value * color.R);
        var g = (byte)Math.Round(value * color.G);
        var b = (byte)Math.Round(value * color.B);
        return new Color(r, g, b);
    }
    
    public static Color operator +(Color color1, Color color2)
    {
        var r = (byte)MathF.Min(color1.R + color2.R, 255);
        var g = (byte)MathF.Min(color1.G + color2.G, 255);
        var b = (byte)MathF.Min(color1.B + color2.B, 255);
        return new Color(r, g, b);
    }
}