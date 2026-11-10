using Avalonia;
using Avalonia.Media;

namespace Polhem.DefineEditor.Views;

/// <summary>The wavy line drawn under a problem in a text editor.</summary>
internal static class WavyLine
{
    /// <summary>Builds a wavy line along the bottom of a text run, from <paramref name="from"/> to <paramref name="right"/>.</summary>
    public static StreamGeometry Build(Point from, double right)
    {
        const double step = 2.5;
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(new Point(from.X, from.Y - 1), isFilled: false);
        var up = true;
        for (var x = from.X + step; x <= right + step; x += step, up = !up)
            context.LineTo(new Point(Math.Min(x, right), from.Y - (up ? 2.5 : 0)));
        context.EndFigure(isClosed: false);
        return geometry;
    }
}
