using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Maestro.Core;

namespace Maestro.Desktop;

internal sealed class FitView : Border
{
    readonly Viewport3D view = new();
    readonly PerspectiveCamera cam = new() { FieldOfView = 43, NearPlaneDistance = .03, FarPlaneDistance = 10000 };
    readonly Model3DGroup scene = new();
    Point? drag; double yaw = .9, pitch = .20, radius = 55;
    Point3D focus;
    Model? current;
    static Point3D Point3(Vector3 p) => new(p.X, p.Y, p.Z);
    public FitView()
    {
        Background = StyleKit.Brush("#253638"); Child = view; view.Camera = cam; view.Children.Add(new ModelVisual3D { Content = scene });
        MouseLeftButtonDown += (_, e) => { drag = e.GetPosition(this); CaptureMouse(); };
        MouseMove += (_, e) => { if (drag.HasValue) { var p = e.GetPosition(this); yaw -= (p.X - drag.Value.X) * .012; pitch = Math.Clamp(pitch + (p.Y - drag.Value.Y) * .01, -1.3, 1.3); drag = p; Camera(); } };
        MouseLeftButtonUp += (_, _) => { drag = null; ReleaseMouseCapture(); };
        MouseWheel += (_, e) => { radius = Math.Clamp(radius * Math.Exp(-e.Delta * .001), .2, 5000); Camera(); };
    }
    void Camera()
    {
        var delta = new Vector3D(Math.Sin(yaw) * Math.Cos(pitch), Math.Sin(pitch), Math.Cos(yaw) * Math.Cos(pitch)) * radius;
        cam.Position = focus + delta; cam.LookDirection = -delta; cam.UpDirection = new(0, 1, 0);
    }
    static GeometryModel3D Shape(IEnumerable<Point3D> points, IEnumerable<int> indices, string colour)
    {
        var material = new DiffuseMaterial(StyleKit.Brush(colour));
        var mesh = new MeshGeometry3D { Positions = new Point3DCollection(points), TriangleIndices = new Int32Collection(indices) };
        mesh.Freeze(); var model = new GeometryModel3D(mesh, material) { BackMaterial = material }; model.Freeze(); return model;
    }
    static GeometryModel3D Mark(Vector3 p, float size, string colour)
    {
        Vector3[] tips = [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ];
        return Shape(tips.Select(t => Point3(p + t * size)), [0, 2, 4, 4, 2, 1, 1, 2, 5, 5, 2, 0, 0, 4, 3, 4, 1, 3, 1, 5, 3, 5, 0, 3], colour);
    }
    readonly Dictionary<Model, Model3DGroup> bodies = new();
    public void Draw(Model? animal, string? bone, Surface[]? saddle, Vector3 offset, Vector3 rotation, Vector3 seat, float facing)
    {
        scene.Children.Clear();
        scene.Children.Add(new AmbientLight(Color.FromRgb(142, 151, 149)));
        scene.Children.Add(new DirectionalLight(Colors.White, new(-.6, -1, -.4)));
        if (animal == null) { current = null; return; }
        if (!bodies.TryGetValue(animal, out var body))
        {
            body = new();
            foreach (var s in animal.Surfaces) body.Children.Add(Shape(s.Positions.Select(Point3), s.Indices, "#9c9181"));
            body.Freeze(); bodies[animal] = body;
        }
        scene.Children.Add(body);
        if (current != animal)
        {
            var b = body.Bounds; focus = new(b.X + b.SizeX / 2, b.Y + b.SizeY / 2, b.Z + b.SizeZ / 2); radius = Math.Max(2, Math.Max(b.SizeX, Math.Max(b.SizeY, b.SizeZ)) * 2.5); current = animal; Camera();
        }
        if (bone == null || !animal.Bones.TryGetValue(bone, out var anchor)) return;
        var pose = FitPose.At(anchor, offset, rotation, seat, facing);
        var m = pose.Saddle;
        if (saddle != null) foreach (var s in saddle) scene.Children.Add(Shape(s.Positions.Select(p => Point3(Vector3.Transform(p, m))), s.Indices, "#d18b50"));
        float size = (float)Math.Clamp(body.Bounds.SizeY * .044, .16, 1.5);
        scene.Children.Add(Mark(anchor, size * .7f, "#81c7ff"));
        var pelvis = pose.Pelvis;
        scene.Children.Add(Mark(pelvis, size, "#ffdf61"));
        var orient = pose.Facing;
        var front = Vector3.TransformNormal(Vector3.UnitZ, orient) * size;
        var side = Vector3.TransformNormal(Vector3.UnitX, orient) * size;
        var up = Vector3.TransformNormal(Vector3.UnitY, orient) * size;
        scene.Children.Add(Shape(new[] { pelvis + front * 4, pelvis + front + side * .65f, pelvis + front - side * .65f, pelvis + front + up * .5f }.Select(Point3), [0, 1, 3, 0, 3, 2, 0, 2, 1, 1, 2, 3], "#ffdf61"));
    }
}
