using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace AIGamingOptimizer.Controls;

public sealed class CircularGauge : FrameworkElement
{
    public static readonly DependencyProperty PercentProperty =
        DependencyProperty.Register(nameof(Percent), typeof(double), typeof(CircularGauge),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(CircularGauge),
            new FrameworkPropertyMetadata("CPU", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AccentProperty =
        DependencyProperty.Register(nameof(Accent), typeof(Brush), typeof(CircularGauge),
            new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(14, 165, 233)),
                FrameworkPropertyMetadataOptions.AffectsRender));

    public double Percent
    {
        get => (double)GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public Brush Accent
    {
        get => (Brush)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size < 8)
        {
            return;
        }

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radius = size / 2 - 10;
        var penBg = new Pen(new SolidColorBrush(Color.FromRgb(42, 46, 58)), 9) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var penFg = new Pen(Accent, 9) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawEllipse(null, penBg, center, radius, radius);

        var pct = Math.Clamp(Percent, 0, 100);
        if (pct > 0.4)
        {
            var geo = BuildArc(center, radius, -90, pct / 100d * 359.9);
            dc.DrawGeometry(null, penFg, geo);
        }

        var value = new FormattedText(
            $"{pct:0}",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            22,
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        var caption = new FormattedText(
            Label,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal),
            11,
            new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        dc.DrawText(value, new Point(center.X - value.Width / 2, center.Y - value.Height / 2 - 6));
        dc.DrawText(caption, new Point(center.X - caption.Width / 2, center.Y + 10));
    }

    private static Geometry BuildArc(Point center, double radius, double startDeg, double sweepDeg)
    {
        var start = Polar(center, radius, startDeg);
        var end = Polar(center, radius, startDeg + sweepDeg);
        var large = sweepDeg > 180;
        var figure = new PathFigure(start, [new ArcSegment(end, new Size(radius, radius), 0, large, SweepDirection.Clockwise, true)], false);
        return new PathGeometry([figure]);
    }

    private static Point Polar(Point center, double radius, double deg)
    {
        var rad = deg * Math.PI / 180.0;
        return new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));
    }
}
