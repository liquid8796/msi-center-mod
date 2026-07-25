namespace MsiCenterMod.Models;

/// <summary>
/// Đường cong quạt 6 điểm: tốc độ (%) tại 6 ngưỡng nhiệt độ do EC định nghĩa.
/// Tốc độ hợp lệ trong khoảng 0–150 (giống dải Advanced của MSI Center).
/// </summary>
public sealed class FanCurve
{
    public const int PointCount = 6;
    public const int MinSpeed = 0;
    public const int MaxSpeed = 150;

    /// <summary>Tốc độ quạt (%) tại từng điểm.</summary>
    public int[] Speeds { get; }

    /// <summary>Ngưỡng nhiệt độ (°C) tương ứng, đọc từ EC (chỉ để hiển thị).</summary>
    public int[] Temperatures { get; }

    public FanCurve(int[] speeds, int[] temperatures)
    {
        ArgumentNullException.ThrowIfNull(speeds);
        ArgumentNullException.ThrowIfNull(temperatures);
        if (speeds.Length != PointCount || temperatures.Length != PointCount)
        {
            throw new ArgumentException($"Đường cong quạt phải có đúng {PointCount} điểm.");
        }

        Speeds = speeds.Select(Clamp).ToArray();
        Temperatures = (int[])temperatures.Clone();
    }

    public static int Clamp(int speed) => Math.Clamp(speed, MinSpeed, MaxSpeed);
}
