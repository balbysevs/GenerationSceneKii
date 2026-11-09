namespace SceneGenerator.Core.Geometry;

public class Point3D
{
    public double X { get; }
    public double Y { get; }
    public double Z { get; }
    
    /// <summary>Создаёт пространственную точку с конечными координатами X, Y и Z.</summary>
    public Point3D(double x, double y, double z)
        {
        if (!double.IsFinite(x)) throw new ArgumentOutOfRangeException(nameof(x));
        if (!double.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(y));
        if (!double.IsFinite(z)) throw new ArgumentOutOfRangeException(nameof(z));
        X = x;
        Y = y;
        Z = z;
        }
}
