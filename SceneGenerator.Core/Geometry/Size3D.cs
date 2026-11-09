namespace SceneGenerator.Core.Geometry;

public class Size3D
{
    public double Length { get;}
    public double Width { get;}
    public double Height { get;}

    /// <summary>Задаёт положительные длину и ширину и неотрицательную высоту; нулевая высота допустима для 2D.</summary>
    public Size3D(double length, double width, double height)
    {
        if (!double.IsFinite(length) || length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Длина должна быть положительным конечным числом.");
        }

        if (!double.IsFinite(width) || width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Ширина должна быть положительным конечным числом.");
        }

        if (!double.IsFinite(height) || height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height),
                "Высота должна быть неотрицательным конечным числом.");
        }
        Length = length;
        Width = width;
        Height = height;
    }
}