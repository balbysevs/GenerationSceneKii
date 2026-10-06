namespace SceneGenerator.Core.Configuration;

public class ObstacleSizeRanges
{
    public DoubleRange Length { get; }
    public DoubleRange Width { get; }
    public DoubleRange Height { get; }

    public ObstacleSizeRanges(
        DoubleRange length,
        DoubleRange width,
        DoubleRange height)
    {
        ArgumentNullException.ThrowIfNull(length);
        ArgumentNullException.ThrowIfNull(width);
        ArgumentNullException.ThrowIfNull(height);

        if (length.Min <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length), "Длина должна быть больше нуля.");
        }

        if (width.Min <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width), "Ширина должна быть больше нуля.");
        }

        if (height.Min < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(height), "Высота не может быть отрицательной.");
        }

        Length = length;
        Width = width;
        Height = height;
    }
}