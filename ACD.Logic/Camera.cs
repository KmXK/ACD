using System.Numerics;

namespace ACD.Logic;

public class Camera
{
    public VectorSpherical SphericalPosition { get; private set; } = new(10, 0, MathF.PI / 2);
    public Vector3 Target { get; private set; } = new(0, 0, 0);
    public Vector3 Up { get; private set; } = new(0, 1, 0);

    public Matrix4x4 View { get; private set; }
    public Matrix4x4 Projection { get; private set; }
    public Matrix4x4 ViewPort { get; private set; }

    private float _screenWidth = 16;
    private float _screenHeight = 9;
    private float _fov = MathF.PI / 2;
    private float _zNear = 0.1f;
    private float _zFar = 1000;

    public Camera()
    {
        UpdateViewMatrix();
        UpdateProjectionMatrix();
        UpdateViewPortMatrix();
    }

    public float ScreenWidth
    {
        get => _screenWidth;
        set
        {
            _screenWidth = value;
            
            UpdateProjectionMatrix();
            UpdateViewPortMatrix();
        }
    }

    public float ScreenHeight
    {
        get => _screenHeight;
        set
        {
            _screenHeight = value;
            UpdateProjectionMatrix();
            UpdateViewPortMatrix();
        }
    }

    public float Fov
    {
        get => _fov;
        set
        {
            _fov = Math.Clamp(value, MathF.PI / 180, MathF.PI * 179 / 180);
            
            UpdateProjectionMatrix();
        }
    }

    public float ZNear
    {
        get => _zNear;
        set
        {
            _zNear = Math.Clamp(value, 0.1f, _zFar - 0.1f);
            
            UpdateProjectionMatrix();
        }
    }

    public float ZFar
    {
        get => _zFar;
        set
        {
            _zFar = Math.Clamp(value, 0.1f, _zNear + 0.1f);
            
            UpdateProjectionMatrix();
        }
    }

    public void TranslateProjection(Vector2 vector)
    {
        SphericalPosition = new VectorSpherical(
            SphericalPosition.R,
            SphericalPosition.AzimuthAngle + vector.X,
            SphericalPosition.ElevationAngle + vector.Y);
        
        // var position = SphericalPosition.ToCartesian();
        //
        // var forward = Vector3.Normalize(Target - position);
        // var right = Vector3.Normalize(Vector3.Cross(forward, Up));
        //
        // Target += Up * vector.Y + right * vector.X;
        
        UpdateViewMatrix();
    }

    public void ResetPosition()
    {
        SphericalPosition = new VectorSpherical(5, 0, MathF.PI / 2);
        Target = new Vector3(0, 0, 0);
        Up = new Vector3(0, 1, 0);
        
        UpdateViewMatrix();
    }
    
    private void UpdateViewMatrix()
    {
        View = Matrix4x4.CreateLookAt(SphericalPosition.ToCartesian(), Target, Up);
    }
    
    private void UpdateProjectionMatrix()
    {
        Projection = Matrix4x4.CreatePerspectiveFieldOfView(_fov, _screenWidth / _screenHeight, _zNear, _zFar);
    }

    private void UpdateViewPortMatrix()
    {
        var halfWidth = (_screenWidth - 1) / 2;
        var halfHeight = (_screenHeight - 1) / 2;

        ViewPort = new Matrix4x4(
            halfWidth, 0,           0, 0,
            0,         -halfHeight, 0, 0,
            0,         0,           1, 0,
            halfWidth, halfHeight,  0, 1);
    }
}