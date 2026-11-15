using System.Numerics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Maestro.Core;
using static Avalonia.OpenGL.GlConsts;

namespace Maestro.CrossPlatform;

internal sealed class FitView : OpenGlControlBase
{
    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Vertex(Vector3 Position, Vector3 Colour);

    static readonly int[] MarkerIndices = [0,2,4, 4,2,1, 1,2,5, 5,2,0, 0,4,3, 4,1,3, 1,5,3, 5,0,3];
    static readonly Vector3[] MarkerAxes = [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ];
    static readonly Vector3 Sun = Vector3.Normalize(new(-.6f, 1, .4f));
    private Vertex[] vertices = [], body = [];
    private int solidCount, shader, buffer, array, matrixLocation;
    private bool upload = true;
    private Model? current;
    private Vector3 focus;
    private float size, radius = 50, yaw = .9f, pitch = .2f;
    private Point? pointer;
    internal string Renderer { get; private set; } = "";
    internal int Frames { get; private set; }
    internal int Orbits { get; private set; }
    internal int WheelZooms { get; private set; }
    internal int KeyZooms { get; private set; }
    internal event Action<string>? Error;

    internal Grid WithPointerSurface()
    {
        // The GPU image is a composition child. Give it a hit-testable input surface.
        var input = new Border { Background = Brushes.Transparent, Focusable = true };
        var grid = new Grid();
        grid.Children.Add(this);
        grid.Children.Add(input);
        input.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(input).Properties.IsLeftButtonPressed) return;
            input.Focus(); pointer = e.GetPosition(input); e.Pointer.Capture(input); e.Handled = true;
        };
        input.PointerCaptureLost += (_, _) => pointer = null;
        input.PointerReleased += (_, e) => { pointer = null; e.Pointer.Capture(null); };
        input.PointerMoved += (_, e) => MoveCamera(e.GetPosition(input));
        input.PointerWheelChanged += (_, e) =>
        {
            Zoom((float)e.Delta.Y); WheelZooms++; e.Handled = true;
        };
        input.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Add or Key.OemPlus) Zoom(1);
            else if (e.Key is Key.Subtract or Key.OemMinus) Zoom(-1);
            else return;
            KeyZooms++; e.Handled = true;
        };
        return grid;
    }
    void MoveCamera(Point now)
    {
        if (pointer is not Point start) return;
        yaw -= (float)(now.X - start.X) * .012f;
        pitch = Math.Clamp(pitch + (float)(now.Y - start.Y) * .01f, -1.3f, 1.3f);
        pointer = now; Orbits++; RequestNextFrameRendering();
    }
    void Zoom(float steps)
    {
        radius = Math.Clamp(radius * MathF.Exp(-.12f * steps), .2f, 5000);
        RequestNextFrameRendering();
    }

    static void Append(List<Vertex> dst, Vector3[] points, int[] indices, Vector3 colour, Matrix4x4 transform)
    {
        for (int i = 0; i < indices.Length; i += 3)
        {
            var a = Vector3.Transform(points[indices[i]], transform);
            var b = Vector3.Transform(points[indices[i + 1]], transform);
            var c = Vector3.Transform(points[indices[i + 2]], transform);
            var n = Vector3.Cross(b - a, c - a);
            if (n.LengthSquared() < 1e-12f) continue;
            var lit = colour * (.50f + .50f * MathF.Abs(Vector3.Dot(Vector3.Normalize(n), Sun)));
            dst.Add(new(a, lit)); dst.Add(new(b, lit)); dst.Add(new(c, lit));
        }
    }

    private async void CheckFirstFrame(Model animal)
    {
        await Task.Delay(5000);
        if (current == animal && Frames == 0 && IsEffectivelyVisible)
            Error?.Invoke("3D preview unavailable. Check that the desktop session provides OpenGL.");
    }

    internal void Draw(Model? animal, string? bone, Surface[]? saddle, Vector3 offset, Vector3 rotation, Vector3 seat, float facing)
    {
        if (current != animal)
        {
            current = animal;
            var mesh = new List<Vertex>();
            if (animal != null)
            {
                foreach (var s in animal.Surfaces) Append(mesh, s.Positions, s.Indices, new(.73f, .65f, .53f), Matrix4x4.Identity);
                if (mesh.Count == 0) throw new InvalidDataException("This animal has no preview triangles.");
                Vector3 min = mesh[0].Position, max = min;
                foreach (var v in mesh) { min = Vector3.Min(min, v.Position); max = Vector3.Max(max, v.Position); }
                focus = (min + max) / 2;
                var extent = max - min;
                size = Math.Clamp(extent.Y * .034f, .16f, 1.5f);
                radius = Math.Max(2, float.Max(extent.X, float.Max(extent.Y, extent.Z)) * 1.65f);
                if (Frames == 0) CheckFirstFrame(animal);
            }
            body = mesh.ToArray();
        }
        var scene = new List<Vertex>(body);
        solidCount = scene.Count;
        if (bone != null && animal != null && animal.Bones.TryGetValue(bone, out var anchor))
        {
            var pose = FitPose.At(anchor, offset, rotation, seat, facing);
            if (saddle != null) foreach (var s in saddle) Append(scene, s.Positions, s.Indices, new(.93f, .50f, .24f), pose.Saddle);
            solidCount = scene.Count;
            // Markers overlay the opaque animal so an internal bind point is still pickable by sight.
            Append(scene, MarkerAxes, MarkerIndices, new(.35f,.78f,1), Matrix4x4.CreateScale(size * .65f) * Matrix4x4.CreateTranslation(anchor));
            Append(scene, MarkerAxes, MarkerIndices, new(1,.86f,.32f), Matrix4x4.CreateScale(size) * Matrix4x4.CreateTranslation(pose.Pelvis));
            var orient = pose.Facing;
            orient.Translation = pose.Pelvis;
            Vector3[] arrow = [new(0,0,4), new(.65f,0,1), new(-.65f,0,1), new(0,.5f,1)];
            Append(scene, arrow, [0,1,3, 0,3,2, 0,2,1, 1,2,3], new(1,.86f,.32f), Matrix4x4.CreateScale(size) * orient);
        }
        vertices = scene.ToArray();
        upload = true; RequestNextFrameRendering();
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        Renderer = gl.Renderer ?? "Unknown OpenGL renderer";
        bool es = GlVersion.Type == GlProfileType.OpenGLES;
        string header = es ? "#version 100\nprecision highp float;\n" : "#version 130\n";
        string vin = es ? "attribute" : "in", vout = es ? "varying" : "out";
        int vs = gl.CreateShader(GL_VERTEX_SHADER), fs = gl.CreateShader(GL_FRAGMENT_SHADER);
        try
        {
            string vert = $"{header}{vin} vec3 pos; {vin} vec3 tint; {vout} vec3 colour; uniform mat4 mvp;\n" +
                "void main(){ colour=tint; gl_Position=mvp*vec4(pos,1.); gl_Position.z=2.*gl_Position.z-gl_Position.w; }";
            string frag = es ? "varying vec3 colour; void main(){gl_FragColor=vec4(colour,1.);}" :
                "in vec3 colour; out vec4 pixel; void main(){pixel=vec4(colour,1.);}";
            Check(gl.CompileShaderAndGetError(vs, vert));
            Check(gl.CompileShaderAndGetError(fs, header + frag));
            shader = gl.CreateProgram();
            gl.AttachShader(shader, vs); gl.AttachShader(shader, fs);
            gl.BindAttribLocationString(shader, 0, "pos"); gl.BindAttribLocationString(shader, 1, "tint");
            Check(gl.LinkProgramAndGetError(shader));
            matrixLocation = gl.GetUniformLocationString(shader, "mvp");
            buffer = gl.GenBuffer();
            if (gl.IsGenVertexArraysAvailable) array = gl.GenVertexArray();
            upload = true;
        }
        catch (Exception ex) { Error?.Invoke(ex.Message); throw; }
        finally { gl.DeleteShader(vs); gl.DeleteShader(fs); }
    }
    static void Check(string? message) { if (!string.IsNullOrEmpty(message)) throw new InvalidOperationException(message); }

    protected override unsafe void OnOpenGlRender(GlInterface gl, int fb)
    {
        double scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        int width = Math.Max(1, (int)Math.Round(Bounds.Width * scale));
        int height = Math.Max(1, (int)Math.Round(Bounds.Height * scale));
        gl.BindFramebuffer(GL_FRAMEBUFFER, fb); gl.Viewport(0, 0, width, height);
        gl.Disable(GL_SCISSOR_TEST); gl.Disable(GL_CULL_FACE); gl.DepthMask(1);
        gl.ClearColor(.125f, .174f, .18f, 1); gl.ClearDepth(1); gl.Clear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
        if (vertices.Length == 0) return;
        gl.UseProgram(shader);
        if (array != 0) gl.BindVertexArray(array);
        gl.BindBuffer(GL_ARRAY_BUFFER, buffer);
        if (upload)
        {
            fixed (Vertex* data = vertices) gl.BufferData(GL_ARRAY_BUFFER, vertices.Length * sizeof(Vertex), (nint)data, GL_STATIC_DRAW);
            upload = false;
        }
        gl.VertexAttribPointer(0, 3, GL_FLOAT, 0, sizeof(Vertex), 0);
        gl.VertexAttribPointer(1, 3, GL_FLOAT, 0, sizeof(Vertex), sizeof(float) * 3);
        gl.EnableVertexAttribArray(0); gl.EnableVertexAttribArray(1);
        var direction = new Vector3(MathF.Sin(yaw)*MathF.Cos(pitch), MathF.Sin(pitch), MathF.Cos(yaw)*MathF.Cos(pitch));
        var mvp = Matrix4x4.CreateLookAt(focus + direction * radius, focus, Vector3.UnitY)
            * Matrix4x4.CreatePerspectiveFieldOfView(.75f, (float)width / height, .03f, 10000);
        gl.UniformMatrix4fv(matrixLocation, 1, false, &mvp);
        gl.Enable(GL_DEPTH_TEST);
        gl.DrawArrays(GL_TRIANGLES, 0, solidCount);
        gl.Disable(GL_DEPTH_TEST);
        gl.DrawArrays(GL_TRIANGLES, solidCount, vertices.Length - solidCount);
        Frames++;
    }
    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        if (array != 0) gl.DeleteVertexArray(array);
        gl.DeleteBuffer(buffer); gl.DeleteProgram(shader);
        array = buffer = shader = 0;
    }
    protected override void OnOpenGlLost() { array = buffer = shader = 0; upload = true; }
}
