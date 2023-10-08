using System;
using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ACD.Infrastructure;
using ACD.Logic;
using ACD.Parser;
using ACD.WPF.Drawers;
using Microsoft.Win32;

namespace ACD.WPF;

public partial class MainWindow
{
    private Model? _model;
    private WriteableBitmap? _bitmap;
    private byte[] _pixelData;

    private readonly ModelTransform _transform = new();
    private readonly Camera _camera = new();

    private Point? _clickPosition = null;

    private Func<WriteableBitmap, DrawerBase>[] _drawerFactories =
    {
        bitmap => new DdaLineDrawer(bitmap),
        bitmap => new BresenhamDrawer(bitmap)
    };

    private int _selectedDrawer = 0;
    
    public MainWindow()
    {
        InitializeComponent();
    }

    private void MenuItem_Exit_OnClicked(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void MenuItem_Open_OnClicked(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog { Filter = "Obj files (*.obj)|*.obj" };

        if (openFileDialog.ShowDialog() == true)
        {
            var fileName = openFileDialog.FileName;
            _model = new ObjParser().Parse(File.ReadAllLines(fileName));
            DrawModel();
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

                DrawModel();
            }

            _clickPosition = position;
        }
        else if (e.LeftButton == MouseButtonState.Released)
        {
            _clickPosition = null;
        }
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

        for (var i = 0; i < _pixelData.Length; i += bytesPerPixel)
        {
            _pixelData[i + 2] = fillColor.R;
            _pixelData[i + 1] = fillColor.G;
            _pixelData[i + 0] = fillColor.B;
            _pixelData[i + 3] = 255;
        }
        
        _bitmap.WritePixels(
            new Int32Rect(0, 0, width, height),
            _pixelData,
            stride,
            0);
    }

    private void DrawModel()
    {
        if (_model is null)
        {
            return;
        }
        
        FillBitmap(Colors.Black);
        
        var drawer = _drawerFactories[_selectedDrawer].Invoke(_bitmap);

        _bitmap.Lock();
        
        foreach (var polygon in _model.Polygons)
        {
            Vector4? prevV = null;
            Vector4? firstV = null;
            
            foreach (var (vertex, _, _) in polygon.Vertices)
            {
                var v = Vector4.Transform(vertex, _transform.Transformation);
                v = Vector4.Transform(v, _camera.View);
                v = Vector4.Transform(v, _camera.Projection);
                
                v = Vector4.Transform(v, _camera.ViewPort);
                
                v /= v.W;

                firstV ??= v;

                if (prevV.HasValue)
                {
                    drawer.DrawLine(v.X, v.Y, prevV.Value.X, prevV.Value.Y);
                }

                prevV = v;
            }
            
            drawer.DrawLine(firstV!.Value.X, firstV.Value.Y, prevV!.Value.X, prevV.Value.Y);
        }
        
        _bitmap.Unlock();
    }

    private void MainWindow_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var delta = -e.Delta * 1f;

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            delta /= 30f;
        }
        else
        {
            delta /= 300f;
        }
        
        _camera.Zoom(delta);
        DrawModel();
    }

    private void MainWindow_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.D)
        {
            _selectedDrawer = (_selectedDrawer + 1) % _drawerFactories.Length;
            DrawModel();
        }
    }
}