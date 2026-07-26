using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BBSApp.Services;

/// <summary>Steel arrangement sketches (column / beam sections) for BOQ and estimate PDFs.</summary>
public static class SketchPdf
{
    public static void DrawSteelArrangementSketches(IContainer container, ProjectStore store, int max = 36)
    {
        var items = new List<(string Title, Func<Size, string> Svg)>();

        void addColumnLike(string kindLabel, IEnumerable<Dictionary<string, string>> rows)
        {
            foreach (var r in rows)
            {
                if (items.Count >= max) return;
                string mark = Get(r, "mark", kindLabel);
                var snap = ColumnSnapshot(r);
                if (snap is null) continue;
                items.Add(($"{mark} · {kindLabel}", size => ColumnSectionSvg(size.Width, size.Height, snap)));
            }
        }

        void addBeamLike(string kindLabel, IEnumerable<Dictionary<string, string>> rows)
        {
            foreach (var r in rows)
            {
                if (items.Count >= max) return;
                string mark = Get(r, "mark", kindLabel);
                var snap = BeamSnapshot(r);
                if (snap is null) continue;
                items.Add(($"{mark} · {kindLabel}", size => BeamSectionSvg(size.Width, size.Height, snap)));
            }
        }

        addColumnLike("Column", store.Columns);
        addColumnLike("Pedestal", store.Pedestals);
        addBeamLike("Beam", store.Beams);
        addBeamLike("Lintel", store.Lintels);

        if (items.Count == 0)
        {
            container.Text("No column / beam steel sketches — add RCC members first.")
                .FontSize(8).FontColor(Colors.Grey.Darken1);
            return;
        }

        container.Column(col =>
        {
            col.Spacing(8);
            col.Item().Text("Steel arrangement sketches").SemiBold().FontSize(11);
            col.Item().Text("Cross-section detailing for columns and beams (ties / stirrups + main bars).")
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
                foreach (var (title, svg) in items)
                {
                    t.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Column(cell =>
                    {
                        cell.Item().Text(title).SemiBold().FontSize(8);
                        cell.Item().Height(110).Svg(size => svg(size));
                    });
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

    public static void DrawEstimateSketches(IContainer container, ProjectStore store) =>
        DrawSteelArrangementSketches(container, store);

    public static void DrawCivilBoqSketches(IContainer container, ProjectStore store) =>
        DrawSteelArrangementSketches(container, store);

    private sealed class ColumnSnap
    {
        public double B, D, Cover, TieDia;
        public ColumnArrangement Arr = null!;
        public string Caption = "";
    }

    private sealed class BeamSnap
    {
        public double B, D, Cover, StirDia;
        public int Legs;
        public List<int> Hangers = new();
        public List<int> Tops = new();
        public List<int> Bottoms = new();
        public string Caption = "";
    }

    private static ColumnSnap? ColumnSnapshot(Dictionary<string, string> r)
    {
        double B = Num(r, "width");
        double D = Num(r, "depth");
        string colType = Get(r, "column_type", "Rectangular");
        if (colType.Equals("Circular", StringComparison.OrdinalIgnoreCase))
        {
            if (B <= 0) B = Num(r, "depth");
            D = B;
        }
        else if (colType.Equals("Square", StringComparison.OrdinalIgnoreCase) && D <= 0)
            D = B;
        if (B < 50 || D < 50) return null;

        double cover = Num(r, "cover");
        if (cover <= 0) cover = 40;
        double tieDia = Num(r, "stirrup_dia");
        if (tieDia <= 0) tieDia = 8;
        string bars = Get(r, "bars", "16:4");
        string tieType = Get(r, "tie_type", "Closed");
        var arr = ColumnLayout.Arrange(B, D, cover, tieDia, bars, tieType, colType);
        return new ColumnSnap
        {
            B = B, D = D, Cover = cover, TieDia = tieDia, Arr = arr,
            Caption = $"{Fmt(B)}x{Fmt(D)} · c={Fmt(cover)} · tie {Fmt(tieDia)}"
        };
    }

    private static BeamSnap? BeamSnapshot(Dictionary<string, string> r)
    {
        double B = Num(r, "width");
        double D = Num(r, "depth");
        if (B < 50 || D < 50) return null;
        double cover = Num(r, "cover");
        if (cover <= 0) cover = 25;
        double stirDia = Num(r, "stirrup_dia");
        if (stirDia <= 0) stirDia = 8;
        int legs = (int)Num(r, "legs");
        if (legs < 2) legs = 2;

        var hangers = FlattenBars(Get(r, "hanger_bars", ""));
        var tops = FlattenBars(Get(r, "top_bars", ""));
        var bottoms = FlattenBars(Get(r, "bottom_bars", ""));
        if (hangers.Count == 0 && tops.Count == 0 && bottoms.Count == 0)
        {
            var bars = FlattenBars(Get(r, "bars", ""));
            if (bars.Count > 0) bottoms = bars;
            else
            {
                tops = new List<int> { 12, 12 };
                bottoms = new List<int> { 16, 16, 16 };
            }
        }

        return new BeamSnap
        {
            B = B, D = D, Cover = cover, StirDia = stirDia, Legs = legs,
            Hangers = hangers, Tops = tops, Bottoms = bottoms,
            Caption = $"{Fmt(B)}x{Fmt(D)} · c={Fmt(cover)} · {legs}-leg {Fmt(stirDia)}"
        };
    }

    private static string ColumnSectionSvg(float w, float h, ColumnSnap s)
    {
        double viewW = Math.Max(40, w);
        double viewH = Math.Max(40, h);
        double scale = Math.Min((viewW - 16) / s.B, (viewH - 20) / s.D);
        double ox = (viewW - s.B * scale) / 2;
        double oy = 12 + (viewH - 20 - s.D * scale) / 2;

        var sb = new StringBuilder();
        sb.Append(SvgOpen(viewW, viewH));
        sb.Append(Rect(ox, oy, s.B * scale, s.D * scale, "#F8FAFC", "#1E293B", 1.4));
        double inset = (s.Cover + s.TieDia / 2) * scale;
        double cageL = ox + inset, cageT = oy + inset;
        double cageW = Math.Max(4, s.B * scale - 2 * inset);
        double cageH = Math.Max(4, s.D * scale - 2 * inset);
        sb.Append(Rect(cageL, cageT, cageW, cageH, "none", "#0F766E", 1.1));
        foreach (var bp in s.Arr.Bars)
        {
            double pad = (bp.Dia / 2.0) * scale;
            double cx = cageL + pad + bp.U * (cageW - 2 * pad);
            double cy = cageT + pad + bp.V * (cageH - 2 * pad);
            double rr = Math.Max(1.5, (bp.Dia / 2.0) * scale);
            sb.Append(Circle(cx, cy, rr, BarFill(bp.Dia), "#0F172A"));
        }
        sb.Append(Text(viewW / 2, 10, s.Caption, 7, "#64748B"));
        sb.Append(SvgClose());
        return sb.ToString();
    }

    private static string BeamSectionSvg(float w, float h, BeamSnap s)
    {
        double viewW = Math.Max(40, w);
        double viewH = Math.Max(40, h);
        double scale = Math.Min((viewW - 20) / s.B, (viewH - 24) / s.D);
        double ox = (viewW - s.B * scale) / 2;
        double oy = 14 + (viewH - 24 - s.D * scale) / 2;

        var sb = new StringBuilder();
        sb.Append(SvgOpen(viewW, viewH));
        sb.Append(Rect(ox, oy, s.B * scale, s.D * scale, "#F8FAFC", "#1E293B", 1.4));
        double inset = (s.Cover + s.StirDia / 2) * scale;
        sb.Append(Rect(ox + inset, oy + inset, Math.Max(4, s.B * scale - 2 * inset), Math.Max(4, s.D * scale - 2 * inset),
            "none", "#0F766E", 1.1));
        if (s.Legs >= 4)
        {
            double mx = ox + s.B * scale / 2;
            sb.Append($"<line x1='{Inv(mx)}' y1='{Inv(oy + inset)}' x2='{Inv(mx)}' y2='{Inv(oy + s.D * scale - inset)}' stroke='#0F766E' stroke-width='0.9'/>");
        }

        void placeLayer(IReadOnlyList<int> dias, bool top, IReadOnlyList<int>? cornerDias)
        {
            if (dias.Count == 0 && (cornerDias is null || cornerDias.Count == 0)) return;
            var layer = new List<int>();
            if (cornerDias is { Count: > 0 })
            {
                layer.Add(cornerDias[0]);
                layer.AddRange(dias);
                layer.Add(cornerDias.Count > 1 ? cornerDias[1] : cornerDias[0]);
            }
            else
                layer.AddRange(dias);
            if (layer.Count == 0) return;
            int dMax = layer.Max();
            double y = top
                ? oy + (s.Cover + s.StirDia + dMax / 2.0) * scale
                : oy + s.D * scale - (s.Cover + s.StirDia + dMax / 2.0) * scale;
            double x0 = ox + (s.Cover + s.StirDia + dMax / 2.0) * scale;
            double x1 = ox + s.B * scale - (s.Cover + s.StirDia + dMax / 2.0) * scale;
            for (int i = 0; i < layer.Count; i++)
            {
                double t = layer.Count == 1 ? 0.5 : i / (double)(layer.Count - 1);
                double cx = x0 + t * (x1 - x0);
                double rr = Math.Max(1.5, (layer[i] / 2.0) * scale);
                sb.Append(Circle(cx, y, rr, BarFill(layer[i]), "#0F172A"));
            }
        }

        if (s.Hangers.Count > 0)
        {
            var corners = new List<int> { s.Hangers[0], s.Hangers.Count > 1 ? s.Hangers[1] : s.Hangers[0] };
            placeLayer(s.Tops, top: true, corners);
        }
        else
            placeLayer(s.Tops, top: true, null);
        placeLayer(s.Bottoms, top: false, null);

        sb.Append(Text(viewW / 2, 10, s.Caption, 7, "#64748B"));
        sb.Append(SvgClose());
        return sb.ToString();
    }

    private static List<int> FlattenBars(string token)
    {
        var list = new List<int>();
        foreach (Match m in Regex.Matches(token ?? "", @"(\d+)\s*:\s*(\d+)"))
        {
            if (!int.TryParse(m.Groups[1].Value, out var d)) continue;
            if (!int.TryParse(m.Groups[2].Value, out var n)) continue;
            for (int i = 0; i < Math.Clamp(n, 0, 16); i++) list.Add(d);
        }
        return list;
    }

    private static string BarFill(int dia) => dia switch
    {
        <= 10 => "#38BDF8",
        <= 16 => "#34D399",
        <= 20 => "#FBBF24",
        _ => "#F87171"
    };

    private static string SvgOpen(double w, double h) =>
        $"<svg xmlns='http://www.w3.org/2000/svg' width='{Inv(w)}' height='{Inv(h)}' viewBox='0 0 {Inv(w)} {Inv(h)}'>";

    private static string SvgClose() => "</svg>";

    private static string Rect(double x, double y, double w, double h, string fill, string stroke, double sw) =>
        $"<rect x='{Inv(x)}' y='{Inv(y)}' width='{Inv(w)}' height='{Inv(h)}' fill='{fill}' stroke='{stroke}' stroke-width='{Inv(sw)}'/>";

    private static string Circle(double cx, double cy, double r, string fill, string stroke) =>
        $"<circle cx='{Inv(cx)}' cy='{Inv(cy)}' r='{Inv(r)}' fill='{fill}' stroke='{stroke}' stroke-width='0.6'/>";

    private static string Text(double x, double y, string s, double size, string fill) =>
        $"<text x='{Inv(x)}' y='{Inv(y)}' text-anchor='middle' font-size='{Inv(size)}' fill='{fill}' font-family='Calibri,sans-serif'>{Esc(s)}</text>";

    private static string Get(Dictionary<string, string> r, string k, string def = "") =>
        r.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : def;

    private static double Num(Dictionary<string, string> r, string k) =>
        r.TryGetValue(k, out var v) && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;

    private static string Fmt(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
    private static string Inv(double d) => d.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}