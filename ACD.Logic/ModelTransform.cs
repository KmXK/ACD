using System.Numerics;

namespace ACD.Logic;

public class ModelTransform
{
    private Vector3 _position;
    private Vector3 _axisRotation;
    private float _scale = 1.0f;

    private Matrix4x4 _move;
    private Matrix4x4 _rotationX;
    private Matrix4x4 _rotationY;
    private Matrix4x4 _rotationZ;
    private Matrix4x4 _scaleMatrix;
        
    private bool _needUpdateTransformation = true;
    private Matrix4x4 _transformation;

    public ModelTransform()
    {
        Scale = 1f;
        AxisRotation = Vector3.Zero;
        Position = Vector3.Zero;
    }
    
    public Matrix4x4 Transformation
    {
        get
        {
            if (_needUpdateTransformation)
            {
                _transformation = _rotationX * _rotationY * _rotationZ * _scaleMatrix * _move;
                _needUpdateTransformation = false;
            }

            return _transformation;
        }
    }

    public float Scale
    {
        get => _scale;
        set
        {
            if (value > 0) _scale = value;
            else _scale = 0;
                
            _scaleMatrix = Matrix4x4.CreateScale(_scale);
            _needUpdateTransformation = true;
        }
    }

    public Vector3 AxisRotation
    {
        get => _axisRotation;
        set
        {
            _axisRotation = value;
            _rotationX = Matrix4x4.CreateRotationX(_axisRotation.X);
            _rotationY = Matrix4x4.CreateRotationY(_axisRotation.Y);
            _rotationZ = Matrix4x4.CreateRotationZ(_axisRotation.Z);
            _needUpdateTransformation = true;
        }
    }
        
    public Vector3 Position
    {
        get => _position;
        set
        {
            _position = value;
                
            _move = Matrix4x4.CreateTranslation(_position.X, _position.Y, _position.Z);
            _needUpdateTransformation = true;
        }
    }
}