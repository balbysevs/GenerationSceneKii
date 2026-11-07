using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SceneGenerator.Core.Geometry;
using SceneGenerator.Core.Models;

namespace SceneGenerator.Desktop.Controls.Rendering;


internal sealed class Scene3DRenderer : IDisposable
{
    private Scene? _scene;
    private SceneProjection3D _projection;
    private Color _kiiColor, _obstacleColor;
    private int _objectCount;
    private int _width, _height;
    private double _scaleX, _scaleY;
    private int[] _pixels = [], _displayPixels = [], _owners = [];
    private float[] _depth = [];
    private WriteableBitmap? _bitmap;
    private bool _dirty;
    private int _highlighted = -2;

    public void Update(Scene scene, SceneProjection3D projection, Color kiiColor, Color obstacleColor)
    {
        if (ReferenceEquals(scene, _scene) && scene.Objects.Count == _objectCount &&
            projection == _projection && kiiColor == _kiiColor && obstacleColor == _obstacleColor && _bitmap is not null) return;
        _scene = scene; _projection = projection; _kiiColor = kiiColor; _obstacleColor = obstacleColor;
        _objectCount = scene.Objects.Count;
        double resolution = Math.Min(2, Math.Min(1600 / projection.Plot.Width, 1200 / projection.Plot.Height));
        int width = Math.Max(1, (int)Math.Ceiling(projection.Plot.Width * resolution));
        int height = Math.Max(1, (int)Math.Ceiling(projection.Plot.Height * resolution));
        if (_bitmap is null || width != _width || height != _height)
        {
            _bitmap?.Dispose(); _width = width; _height = height;
            _bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96),
                PixelFormat.Bgra8888, AlphaFormat.Premul);
            _pixels = new int[width * height]; _displayPixels = new int[_pixels.Length];
            _depth = new float[_pixels.Length]; _owners = new int[_pixels.Length];
        }
        _scaleX = width / projection.Plot.Width; _scaleY = height / projection.Plot.Height;
        Array.Clear(_pixels); Array.Fill(_depth, float.NegativeInfinity); Array.Fill(_owners, -1);
        for (int index = 0; index < scene.Objects.Count; index++) DrawObject(scene.Objects[index], index);
        for (int index = 0; index < scene.Objects.Count; index++) DrawMarker(scene.Objects[index], index);
        _dirty = true;
    }

    public SceneObject? FindObjectAt(Point screen)
    {
        if (_scene is null || !_projection.Plot.Contains(screen)) return null;
        int x = (int)((screen.X - _projection.Plot.X) * _scaleX);
        int y = (int)((screen.Y - _projection.Plot.Y) * _scaleY);
        if (x < 0 || y < 0 || x >= _width || y >= _height) return null;
        int index = _owners[y * _width + x];
        return index < 0 ? null : _scene.Objects[index];
    }

    public bool TryGetHitPoint(Point screen, out Point3D point)
    {
        point = null!;
        if (FindObjectAt(screen) is null) return false;
        int x = (int)((screen.X - _projection.Plot.X) * _scaleX);
        int y = (int)((screen.Y - _projection.Plot.Y) * _scaleY);
        point = _projection.Unproject(screen, _depth[y * _width + x]);
        return true;
    }

    public WriteableBitmap GetImage(SceneObject? hovered)
    {
        int highlighted = hovered is null || _scene is null ? -1 : IndexOf(hovered);
        if (_dirty || highlighted != _highlighted)
        {
            Array.Copy(_pixels, _displayPixels, _pixels.Length);
            if (highlighted >= 0)
            {
                int outline = Pack(_kiiColor, 1);
                for (int y = 0; y < _height; y++)
                for (int x = 0; x < _width; x++)
                {
                    int i = y * _width + x;
                    if (_owners[i] != highlighted) continue;
                    bool edge = x == 0 || y == 0 || x == _width - 1 || y == _height - 1 ||
                        _owners[i - 1] != highlighted || _owners[i + 1] != highlighted ||
                        _owners[i - _width] != highlighted || _owners[i + _width] != highlighted;
                    _displayPixels[i] = edge ? outline : Lighten(_pixels[i]);
                }
            }
            using var frame = _bitmap!.Lock();
            for (int y = 0; y < _height; y++)
                Marshal.Copy(_displayPixels, y * _width, IntPtr.Add(frame.Address, y * frame.RowBytes), _width);
            _dirty = false; _highlighted = highlighted;
        }
        return _bitmap!;
    }

    private int IndexOf(SceneObject item)
    {
        for (int i = 0; i < _scene!.Objects.Count; i++) if (ReferenceEquals(item, _scene.Objects[i])) return i;
        return -1;
    }

    private Vertex Project(Point2D point, double z)
    {
        Point screen = _projection.ToScreen(point.X, point.Y, z);
        return new Vertex((screen.X - _projection.Plot.X) * _scaleX,
            (screen.Y - _projection.Plot.Y) * _scaleY, _projection.Depth(point.X, point.Y, z));
    }

    private void DrawObject(SceneObject item, int owner)
    {
        PolygonPrism3D prism = item.Geometry;
        Color color = item.Type == SceneObjectType.Kii ? _kiiColor : _obstacleColor;
        IReadOnlyList<Point2D>[] rings = [prism.Footprint.Outer, .. prism.Footprint.Holes];
        double roof = prism.BaseElevation + prism.Height;
        var tops = rings.Select(ring => ring.Select(p => Project(p, roof)).ToArray()).ToArray();
        DrawRoof(tops, roof, owner, Pack(color, 1.08));
        int outline = Pack(color, .55);
        for (int ringIndex = 0; ringIndex < rings.Length; ringIndex++)
        {
            IReadOnlyList<Point2D> ring = rings[ringIndex];
            Vertex[] top = tops[ringIndex];
            for (int i = 0; i < ring.Count; i++)
            {
                int next = (i + 1) % ring.Count;
                Vertex a = Project(ring[i], prism.BaseElevation), b = Project(ring[next], prism.BaseElevation);
                double dx = ring[next].X - ring[i].X, dy = ring[next].Y - ring[i].Y;
                double light = Math.Max(0, (-.4 * dy + .6 * dx) / Math.Sqrt(dx * dx + dy * dy));
                int fill = Pack(color, .62 + .28 * light);
                DrawTriangle(a, b, top[next], owner, fill);
                DrawTriangle(a, top[next], top[i], owner, fill);
                DrawLine(a, b, owner, outline);
                DrawLine(a, top[i], owner, outline);
                DrawLine(top[i], top[next], owner, outline);
            }
        }
    }

    private void DrawRoof(Vertex[][] rings, double roofZ, int owner, int color)
    {
        double minY = rings.Min(r => r.Min(v => v.Y)), maxY = rings.Max(r => r.Max(v => v.Y));
        int start = Math.Max(0, (int)Math.Ceiling(minY - .5));
        int end = Math.Min(_height - 1, (int)Math.Floor(maxY - .5));
        var crossings = new List<double>();
        for (int y = start; y <= end; y++)
        {
            crossings.Clear();
            double py = y + .5;
            foreach (Vertex[] ring in rings)
            for (int i = 0; i < ring.Length; i++)
            {
                Vertex a = ring[i], b = ring[(i + 1) % ring.Length];
                if ((a.Y > py) != (b.Y > py)) crossings.Add(a.X + (py - a.Y) / (b.Y - a.Y) * (b.X - a.X));
            }
            crossings.Sort();
            double depth = _projection.DepthOnHorizontalPlane(_projection.Plot.Y + py / _scaleY, roofZ);
            for (int i = 0; i + 1 < crossings.Count; i += 2)
            {
                int left = Math.Max(0, (int)Math.Ceiling(crossings[i] - .5));
                int right = Math.Min(_width - 1, (int)Math.Floor(crossings[i + 1] - .5));
                for (int x = left; x <= right; x++) Pixel(x, y, depth, owner, color);
            }
        }
    }

    private void DrawTriangle(Vertex a, Vertex b, Vertex c, int owner, int color)
    {
        double area = Cross(a, b, c.X, c.Y);
        if (Math.Abs(area) < 1e-9) return;
        int left = Math.Max(0, (int)Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X))));
        int right = Math.Min(_width - 1, (int)Math.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))));
        int top = Math.Max(0, (int)Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))));
        int bottom = Math.Min(_height - 1, (int)Math.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))));
        if (left > right || top > bottom) return;

        double waStep = -(c.Y - b.Y) / area;
        double wbStep = -(a.Y - c.Y) / area;
        double wcStep = -waStep - wbStep;
        double depthStep = waStep * (a.Depth - c.Depth) + wbStep * (b.Depth - c.Depth);
        for (int y = top; y <= bottom; y++)
        {
            double wa = Cross(b, c, .5, y + .5) / area;
            double wb = Cross(c, a, .5, y + .5) / area;
            double first = left, last = right;

            if (!ClipWeight(wa, waStep, ref first, ref last) ||
                !ClipWeight(wb, wbStep, ref first, ref last) ||
                !ClipWeight(1 - wa - wb, wcStep, ref first, ref last)) continue;
            int start = (int)Math.Ceiling(first), end = (int)Math.Floor(last);
            double depth = (wa + waStep * start) * (a.Depth - c.Depth) +
                (wb + wbStep * start) * (b.Depth - c.Depth) + c.Depth;
            for (int x = start; x <= end; x++, depth += depthStep)
                Pixel(x, y, depth, owner, color);
        }
    }

    private static bool ClipWeight(double value, double step, ref double first, ref double last)
    {
        if (step == 0) return value >= -1e-9;
        double crossing = (-1e-9 - value) / step;
        if (step > 0) first = Math.Max(first, crossing);
        else last = Math.Min(last, crossing);
        return first <= last;
    }

    private static double Cross(Vertex a, Vertex b, double x, double y) => (b.X - a.X) * (y - a.Y) - (b.Y - a.Y) * (x - a.X);

    private void DrawLine(Vertex a, Vertex b, int owner, int color)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double t0 = 0, t1 = 1;
        if (!Clip(-dx, a.X, ref t0, ref t1) || !Clip(dx, _width - 1 - a.X, ref t0, ref t1) ||
            !Clip(-dy, a.Y, ref t0, ref t1) || !Clip(dy, _height - 1 - a.Y, ref t0, ref t1)) return;
        Vertex start = Lerp(a, b, t0), end = Lerp(a, b, t1);
        int steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y))));
        double step = 1.0 / steps;
        for (int i = 0; i <= steps; i++)
        {
            Vertex p = Lerp(start, end, i * step);
            int x = (int)Math.Round(p.X), y = (int)Math.Round(p.Y);
            if (x >= 0 && x < _width && y >= 0 && y < _height) Pixel(x, y, p.Depth + .03, owner, color);
        }
    }

    private static bool Clip(double p, double q, ref double min, ref double max)
    {
        if (Math.Abs(p) < 1e-12) return q >= 0;
        double t = q / p;
        if (p < 0) min = Math.Max(min, t); else max = Math.Min(max, t);
        return min <= max;
    }

    private static Vertex Lerp(Vertex a, Vertex b, double t) => new(a.X + (b.X - a.X) * t,
        a.Y + (b.Y - a.Y) * t, a.Depth + (b.Depth - a.Depth) * t);

    private void DrawMarker(SceneObject item, int owner)
    {
        if (Math.Max(item.Geometry.Size.Length, item.Geometry.Size.Width) * _projection.Scale >= 5) return;
        Point2D center = item.Geometry.Footprint.GetCenterPoint();
        Vertex p = Project(center, item.Geometry.BaseElevation + item.Geometry.Height);
        int cx = (int)p.X, cy = (int)p.Y;
        if (cx < 0 || cy < 0 || cx >= _width || cy >= _height) return;
        int centerIndex = cy * _width + cx;
        if (_owners[centerIndex] != owner && _depth[centerIndex] > p.Depth + .04) return;
        double radius = 2.5 * Math.Min(_scaleX, _scaleY);
        int color = Pack(item.Type == SceneObjectType.Kii ? _kiiColor : _obstacleColor, 1.08);
        for (int y = Math.Max(0, (int)(p.Y - radius)); y <= Math.Min(_height - 1, (int)Math.Ceiling(p.Y + radius)); y++)
        for (int x = Math.Max(0, (int)(p.X - radius)); x <= Math.Min(_width - 1, (int)Math.Ceiling(p.X + radius)); x++)
            if ((x + .5 - p.X) * (x + .5 - p.X) + (y + .5 - p.Y) * (y + .5 - p.Y) <= radius * radius)
                Pixel(x, y, p.Depth, owner, color);
    }

    private void Pixel(int x, int y, double depth, int owner, int color)
    {
        int index = y * _width + x;
        if (depth < _depth[index]) return;
        _depth[index] = (float)depth; _owners[index] = owner; _pixels[index] = color;
    }

    private static int Pack(Color color, double shade)
    {
        int r = (int)Math.Clamp(Math.Round(color.R * shade), 0, 255) * color.A / 255;
        int g = (int)Math.Clamp(Math.Round(color.G * shade), 0, 255) * color.A / 255;
        int b = (int)Math.Clamp(Math.Round(color.B * shade), 0, 255) * color.A / 255;
        return unchecked((int)((uint)color.A << 24 | (uint)r << 16 | (uint)g << 8 | (uint)b));
    }

    private static int Lighten(int pixel)
    {
        uint p = unchecked((uint)pixel); uint a = p >> 24;
        uint r = (uint)(((p >> 16) & 255) * .8 + a * .2);
        uint g = (uint)(((p >> 8) & 255) * .8 + a * .2);
        uint b = (uint)((p & 255) * .8 + a * .2);
        return unchecked((int)(a << 24 | r << 16 | g << 8 | b));
    }

    public void Dispose()
    {
        _bitmap?.Dispose(); _bitmap = null; _scene = null;
        _pixels = []; _displayPixels = []; _depth = []; _owners = [];
    }

    private readonly record struct Vertex(double X, double Y, double Depth);
}
