namespace SceneGenerator.Core.Geometry;

/// <summary>A simple outer ring with disjoint, strictly contained holes. No repeated closing points.</summary>
public sealed class Polygon2D
{
    public const double Tolerance = 1e-8;
    public IReadOnlyList<Point2D> Outer { get; }
    public IReadOnlyList<IReadOnlyList<Point2D>> Holes { get; }
    public double MinX { get; }
    public double MinY { get; }
    public double MaxX { get; }
    public double MaxY { get; }
    public double Area { get; }

    public Polygon2D(IEnumerable<Point2D> outer, IEnumerable<IEnumerable<Point2D>>? holes = null)
    {
        ArgumentNullException.ThrowIfNull(outer);
        var shell = ValidateRing(outer, counterclockwise: true);
        var inner = (holes ?? []).Select(r => ValidateRing(r, counterclockwise: false)).ToList();
        foreach (var hole in inner)
        {
            if (BoundariesMeet(shell, hole) || !InsideRing(shell, hole[0]))
                throw new ArgumentException("Внутренний контур должен строго находиться внутри внешнего.", nameof(holes));
        }
        for (int i = 0; i < inner.Count; i++)
        for (int j = i + 1; j < inner.Count; j++)
            if (BoundariesMeet(inner[i], inner[j]) || InsideRing(inner[i], inner[j][0]) || InsideRing(inner[j], inner[i][0]))
                throw new ArgumentException("Внутренние контуры не должны пересекаться или содержать друг друга.", nameof(holes));

        Outer = shell.AsReadOnly();
        Holes = inner.Select(r => (IReadOnlyList<Point2D>)r.AsReadOnly()).ToList().AsReadOnly();
        MinX = shell.Min(p => p.X); MinY = shell.Min(p => p.Y);
        MaxX = shell.Max(p => p.X); MaxY = shell.Max(p => p.Y);
        Area = SignedArea(shell) + inner.Sum(r => SignedArea(r));
        if (!double.IsFinite(Area) || Area <= Tolerance)
            throw new ArgumentException("Площадь многоугольника должна быть положительной и конечной.");
    }

    public bool Contains(Point2D point, bool includeBoundary = true)
    {
        ArgumentNullException.ThrowIfNull(point);
        if (Rings().Any(r => OnBoundary(r, point))) return includeBoundary;
        return InsideRing(Outer, point) && !Holes.Any(r => InsideRing(r, point));
    }

    public bool IsOnOuterBoundary(Point2D point, double tolerance = Tolerance)
    {
        ArgumentNullException.ThrowIfNull(point);
        return Edges(Outer).Any(e => PointSegmentDistance(point, e.A, e.B) <= tolerance);
    }

    /// <summary>Tests overlap of positive area; shared edges and vertices are not intersections.</summary>
    public bool Intersects(Polygon2D other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (MaxX <= other.MinX || other.MaxX <= MinX || MaxY <= other.MinY || other.MaxY <= MinY) return false;
        // Between consecutive vertex/crossing levels, all boundary edges have a fixed order.
        var levels = Rings().Concat(other.Rings()).SelectMany(r => r).Select(p => p.Y).ToList();
        foreach (var a in AllEdges())
        foreach (var b in other.AllEdges())
        {
            double rx = a.B.X - a.A.X, ry = a.B.Y - a.A.Y;
            double sx = b.B.X - b.A.X, sy = b.B.Y - b.A.Y;
            double denominator = rx * sy - ry * sx;
            if (denominator == 0) continue;
            double dx = b.A.X - a.A.X, dy = b.A.Y - a.A.Y;
            double t = (dx * sy - dy * sx) / denominator;
            double u = (dx * ry - dy * rx) / denominator;
            if (t >= 0 && t <= 1 && u >= 0 && u <= 1) levels.Add(a.A.Y + t * ry);
        }
        var sorted = levels.Distinct().Order().ToArray();
        for (int i = 0; i + 1 < sorted.Length; i++)
        {
            if (sorted[i + 1] - sorted[i] <= Tolerance) continue;
            double y = sorted[i] + (sorted[i + 1] - sorted[i]) / 2;
            foreach (var a in Intervals(y))
            foreach (var b in other.Intervals(y))
                if (Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left) > Tolerance) return true;
        }
        return false;
    }

    public double DistanceTo(Polygon2D other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (Intersects(other)) return 0;
        double distance = double.PositiveInfinity;
        foreach (var a in AllEdges())
        foreach (var b in other.AllEdges())
        {
            if (SegmentsMeet(a.A, a.B, b.A, b.B)) return 0;
            distance = Math.Min(distance, Math.Min(
                Math.Min(PointSegmentDistance(a.A, b.A, b.B), PointSegmentDistance(a.B, b.A, b.B)),
                Math.Min(PointSegmentDistance(b.A, a.A, a.B), PointSegmentDistance(b.B, a.A, a.B))));
        }
        return distance;
    }

    /// <summary>Area centroid if inside; otherwise a deterministic interior representative point.</summary>
    public Point2D GetCenterPoint()
    {
        double cx = 0, cy = 0, area2 = 0;
        foreach (var edge in AllEdges())
        {
            double cross = (edge.A.X - MinX) * (edge.B.Y - MinY) - (edge.B.X - MinX) * (edge.A.Y - MinY);
            area2 += cross;
            cx += (edge.A.X + edge.B.X - 2 * MinX) * cross;
            cy += (edge.A.Y + edge.B.Y - 2 * MinY) * cross;
        }
        var centroid = new Point2D(MinX + cx / (3 * area2), MinY + cy / (3 * area2));
        if (Contains(centroid, includeBoundary: false)) return centroid;
        var levels = Rings().SelectMany(r => r).Select(p => p.Y).Distinct().Order().ToArray();
        Point2D? result = null;
        double widest = 0;
        for (int i = 0; i + 1 < levels.Length; i++)
        {
            double y = levels[i] + (levels[i + 1] - levels[i]) / 2;
            foreach (var interval in Intervals(y))
                if (interval.Right - interval.Left > widest)
                {
                    widest = interval.Right - interval.Left;
                    result = new Point2D(interval.Left + widest / 2, y);
                }
        }
        return result ?? throw new InvalidOperationException("Не найдена внутренняя точка многоугольника.");
    }

    private IEnumerable<IReadOnlyList<Point2D>> Rings() => new[] { Outer }.Concat(Holes);
    private IEnumerable<(Point2D A, Point2D B)> AllEdges() => Rings().SelectMany(Edges);

    private IEnumerable<(double Left, double Right)> Intervals(double y)
    {
        var xs = new List<double>();
        foreach (var edge in AllEdges())
            if ((edge.A.Y > y) != (edge.B.Y > y))
                xs.Add(edge.A.X + (y - edge.A.Y) / (edge.B.Y - edge.A.Y) * (edge.B.X - edge.A.X));
        xs.Sort();
        for (int i = 0; i + 1 < xs.Count; i += 2) yield return (xs[i], xs[i + 1]);
    }

    private static List<Point2D> ValidateRing(IEnumerable<Point2D> ring, bool counterclockwise)
    {
        ArgumentNullException.ThrowIfNull(ring);
        var points = ring.ToList();
        if (points.Count < 3 || points.Any(p => p is null) || points.Distinct().Count() != points.Count)
            throw new ArgumentException("Контур должен содержать минимум три различные точки без повторения первой в конце.");
        for (int i = 0; i < points.Count; i++)
        {
            var a = points[i]; var b = points[(i + 1) % points.Count]; var c = points[(i + 2) % points.Count];
            if (Math.Abs(Cross(a, b, c)) <= Tolerance &&
                (a.X - b.X) * (c.X - b.X) + (a.Y - b.Y) * (c.Y - b.Y) > 0)
                throw new ArgumentException("Контур содержит возврат по одному ребру.");
            for (int j = i + 1; j < points.Count; j++)
            {
                if (j == i + 1 || (i == 0 && j == points.Count - 1)) continue;
                if (SegmentsMeet(a, b, points[j], points[(j + 1) % points.Count]))
                    throw new ArgumentException("Самопересекающиеся контуры недопустимы.");
            }
        }
        double area = SignedArea(points);
        if (!double.IsFinite(area) || Math.Abs(area) <= Tolerance)
            throw new ArgumentException("Контур должен иметь ненулевую конечную площадь.");
        if ((area > 0) != counterclockwise) points.Reverse();
        return points;
    }

    private static double SignedArea(IReadOnlyList<Point2D> ring)
    {
        var origin = ring[0];
        return Edges(ring).Sum(e => (e.A.X - origin.X) * (e.B.Y - origin.Y) - (e.B.X - origin.X) * (e.A.Y - origin.Y)) / 2;
    }

    private static IEnumerable<(Point2D A, Point2D B)> Edges(IReadOnlyList<Point2D> ring)
    {
        for (int i = 0; i < ring.Count; i++) yield return (ring[i], ring[(i + 1) % ring.Count]);
    }

    private static bool BoundariesMeet(IReadOnlyList<Point2D> a, IReadOnlyList<Point2D> b) =>
        Edges(a).Any(x => Edges(b).Any(y => SegmentsMeet(x.A, x.B, y.A, y.B)));

    private static bool InsideRing(IReadOnlyList<Point2D> ring, Point2D point)
    {
        bool inside = false;
        foreach (var edge in Edges(ring))
            if ((edge.A.Y > point.Y) != (edge.B.Y > point.Y) &&
                point.X < edge.A.X + (point.Y - edge.A.Y) / (edge.B.Y - edge.A.Y) * (edge.B.X - edge.A.X))
                inside = !inside;
        return inside;
    }

    private static bool OnBoundary(IReadOnlyList<Point2D> ring, Point2D p) =>
        Edges(ring).Any(e => PointSegmentDistance(p, e.A, e.B) <= Tolerance);

    private static double Cross(Point2D a, Point2D b, Point2D c) =>
        (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    private static bool SegmentsMeet(Point2D a, Point2D b, Point2D c, Point2D d)
    {
        double abC = Cross(a, b, c), abD = Cross(a, b, d), cdA = Cross(c, d, a), cdB = Cross(c, d, b);
        if (((abC > 0 && abD < 0) || (abC < 0 && abD > 0)) &&
            ((cdA > 0 && cdB < 0) || (cdA < 0 && cdB > 0))) return true;
        return PointSegmentDistance(a, c, d) <= Tolerance || PointSegmentDistance(b, c, d) <= Tolerance ||
               PointSegmentDistance(c, a, b) <= Tolerance || PointSegmentDistance(d, a, b) <= Tolerance;
    }

    private static double PointSegmentDistance(Point2D p, Point2D a, Point2D b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy), 0, 1);
        return double.Hypot(p.X - (a.X + t * dx), p.Y - (a.Y + t * dy));
    }
}
