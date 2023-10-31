using System.Numerics;

namespace ACD.Infrastructure;

public readonly struct VectorSpherical
{
    public readonly float R;
    public readonly float AzimuthAngle;
    public readonly float ElevationAngle;

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
        // return new Vector3(
        //     R * sinElevation * cosAzimuth,
        //     R * cosElevation,
        //     R * sinElevation * sinAzimuth);
        return new Vector3(R * sinElevation * sinAzimuth, R * cosElevation, R * sinElevation * cosAzimuth);
    }
}