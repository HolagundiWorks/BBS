using System.Globalization;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BBSApp.Services;

/// <summary>Simple dimensioned box sketches for BOQ / estimate PDFs (SVG).</summary>
public static class SketchPdf
{
    public static void DrawMeasurementSketch(
        IContainer container,
        string title,
        double lengthM,
        double breadthM,
        double heightM,
        double areaM2,
        double volumeM3,
        string unit)
    {
        container.Column(col =>
        {
            col.Spacing(2);
            col.Item().Text(title).SemiBold().FontSize(8);
            col.Item().Height(72).Svg(size => BuildSvg(size.Width, size.Height, lengthM, breadthM, heightM, areaM2, volumeM3, unit));
            var bits = new List<string>();
            if (lengthM > 0) bits.Add($"L={Fmt(lengthM)} m");
            if (breadthM > 0) bits.Add($"B={Fmt(breadthM)} m");
            if (heightM > 0) bits.Add($"H={Fmt(heightM)} m");
            if (areaM2 > 0) bits.Add($"A={Fmt(areaM2)} m²");
            if (volumeM3 > 0) bits.Add($"V={Fmt(volumeM3)} m³");
            if (bits.Count > 0)
                col.Item().Text(string.Join(" · ", bits)).FontSize(7).FontColor(Colors.Grey.Darken1);
        });
    }

    public static void DrawEstimateSketches(IContainer container, IEnumerable<EstimateLine> lines, int max = 24)
    {
        var list = lines
            .Where(l => l.LengthM > 0 || l.BreadthM > 0 || l.HeightM > 0 || l.AreaM2 > 0 || l.VolumeM3 > 0)
            .Take(max)
            .ToList();
        if (list.Count == 0) return;

        container.Column(col =>
        {
            col.Spacing(8);
            col.Item().Text("IV. Measurement sketches").SemiBold().FontSize(11);
            col.Item().Text("Dimensioned sketches for civil / finish items (first " + list.Count + ").")
                .FontSize(7).FontColor(Colors.Grey.Darken1);

            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.RelativeColumn();
                    c.RelativeColumn();
                    c.RelativeColumn();
                });
                int i = 0;
                foreach (var l in list)
                {
                    string title = string.IsNullOrWhiteSpace(l.Mark)
                        ? $"{l.Category}: {l.Description}"
                        : $"{l.Mark} · {l.Category}";
                    if (title.Length > 48) title = title[..45] + "…";
                    t.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4)
                        .Element(c => DrawMeasurementSketch(c, title, l.LengthM, l.BreadthM, l.HeightM, l.AreaM2, l.VolumeM3, l.Unit));
                    i++;
                }
                while (i % 3 != 0)
                {
                    t.Cell();
                    i++;
                }
            });
        });
    }

    public static void DrawCivilBoqSketches(IContainer container, IEnumerable<CivilLine> lines, int max = 30)
    {
        var list = lines
            .Where(l => l.LengthM > 0 || l.BreadthM > 0 || l.HeightM > 0 || l.AreaM2 > 0 || l.VolumeM3 > 0)
            .Take(max)
            .ToList();
        if (list.Count == 0) return;

        container.Column(col =>
        {
            col.Spacing(8);
            col.Item().Text("Measurement sketches").SemiBold().FontSize(11);
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.RelativeColumn();
                    c.RelativeColumn();
                    c.RelativeColumn();
                });
                int i = 0;
                foreach (var l in list)
                {
                    string title = $"{l.Mark} · {l.Element}";
                    t.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4)
                        .Element(c => DrawMeasurementSketch(c, title, l.LengthM, l.BreadthM, l.HeightM, l.AreaM2, l.VolumeM3, l.Unit));
                    i++;
                }
                while (i % 3 != 0)
                {
                    t.Cell();
                    i++;
                }
            });
        });
    }

    private static string Fmt(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static string BuildSvg(
        float width, float height,
        double lengthM, double breadthM, double heightM,
        double areaM2, double volumeM3, string unit)
    {
        double L = lengthM > 0 ? lengthM : 1;
        double H = heightM > 0 ? heightM : (breadthM > 0 ? breadthM : 1);
        bool isArea = (unit?.Contains("m²") == true || unit?.Contains("Sqm", StringComparison.OrdinalIgnoreCase) == true
                       || areaM2 > 0 && volumeM3 <= 0);

        const double pad = 14;
        double availW = Math.Max(40, width - 2 * pad);
        double availH = Math.Max(28, height - 2 * pad - 8);
        double scale = Math.Min(availW / L, availH / H);
        double w = Math.Max(12, L * scale);
        double h = Math.Max(12, H * scale);
        double x = (width - w) / 2;
        double y = pad;

        string qty = isArea && areaM2 > 0
            ? $"{Fmt(areaM2)} m²"
            : volumeM3 > 0 ? $"{Fmt(volumeM3)} m³" : "";

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{width.ToString(CultureInfo.InvariantCulture)}' height='{height.ToString(CultureInfo.InvariantCulture)}'>");
        sb.Append($"<rect x='{Inv(x)}' y='{Inv(y)}' width='{Inv(w)}' height='{Inv(h)}' fill='#F0F4F8' stroke='#334155' stroke-width='1.2'/>");
        if (!string.IsNullOrEmpty(qty))
            sb.Append($"<text x='{Inv(x + w / 2)}' y='{Inv(y + h / 2 + 4)}' text-anchor='middle' font-size='10' fill='#0F766E' font-family='Calibri,sans-serif'>{Esc(qty)}</text>");
        sb.Append($"<text x='{Inv(x + w / 2)}' y='{Inv(y + h + 11)}' text-anchor='middle' font-size='8' fill='#64748B' font-family='Calibri,sans-serif'>L {Fmt(L)} m</text>");
        sb.Append($"<text x='{Inv(x + w + 4)}' y='{Inv(y + h / 2)}' font-size='8' fill='#64748B' font-family='Calibri,sans-serif'>H {Fmt(H)} m</text>");
        if (breadthM > 0 && !isArea)
            sb.Append($"<text x='{Inv(x + w / 2)}' y='{Inv(Math.Min(height - 2, y + h + 22))}' text-anchor='middle' font-size='7' fill='#94A3B8' font-family='Calibri,sans-serif'>B {Fmt(breadthM)} m</text>");
        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string Inv(double d) => d.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
