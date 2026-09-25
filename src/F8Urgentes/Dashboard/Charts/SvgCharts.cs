using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Html;

namespace F8Urgentes.Dashboard.Charts;

public sealed record ChartSlice(string Label, double Value, string Color);

public sealed record ChartSeries(string Name, string Color, IReadOnlyList<double> Values);

/// <summary>
/// Server-rendered inline SVG charts for the Estadisticas page. No JS/CDN on purpose: the
/// dashboard runs on the municipal intranet and must also print cleanly. Every mark carries
/// a &lt;title&gt; so hovering shows the exact value.
/// </summary>
public static class SvgCharts
{
    private static readonly CultureInfo Ic = CultureInfo.InvariantCulture;

    public static readonly string[] Palette =
        ["#2563eb", "#059669", "#d97706", "#7c3aed", "#db2777", "#0891b2", "#65a30d", "#dc2626", "#475569"];

    private static string F(double v) => v.ToString("0.##", Ic);
    private static string E(string s) => WebUtility.HtmlEncode(s);

    /// <summary>Pie (or donut when <paramref name="centerText"/> is given) with % labels and legend.</summary>
    public static IHtmlContent Pie(IReadOnlyList<ChartSlice> slices, string? centerText = null, string? centerSub = null)
    {
        var total = slices.Sum(s => s.Value);
        if (total <= 0) return Empty("Sin datos todavía.");

        const double cx = 110, cy = 110, r = 100;
        var inner = centerText is null ? 0 : 58;
        var sb = new StringBuilder();
        sb.Append("<div class=\"pie-wrap\"><svg viewBox=\"0 0 220 220\" class=\"pie-chart\" role=\"img\">");

        var angle = -Math.PI / 2;
        foreach (var s in slices.Where(s => s.Value > 0))
        {
            var frac = s.Value / total;
            var tip = $"{E(s.Label)}: {F(s.Value)} ({(frac * 100).ToString("0.#", Ic)}%)";
            if (frac >= 0.9999)
            {
                sb.Append($"<circle cx=\"{F(cx)}\" cy=\"{F(cy)}\" r=\"{F(r)}\" fill=\"{s.Color}\" class=\"pie-slice\"><title>{tip}</title></circle>");
            }
            else
            {
                var end = angle + frac * 2 * Math.PI;
                var large = frac > 0.5 ? 1 : 0;
                sb.Append($"<path d=\"M{F(cx)},{F(cy)} L{F(cx + r * Math.Cos(angle))},{F(cy + r * Math.Sin(angle))} A{F(r)},{F(r)} 0 {large} 1 {F(cx + r * Math.Cos(end))},{F(cy + r * Math.Sin(end))} Z\" fill=\"{s.Color}\" class=\"pie-slice\"><title>{tip}</title></path>");
            }

            // Donuts already say the headline number in the hole; slice labels there are noise.
            if (inner == 0 && frac >= 0.06)
            {
                var mid = angle + frac * Math.PI;
                var lr = frac >= 0.9999 ? 0 : r * 0.62;
                sb.Append($"<text x=\"{F(cx + lr * Math.Cos(mid))}\" y=\"{F(cy + lr * Math.Sin(mid))}\" class=\"pie-label\" text-anchor=\"middle\" dominant-baseline=\"central\">{(frac * 100).ToString("0", Ic)}%</text>");
            }
            angle += frac * 2 * Math.PI;
        }

        if (inner > 0)
        {
            sb.Append($"<circle cx=\"{F(cx)}\" cy=\"{F(cy)}\" r=\"{inner}\" class=\"pie-hole\" />");
            sb.Append($"<text x=\"{F(cx)}\" y=\"{F(cy - (centerSub is null ? 0 : 8))}\" class=\"pie-center\" text-anchor=\"middle\" dominant-baseline=\"central\">{E(centerText!)}</text>");
            if (centerSub is not null)
                sb.Append($"<text x=\"{F(cx)}\" y=\"{F(cy + 16)}\" class=\"pie-center-sub\" text-anchor=\"middle\">{E(centerSub)}</text>");
        }
        sb.Append("</svg><ul class=\"chart-legend\">");
        foreach (var s in slices)
        {
            var pct = total > 0 ? s.Value * 100 / total : 0;
            sb.Append($"<li><i style=\"background:{s.Color}\"></i><span class=\"legend-label\">{E(s.Label)}</span><b>{F(s.Value)}</b><span class=\"legend-pct\">{pct.ToString("0.#", Ic)}%</span></li>");
        }
        sb.Append("</ul></div>");
        return new HtmlString(sb.ToString());
    }

    /// <summary>Vertical bar chart (also used as histogram with <paramref name="gapless"/>).</summary>
    public static IHtmlContent Bars(IReadOnlyList<(string Label, double Value)> items, string color = "#2563eb",
        Func<int, string?>? colorAt = null, bool gapless = false, string unit = "caso(s)")
    {
        if (items.Count == 0 || items.All(i => i.Value <= 0)) return Empty("Sin datos todavía.");
        return Grouped(items.Select(i => i.Label).ToList(),
            [new ChartSeries("", color, items.Select(i => i.Value).ToList())],
            stacked: false, colorAt: colorAt, gapless: gapless, unit: unit, legend: false);
    }

    /// <summary>Grouped (side by side) or stacked vertical bars for several series over the same categories.</summary>
    public static IHtmlContent Grouped(IReadOnlyList<string> categories, IReadOnlyList<ChartSeries> series,
        bool stacked = false, Func<int, string?>? colorAt = null, bool gapless = false, string unit = "caso(s)",
        bool legend = true, bool percentAxis = false, double w = 640)
    {
        var n = categories.Count;
        if (n == 0) return Empty("Sin datos todavía.");

        const double h = 230;
        const double padL = 38, padR = 10, padT = 18, padB = 34;
        var plotW = w - padL - padR;
        var plotH = h - padT - padB;
        var rawMax = stacked
            ? Enumerable.Range(0, n).Max(i => series.Sum(s => s.Values[i]))
            : series.Max(s => s.Values.DefaultIfEmpty(0).Max());
        if (rawMax <= 0) return Empty("Sin datos todavía.");
        var max = percentAxis ? 100 : NiceMax(rawMax);

        var sb = new StringBuilder();
        sb.Append($"<svg viewBox=\"0 0 {F(w)} {F(h)}\" class=\"bar-chart\" role=\"img\">");
        for (var g = 0; g <= 4; g++)
        {
            var gy = padT + plotH * g / 4;
            var val = max * (4 - g) / 4;
            sb.Append($"<line x1=\"{F(padL)}\" x2=\"{F(w - padR)}\" y1=\"{F(gy)}\" y2=\"{F(gy)}\" class=\"chart-gridline\" />");
            sb.Append($"<text x=\"{F(padL - 6)}\" y=\"{F(gy)}\" class=\"chart-axis-label\" text-anchor=\"end\" dominant-baseline=\"central\">{F(Math.Round(val, 1))}{(percentAxis ? "%" : "")}</text>");
        }

        var slot = plotW / n;
        var groupW = slot * (gapless ? 0.96 : 0.7);
        var barW = stacked ? groupW : groupW / series.Count;
        var labelEvery = n <= 14 ? 1 : (int)Math.Ceiling(n / 14.0);
        for (var i = 0; i < n; i++)
        {
            var x0 = padL + slot * i + (slot - groupW) / 2;
            var stackTop = padT + plotH;
            for (var s = 0; s < series.Count; s++)
            {
                var v = series[s].Values[i];
                var bh = plotH * v / max;
                var fill = colorAt?.Invoke(i) ?? series[s].Color;
                double x, y;
                if (stacked) { x = x0; y = stackTop - bh; stackTop = y; }
                else { x = x0 + barW * s; y = padT + plotH - bh; }
                var name = series[s].Name.Length > 0 ? $"{E(series[s].Name)} · " : "";
                sb.Append($"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(Math.Max(barW - 1, 1))}\" height=\"{F(bh)}\" fill=\"{fill}\" class=\"bar\"><title>{name}{E(categories[i])}: {F(v)}{(percentAxis ? "%" : " " + unit)}</title></rect>");
                if (!stacked && v > 0 && barW >= 14)
                    sb.Append($"<text x=\"{F(x + barW / 2)}\" y=\"{F(y - 4)}\" class=\"bar-value\" text-anchor=\"middle\">{F(Math.Round(v, 1))}{(percentAxis ? "%" : "")}</text>");
            }
            if (stacked)
            {
                var sum = series.Sum(s => s.Values[i]);
                if (sum > 0 && groupW >= 14)
                    sb.Append($"<text x=\"{F(x0 + groupW / 2)}\" y=\"{F(stackTop - 4)}\" class=\"bar-value\" text-anchor=\"middle\">{F(sum)}</text>");
            }
            if (i % labelEvery == 0)
                sb.Append($"<text x=\"{F(padL + slot * i + slot / 2)}\" y=\"{F(h - 12)}\" class=\"chart-axis-label\" text-anchor=\"middle\">{E(categories[i])}</text>");
        }
        sb.Append("</svg>");
        if (legend && series.Count > 1) sb.Append(Legend(series));
        return new HtmlString(sb.ToString());
    }

    /// <summary>Area + line trend with dots.</summary>
    public static IHtmlContent Line(IReadOnlyList<(string Label, double Value)> points, string color = "#2563eb", string unit = "caso(s)", double w = 640)
    {
        var n = points.Count;
        if (n == 0) return Empty("Sin datos todavía.");

        const double h = 220;
        const double padL = 38, padR = 14, padT = 16, padB = 30;
        var plotH = h - padT - padB;
        var max = NiceMax(Math.Max(1, points.Max(p => p.Value)));
        double X(int i) => n > 1 ? padL + (w - padL - padR) * i / (n - 1) : w / 2;
        double Y(double v) => padT + plotH * (1 - v / max);

        var sb = new StringBuilder();
        sb.Append($"<svg viewBox=\"0 0 {F(w)} {F(h)}\" class=\"bar-chart\" role=\"img\">");
        for (var g = 0; g <= 4; g++)
        {
            var gy = padT + plotH * g / 4;
            sb.Append($"<line x1=\"{F(padL)}\" x2=\"{F(w - padR)}\" y1=\"{F(gy)}\" y2=\"{F(gy)}\" class=\"chart-gridline\" />");
            sb.Append($"<text x=\"{F(padL - 6)}\" y=\"{F(gy)}\" class=\"chart-axis-label\" text-anchor=\"end\" dominant-baseline=\"central\">{F(Math.Round(max * (4 - g) / 4, 1))}</text>");
        }
        var line = string.Join(" ", points.Select((p, i) => $"{F(X(i))},{F(Y(p.Value))}"));
        sb.Append($"<polygon points=\"{F(X(0))},{F(padT + plotH)} {line} {F(X(n - 1))},{F(padT + plotH)}\" fill=\"{color}\" class=\"line-area\" />");
        sb.Append($"<polyline points=\"{line}\" fill=\"none\" stroke=\"{color}\" class=\"line-path\" />");
        var labelEvery = n <= 14 ? 1 : (int)Math.Ceiling(n / 14.0);
        for (var i = 0; i < n; i++)
        {
            sb.Append($"<circle cx=\"{F(X(i))}\" cy=\"{F(Y(points[i].Value))}\" r=\"3.5\" fill=\"{color}\" class=\"line-dot\"><title>{E(points[i].Label)}: {F(points[i].Value)} {unit}</title></circle>");
            if (i % labelEvery == 0)
                sb.Append($"<text x=\"{F(X(i))}\" y=\"{F(h - 10)}\" class=\"chart-axis-label\" text-anchor=\"middle\">{E(points[i].Label)}</text>");
        }
        sb.Append("</svg>");
        return new HtmlString(sb.ToString());
    }

    private static string Legend(IReadOnlyList<ChartSeries> series) =>
        "<ul class=\"chart-legend chart-legend-inline\">" +
        string.Concat(series.Select(s => $"<li><i style=\"background:{s.Color}\"></i><span class=\"legend-label\">{E(s.Name)}</span><b>{F(s.Values.Sum())}</b></li>")) +
        "</ul>";

    private static IHtmlContent Empty(string text) => new HtmlString($"<p class=\"empty-hint\">{E(text)}</p>");

    // Rounds the axis top up to 1/2/5 × 10^k so gridline ticks land on readable numbers.
    private static double NiceMax(double v)
    {
        var exp = Math.Pow(10, Math.Floor(Math.Log10(v)));
        var f = v / exp;
        var nice = f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10;
        var m = nice * exp;
        return m < 4 ? 4 : m;
    }
}
