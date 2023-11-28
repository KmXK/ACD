using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ACD.Infrastructure;
using ACD.Logic;
using ACD.Logic.Bitmap;
using ACD.Logic.LineDrawers;
using ACD.Logic.ModelDrawer;
using ACD.Logic.VertexTransformer;
using ACD.Parser;
using Microsoft.Win32;
using Color = System.Windows.Media.Color;

namespace ACD.WPF;

public partial class MainWindow
{
    private WriteableBitmap? _bitmap;
    private byte[] _pixelData;

    private readonly ModelTransform _transform = new();
    private readonly Camera _camera = new();
    private Vector3 _lightPosition;

    private Point? _clickPosition = null;

    private IRenderer? _modelDrawer;

    private int _selectedDrawer = 0;

    private bool _isMovingLight = true;
    
    public MainWindow()
    {
        InitializeComponent();

        DataContext = this;

        _lightPosition = _camera.SphericalPosition.ToCartesian();
    }

    private void MenuItem_Exit_OnClicked(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void MenuItem_Open_OnClicked(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog { Filter = "Obj files (*.obj)|*.obj" };

        Model? model = null;

        if (openFileDialog.ShowDialog() == false)
        {
            return;
        }
        
        var fileName = openFileDialog.FileName;
        var folderPath = Directory.GetParent(fileName);

        try
        {
            model = new ObjParser().Parse(File.ReadAllLines(fileName), folderPath?.FullName);
        }
        catch
        {
            MessageBox.Show("Invalid OBJ file format.");
            return;
        }
        
        _modelDrawer = new PhongIlluminationRenderer(model);
        DrawModel();
    }

    private static ACD.Infrastructure.Color[,]? GetColorArrayFromImagePath(string imagePath)
    {
        try
        {
            var bitmapDecoder = BitmapDecoder.Create(
                new Uri(imagePath),
                BitmapCreateOptions.None,
                BitmapCacheOption.Default);

            var bitmapFrame = bitmapDecoder.Frames[0];

            var writeableBitmap = new WriteableBitmap(bitmapFrame);

            var width = writeableBitmap.PixelWidth;
            var height = writeableBitmap.PixelHeight;
            var stride = (writeableBitmap.Format.BitsPerPixel + 7) / 8 * width;
            var pixelData = new byte[stride * height];

            writeableBitmap.CopyPixels(pixelData, stride, 0);

            var rgbArray = new ACD.Infrastructure.Color[width, height];

            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    var index = y * stride + x * 4;
                    var blue = pixelData[index];
                    var green = pixelData[index + 1];
                    var red = pixelData[index + 2];

                    rgbArray[x, y] = new ACD.Infrastructure.Color(red, green, blue);
                }
            }

            return rgbArray;
        }
        catch
        {
            return null;
        }
    }

    private void MainWindow_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Image.Width = ImageContainer.ActualWidth;
        Image.Height = ImageContainer.ActualHeight;
        
        _bitmap = new WriteableBitmap((int) Image.Width, (int) Image.Height, 96, 96, PixelFormats.Bgra32, null);
        var bytesPerPixel = (_bitmap.Format.BitsPerPixel + 7) / 8;
        _pixelData = new byte[_bitmap.PixelWidth * _bitmap.PixelHeight * bytesPerPixel];
        
        _camera.ScreenWidth = (float)Image.Width;
        _camera.ScreenHeight = (float)Image.Height;

        DrawModel();
        
        Image.Source = _bitmap;
    }

    private void MainWindow_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            var position = e.GetPosition(Image);
            
            if (_clickPosition.HasValue)
            {
                var delta = _clickPosition.Value - position;

                _camera.MoveOnSphere(new Vector2((float)delta.X / 100, (float)delta.Y / 100));

                if (_isMovingLight)
                {
                    _lightPosition = _camera.SphericalPosition.ToCartesian();
                }

                DrawModel();
            }

            _clickPosition = position;
        }
        else if (e.LeftButton == MouseButtonState.Released)
        {
            _clickPosition = null;
        }
    }

    private void DrawModel()
    {
        if (_bitmap is null || _modelDrawer is null)
        {
            return;
        }
        
        FillBitmap(Color.FromArgb(0, 0, 0, 0));

        var vertexTransformer = new VertexScreenTransformer(_camera, _transform);
        var bitmapAdapter = new WritableBitmapAdapter(_bitmap);

        _bitmap.Lock();
        _modelDrawer.DrawModel(bitmapAdapter, vertexTransformer, _lightPosition, _camera.SphericalPosition.ToCartesian());

        // DrawAxes(vertexTransformer, bitmapAdapter);
        
        _bitmap.Unlock();
    }

    private void FillBitmap(Color fillColor)
    {
        if (_bitmap == null)
        {
            return;
        }

        var width = _bitmap.PixelWidth;
        var height = _bitmap.PixelHeight;
        var bytesPerPixel = (_bitmap.Format.BitsPerPixel + 7) / 8;
        var stride = width * bytesPerPixel;

        if (_pixelData[2] != fillColor.R || _pixelData[1] != fillColor.G ||
            _pixelData[0] != fillColor.B || _pixelData[3] != fillColor.A)
        {
            for (var i = 0; i < _pixelData.Length; i += bytesPerPixel)
            {
                _pixelData[i + 2] = fillColor.R;
                _pixelData[i + 1] = fillColor.G;
                _pixelData[i + 0] = fillColor.B;
                _pixelData[i + 3] = fillColor.A;
            }
        }
        
        _bitmap.WritePixels(
            new Int32Rect(0, 0, width, height),
            _pixelData,
            stride,
            0);
    }

    private void MainWindow_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var delta = -e.Delta * 1f;

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            delta *= 3;
        }
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            delta /= 30f;
        }
        else
        {
            delta /= 300f;
        }
        
        _camera.Zoom(delta);
        
        if (_isMovingLight)
        {
            _lightPosition = _camera.SphericalPosition.ToCartesian();
        }
        
        DrawModel();
    }

    private void MainWindow_OnKeyDown(object sender, KeyEventArgs e)
    {
        var dict = new Dictionary<Key, Action>
        {
            [Key.Left] = () => _camera.MoveTarget(Vector3.UnitX * -1),
            [Key.Right] = () => _camera.MoveTarget(Vector3.UnitX * 1),
            [Key.Up] = () => _camera.MoveTarget(Vector3.UnitY * -1),
            [Key.Down] = () => _camera.MoveTarget(Vector3.UnitY * 1), 
            [Key.A] = () => _transform.Position += Vector3.UnitX * -1,
            [Key.D] = () => _transform.Position += Vector3.UnitX * 1,
            [Key.W] = () => _transform.Position += Vector3.UnitY * -1,
            [Key.S] = () => _transform.Position += Vector3.UnitY * 1,
            [Key.E] = () =>
            {
                _isMovingLight = !_isMovingLight;
                if (_isMovingLight) _lightPosition = _camera.SphericalPosition.ToCartesian();
            }
        };
        
        // if (e.Key == Key.D)
        // {
        //     _selectedDrawer = (_selectedDrawer + 1) % 2;
        //     DrawModel();
        // }
        // else
        // {
            dict.TryGetValue(e.Key, out var action);
            action?.Invoke();
            DrawModel();
        // }
    }
}