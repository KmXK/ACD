using System.Numerics;

namespace ACD.Logic;

public struct VectorSpherical
{
    public float R;
    public float AzimuthAngle;
    public float ElevationAngle;

    public VectorSpherical(float r, float azimuthAngle, float elevationAngle)
    {
        R = r;
        AzimuthAngle = azimuthAngle;
        ElevationAngle = elevationAngle;
    }

    public Vector3 ToCartesian()
    {
        var (sinAzimuth, cosAzimuth) = MathF.SinCos(AzimuthAngle);
        var (sinElevation, cosElevation) = MathF.SinCos(ElevationAngle);
        return new Vector3(R * sinElevation * sinAzimuth, R * cosElevation, R * sinElevation * cosAzimuth);
    }
}