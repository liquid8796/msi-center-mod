using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace MsiCenterMod.Controls;

/// <summary>
/// Vòng tròn hiển thị phần trăm (0–100) kiểu MSI Center: vòng nền mờ + cung màu accent
/// bắt đầu từ đỉnh, chạy theo chiều kim đồng hồ. Text ở giữa do XAML overlay đảm nhiệm.
/// </summary>
public sealed class DonutGauge : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(DonutGauge),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RingThicknessProperty = DependencyProperty.Register(
        nameof(RingThickness), typeof(double), typeof(DonutGauge),
        new FrameworkPropertyMetadata(12d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(Brush), typeof(DonutGauge),
        new FrameworkPropertyMetadata(Brushes.DimGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillBrushProperty = DependencyProperty.Register(
        nameof(FillBrush), typeof(Brush), typeof(DonutGauge),
        new FrameworkPropertyMetadata(Brushes.OrangeRed, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Giá trị 0–100.</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double RingThickness
    {
        get => (double)GetValue(RingThicknessProperty);
        set => SetValue(RingThicknessProperty, value);
    }

    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public Brush FillBrush
    {
        get => (Brush)GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= RingThickness * 2)
        {
            return;
        }

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        double radius = (size - RingThickness) / 2;

        dc.DrawEllipse(null, new Pen(TrackBrush, RingThickness), center, radius, radius);

        double fraction = Math.Clamp(Value, 0, 100) / 100.0;
        if (fraction < 0.002)
        {
            return;
        }

        var fillPen = new Pen(FillBrush, RingThickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };

        if (fraction > 0.998)
        {
            dc.DrawEllipse(null, fillPen, center, radius, radius);
            return;
        }

        double sweepDegrees = fraction * 360;
        Point start = PointOnCircle(center, radius, -90);
        Point end = PointOnCircle(center, radius, -90 + sweepDegrees);

        var geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(start, isFilled: false, isClosed: false);
            ctx.ArcTo(end, new Size(radius, radius), 0,
                isLargeArc: sweepDegrees > 180, SweepDirection.Clockwise,
                isStroked: true, isSmoothJoin: false);
        }

        geometry.Freeze();
        dc.DrawGeometry(null, fillPen, geometry);
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        double radians = angleDegrees * Math.PI / 180;
        return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }
}
