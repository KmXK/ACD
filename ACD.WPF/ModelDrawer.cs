using System;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ACD.Infrastructure;
using ACD.WPF.Drawers;

namespace ACD.WPF;

public class ModelDrawer
{
    private readonly Model _model;

    public ModelDrawer(Model model)
    {
        _model = model;
    }
    
    private Func<WriteableBitmap, LineDrawerBase>[] _drawerFactories =
    {
        bitmap => new DdaLineLineDrawer(bitmap),
        bitmap => new BresenhamLineDrawer(bitmap)
    };
    
    private void DrawModel(LineDrawerBase lineDrawerBase)
    {
        Span<Vector4> points = stackalloc Vector4[polygon.Vertices.Count];
        
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
        
        bitmap.Unlock();
    }
}