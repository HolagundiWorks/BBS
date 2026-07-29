// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using System.Globalization;
using System.Text.RegularExpressions;
using BBSApp.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace BBSApp.Controls;

/// <summary>Which steel / geometry feature to emphasize on the section sketch.</summary>
public enum DiagramPart
{
    None,
    Geometry,
    Cover,
    Stirrups,
    Ties,
    Hangers,
    TopBars,
    BottomBars,
    LongBars,
    SideFace,
    MainX,
    MainY,
    BentUp,
    BottomMesh,
    TopMesh,
    ColumnStub,
    StemMain,
    StemDist,
    BaseSteel,
    Links,
    Pedestal,
    StairMain,
    StairDist,
    Landing,
    Extra
}

/// <summary>Live values from the entry form for dimensioned section sketches.</summary>
public sealed class DiagramSnapshot
{
    public double B { get; init; }      // width / breadth
    public double D { get; init; }      // depth / thickness
    public double L { get; init; }      // span / length
    public double Ly { get; init; }     // longer span (slab)
    public double Cover { get; init; }
    public double Height { get; init; } // column clear height
    public string StirrupDia { get; init; } = "";
    public string TieType { get; init; } = "Auto";
    public string ColumnType { get; init; } = "Rectangular";
    public string HangerBars { get; init; } = "";
    public string TopBars { get; init; } = "";      // φ:nos,…
    public string BottomBars { get; init; } = "";
    public string LongBars { get; init; } = "";
    public string SkinDia { get; init; } = "";
    public int SkinNos { get; init; }              // per face; 0 → from spacing
    public double SkinSpacing { get; init; }
    public string DiaX { get; init; } = "";
    public string DiaY { get; init; } = "";
    public string DiaL { get; init; } = "";
    public string DiaB { get; init; } = "";
    public int Legs { get; init; } = 2;
    public double SpacingSupport { get; init; }
    public double SpacingMiddle { get; init; }
    public double Going { get; init; }
    public double Riser { get; init; }
    public int NRisers { get; init; }
    public int HookAngle { get; init; } = 135;
    public string EndAnchorage { get; init; } = "Straight Ld";
    public bool ProvideLap { get; init; }
    public double LdMm { get; init; }      // representative Ld (largest bottom / long bar)
    public double LapMm { get; init; }

    // Footing
    public string FootingType { get; init; } = "Isolated";
    public double ColDimL { get; init; }
    public double ColDimB { get; init; }
    public double MeshSpacingL { get; init; }
    public double MeshSpacingB { get; init; }
    public string TopDiaL { get; init; } = "";
    public string TopDiaB { get; init; } = "";

    // Retaining wall
    public double Heel { get; init; }
    public double Toe { get; init; }
    public double BaseThickness { get; init; }
    public string TensionFace { get; init; } = "Front";
    public string DiaBack { get; init; } = "";
    public double StemVSpacing { get; init; }
    public double StemHSpacing { get; init; }
    public double StemVBackSpacing { get; init; }
    public double BaseLSpacing { get; init; }
    public double BaseBSpacing { get; init; }
    public string DiaBaseB { get; init; } = "";
    public string LinkDia { get; init; } = "";
    public double LinkSpacing { get; init; }
    public int LinkLegs { get; init; } = 2;

    // Slab
    public string SlabType { get; init; } = "Two-Way";
    public int CrankCount { get; init; }
    public double CrankRise { get; init; }
    public double SpacingX { get; init; }
    public double SpacingY { get; init; }
}

/// <summary>
/// Live RC technical drawings — true-scale section + elevation (IS 456 / SP 34–lite).
/// </summary>
public sealed class SectionDiagram : UserControl
{
    private readonly Canvas _canvas = new() { Width = 300, Height = 460 };
    private readonly TextBlock _caption = new()
    {
        FontSize = 11,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 4)
    };
    private readonly TextBlock _hint = new()
    {
        FontSize = 10,
        TextWrapping = TextWrapping.Wrap,
        Opacity = 0.75
    };
    private readonly StackPanel _legend = new() { Orientation = Orientation.Horizontal, Spacing = 6 };

    private string _kind = "beams";
    private DiagramPart _part = DiagramPart.None;
    private DiagramSnapshot _snap = new();

    // Distinct colours per bar diameter (mm) — readable on light/dark mica
    private static readonly Dictionary<int, Color> DiaPalette = new()
    {
        [6] = Color.FromArgb(255, 156, 163, 175),
        [8] = Color.FromArgb(255, 14, 165, 233),   // sky
        [10] = Color.FromArgb(255, 34, 197, 94),   // green
        [12] = Color.FromArgb(255, 234, 179, 8),   // amber
        [16] = Color.FromArgb(255, 249, 115, 22),  // orange
        [20] = Color.FromArgb(255, 239, 68, 68),   // red
        [25] = Color.FromArgb(255, 168, 85, 247),  // purple
        [28] = Color.FromArgb(255, 236, 72, 153),  // pink
        [32] = Color.FromArgb(255, 20, 184, 166),  // teal
        [36] = Color.FromArgb(255, 99, 102, 241),  // indigo
        [40] = Color.FromArgb(255, 180, 83, 9),    // brown
    };

    public SectionDiagram()
    {
        var root = new StackPanel { Spacing = 2 };
        root.Children.Add(_caption);
        var frame = new Border
        {
            Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(6),
            Child = _canvas,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        root.Children.Add(frame);
        root.Children.Add(_legend);
        root.Children.Add(_hint);
        Content = root;
        ActualThemeChanged += (_, _) => Redraw();
        Loaded += (_, _) => Redraw();
    }

    public void SetKind(string kind)
    {
        _kind = kind;
        Redraw();
    }

    public void Update(DiagramSnapshot snap, DiagramPart part, string? caption = null)
    {
        _snap = snap;
        _part = part;
        _caption.Text = caption ?? CaptionFor(part);
        _hint.Text = HintFor(part);
        Redraw();
    }

    public void SetHighlight(DiagramPart part, string? caption = null) =>
        Update(_snap, part, caption);

    public static DiagramPart PartForField(string kind, string fieldKey)
    {
        if (fieldKey.StartsWith("_sec_", StringComparison.Ordinal))
            return PartForSection(kind, fieldKey);

        return (kind, fieldKey) switch
        {
            ("beams", "width") or ("beams", "depth") or ("beams", "span")
                => DiagramPart.Geometry,
            ("beams", "cover") => DiagramPart.Cover,
            ("beams", "stirrup_dia") or ("beams", "spacing_support") or ("beams", "spacing_middle")
                or ("beams", "legs") or ("beams", "hook_angle")
                => DiagramPart.Stirrups,
            ("beams", "top_bar_type") or ("beams", "top_bars") => DiagramPart.TopBars,
            ("beams", "hanger_bars") => DiagramPart.Hangers,
            ("beams", "bottom_bars") => DiagramPart.BottomBars,
            ("beams", "skin_dia") or ("beams", "skin_spacing") or ("beams", "skin_nos")
                => DiagramPart.SideFace,
            ("beams", "end_anchorage") or ("beams", "provide_lap") or ("beams", "lap_nos")
                or ("beams", "steel_grade") or ("beams", "concrete_grade")
                => DiagramPart.BottomBars,

            ("stairs", "n_risers") or ("stairs", "going") or ("stairs", "riser")
                or ("stairs", "waist_t") or ("stairs", "flight_width") or ("stairs", "n_flights")
                or ("stairs", "landing_len") or ("stairs", "landing_width") or ("stairs", "landing_t")
                => DiagramPart.Geometry,
            ("stairs", "cover") => DiagramPart.Cover,
            ("stairs", "main_dia") or ("stairs", "main_spacing") => DiagramPart.StairMain,
            ("stairs", "dist_dia") or ("stairs", "dist_spacing") => DiagramPart.StairDist,
            ("stairs", "landing_dia") or ("stairs", "landing_spacing") => DiagramPart.Landing,

            ("columns", "width") or ("columns", "depth") or ("columns", "height")
                or ("columns", "column_type")
                => DiagramPart.Geometry,
            ("columns", "cover") => DiagramPart.Cover,
            ("columns", "stirrup_dia") or ("columns", "spacing") or ("columns", "hook_angle")
                or ("columns", "tie_type")
                => DiagramPart.Ties,
            ("columns", "bars") => DiagramPart.LongBars,
            ("columns", "provide_lap") or ("columns", "lap_nos") or ("columns", "steel_grade")
                or ("columns", "concrete_grade")
                => DiagramPart.LongBars,
            ("columns", var k) when k.StartsWith("pedestal", StringComparison.Ordinal) => DiagramPart.Pedestal,

            ("slabs", "span_x") or ("slabs", "span_y") or ("slabs", "thickness")
                => DiagramPart.Geometry,
            ("slabs", "cover") => DiagramPart.Cover,
            ("slabs", "dia_x") or ("slabs", "spacing_x") => DiagramPart.MainX,
            ("slabs", "dia_y") or ("slabs", "spacing_y") => DiagramPart.MainY,
            ("slabs", "crank_count") or ("slabs", "crank_rise") => DiagramPart.BentUp,

            ("footings", "length_l") or ("footings", "width_b") or ("footings", "depth")
                => DiagramPart.Geometry,
            ("footings", "cover") => DiagramPart.Cover,
            ("footings", var k) when k.StartsWith("col", StringComparison.Ordinal) => DiagramPart.ColumnStub,
            ("footings", "dia_l") or ("footings", "spacing_l") or ("footings", "dia_b")
                or ("footings", "spacing_b")
                => DiagramPart.BottomMesh,
            ("footings", var k) when k.StartsWith("top_", StringComparison.Ordinal) => DiagramPart.TopMesh,
            ("footings", var k) when k.Contains("step", StringComparison.Ordinal) => DiagramPart.Geometry,

            ("walls", "stem_h") or ("walls", "stem_t") or ("walls", "heel") or ("walls", "toe")
                or ("walls", "base_t") or ("walls", "wall_length")
                => DiagramPart.Geometry,
            ("walls", "cover") => DiagramPart.Cover,
            ("walls", "stem_v_dia") or ("walls", "stem_v_spacing") or ("walls", "stem_v_back_dia")
                or ("walls", "stem_v_back_spacing") or ("walls", "tension_face")
                => DiagramPart.StemMain,
            ("walls", "stem_h_dia") or ("walls", "stem_h_spacing") => DiagramPart.StemDist,
            ("walls", "base_l_dia") or ("walls", "base_l_spacing") or ("walls", "base_b_dia")
                or ("walls", "base_b_spacing")
                => DiagramPart.BaseSteel,
            ("walls", "link_dia") or ("walls", "link_spacing") or ("walls", "link_legs")
                => DiagramPart.Links,

            _ => DiagramPart.None
        };
    }

    private static DiagramPart PartForSection(string kind, string secKey)
    {
        var t = secKey.ToLowerInvariant();
        if (t.Contains("shear") || t.Contains("stirrup") || t.Contains("tie"))
            return kind == "columns" ? DiagramPart.Ties : DiagramPart.Stirrups;
        if (t.Contains("flexural") || t.Contains("main flexural") || t.Contains("long"))
            return kind == "columns" ? DiagramPart.LongBars : DiagramPart.TopBars;
        if (t.Contains("hanger")) return DiagramPart.Hangers;
        if (t.Contains("side-face") || t.Contains("side face") || t.Contains("distributor"))
            return DiagramPart.SideFace;
        if (t.Contains("bent")) return DiagramPart.BentUp;
        if (t.Contains("shorter") || t.Contains("ℓx")) return DiagramPart.MainX;
        if (t.Contains("longer") || t.Contains("distribution") || t.Contains("ℓy")) return DiagramPart.MainY;
        if (t.Contains("bottom reinf")) return DiagramPart.BottomMesh;
        if (t.Contains("top reinf")) return DiagramPart.TopMesh;
        if (t.Contains("stem")) return DiagramPart.StemMain;
        if (t.Contains("base slab")) return DiagramPart.BaseSteel;
        if (t.Contains("link")) return DiagramPart.Links;
        if (t.Contains("pedestal")) return DiagramPart.Pedestal;
        if (t.Contains("waist main") || (t.Contains("main steel") && kind == "stairs"))
            return DiagramPart.StairMain;
        if (t.Contains("distribution steel") && kind == "stairs") return DiagramPart.StairDist;
        if (t.Contains("landing")) return DiagramPart.Landing;
        if (t.Contains("flight") || t.Contains("cross-section") || t.Contains("geometry") || t.Contains("plan"))
            return DiagramPart.Geometry;
        return DiagramPart.None;
    }

    private static string CaptionFor(DiagramPart part) => part switch
    {
        DiagramPart.Geometry => "Cross-section / geometry",
        DiagramPart.Cover => "Nominal cover / d′",
        DiagramPart.Stirrups => "Stirrups (2/4-leg · s1 / s2)",
        DiagramPart.Ties => "Lateral ties",
        DiagramPart.Hangers => "Hanger bars",
        DiagramPart.TopBars => "Top main bars",
        DiagramPart.BottomBars => "Bottom main bars",
        DiagramPart.LongBars => "Longitudinal bars",
        DiagramPart.SideFace => "Side-face / distributor",
        DiagramPart.MainX => "Main steel — ℓx",
        DiagramPart.MainY => "Steel — ℓy",
        DiagramPart.BentUp => "Bent-up bars",
        DiagramPart.BottomMesh => "Bottom mesh",
        DiagramPart.TopMesh => "Top mesh",
        DiagramPart.ColumnStub => "Column on footing",
        DiagramPart.StemMain => "Stem main vertical",
        DiagramPart.StemDist => "Stem distribution",
        DiagramPart.BaseSteel => "Base slab steel",
        DiagramPart.Links => "Links",
        DiagramPart.Pedestal => "Pedestal",
        DiagramPart.StairMain => "Waist main bars (slope)",
        DiagramPart.StairDist => "Distribution bars",
        DiagramPart.Landing => "Landing mesh",
        DiagramPart.Extra => "Additional bars",
        _ => "Section sketch"
    };

    private static string HintFor(DiagramPart part) => part switch
    {
        DiagramPart.Stirrups => "Closed links · 4-leg adds crosstie · s1 near supports, s2 mid-span.",
        DiagramPart.Hangers => "Corner bars that hang the stirrup cage — full span.",
        DiagramPart.TopBars or DiagramPart.BottomBars or DiagramPart.LongBars
            => "Bar colour = diameter. Glow marks the active steel.",
        DiagramPart.SideFace => "Skin bars each face — count from nos or spacing (D > 750).",
        DiagramPart.StairMain => "Main bars along inclined waist — colour = φ.",
        DiagramPart.StairDist => "Distribution across flight width along slope.",
        DiagramPart.Landing => "Landing mesh both ways.",
        DiagramPart.None => "Focus a field — true-scale section & elevation update live.",
        _ => "Scale badge = drawing ratio · glow = active part · colours = φ."
    };

    private void Redraw()
    {
        _canvas.Children.Clear();
        _legend.Children.Clear();
        var muted = Brush("TextFillColorTertiaryBrush", Colors.Gray);
        var line = Brush("TextFillColorSecondaryBrush", Colors.DimGray);
        var accent = Brush("AccentFillColorDefaultBrush", Color.FromArgb(255, 0, 120, 212));

        switch (_kind)
        {
            case "columns": DrawColumn(muted, line, accent); break;
            case "slabs": DrawSlab(muted, line, accent); break;
            case "footings": DrawFooting(muted, line, accent); break;
            case "walls": DrawWall(muted, line, accent); break;
            case "stairs": DrawStair(muted, line, accent); break;
            case "masonry":
            case "plaster":
            case "pcc":
            case "earthwork":
            case "ssm":
            case "shuttering":
            case "flooring":
            case "painting":
            case "doors":
            case "windows":
            case "waterproofing":
            case "dpc":
            case "coping":
            case "screed":
            case "vdf":
            case "skirting":
            case "parapet":
            case "plinth_protection":
                DrawCivilBox(muted, line, accent); break;
            default: DrawBeam(muted, line, accent); break;
        }
    }

    private void DrawCivilBox(Brush muted, Brush line, Brush accent)
    {
        double L = Math.Max(_snap.L, 1);
        double B = Math.Max(_snap.B > 0 ? _snap.B : _snap.D, 1);
        double H = Math.Max(_snap.Height > 0 ? _snap.Height : _snap.D, 1);
        // Prefer length × height for walls/plaster; L×B×H for volumes
        bool isArea = _kind is "plaster" or "shuttering" or "flooring" or "painting" or "doors" or "windows"
            or "waterproofing" or "skirting" or "dpc" or "screed" or "vdf" or "plinth_protection"
            || (_kind == "masonry" && Math.Abs(B - 110) < 1);
        const double boxW = 220, boxH = 140;
        double scale = isArea
            ? Math.Min(boxW / L, boxH / H)
            : Math.Min(boxW / Math.Max(L, B), boxH / Math.Max(H, B));
        double w = L * scale;
        double h = (isArea ? H : Math.Max(H, B)) * scale * (isArea ? 1 : 0.85);
        double x = 40 + (boxW - w) / 2, y = 40;
        ScaleBadge(8, 8, scale, muted, "QTY");
        AddTechRect(x, y, w, Math.Max(h, 20), Fill(line, 22),
            Hot(DiagramPart.Geometry) ? accent : line, 1.6, Hot(DiagramPart.Geometry));
        TechDimHorizontal(x, y + Math.Max(h, 20) + 8, w, Fmt(L, "L"), muted, true);
        if (isArea)
        {
            TechDimVertical(x + w + 10, y, Math.Max(h, 20), Fmt(H, "H"), muted, true);
            double area = L * H / 1e6;
            Label(x + w / 2, y + Math.Max(h, 20) / 2 - 6, $"{area:0.###} m²", accent, 11);
        }
        else
        {
            TechDimVertical(x + w + 10, y, Math.Max(h, 20), Fmt(H > 1 ? H : B, H > 1 ? "H" : "B"), muted, true);
            double vol = MaterialsCalculator.Mm3ToM3(L * B * (H > 1 ? H : B));
            // For earthwork depth is in Height or D; snapshot sets fields below
            if (_snap.D > 0 && _snap.Height <= 0 && _snap.B > 0)
                vol = MaterialsCalculator.Mm3ToM3(L * _snap.B * _snap.D);
            Label(x + w / 2, y + Math.Max(h, 20) / 2 - 6, $"{vol:0.###} m³", accent, 11);
            if (_snap.B > 0)
                Label(x + w / 2, y + Math.Max(h, 20) + 28, Fmt(_snap.B, "B"), muted, 9);
        }
        string title = _kind switch
        {
            "masonry" => "Masonry",
            "plaster" => "Plaster",
            "pcc" => "PCC bed",
            "earthwork" => "Earthwork",
            "ssm" => "Size stone",
            "shuttering" => "Shuttering",
            "flooring" => "Flooring",
            "painting" => "Painting",
            "doors" => "Door",
            "windows" => "Window",
            _ => "Civil"
        };
        Label(x + w / 2, y - 14, title, muted, 9);
    }

    private bool Hot(DiagramPart p) => _part == p;

    private void DrawBeam(Brush muted, Brush line, Brush accent)
    {
        double bMm = Math.Max(_snap.B, 1);
        double dMm = Math.Max(_snap.D, 1);
        double lMm = Math.Max(_snap.L, 0);
        double coverMm = Math.Max(_snap.Cover, 0);
        int legs = _snap.Legs >= 4 ? 4 : 2;
        double.TryParse(_snap.StirrupDia, NumberStyles.Float, CultureInfo.InvariantCulture, out var stirDia);
        if (stirDia <= 0) stirDia = 8;
        int hookAng = _snap.HookAngle > 0 ? _snap.HookAngle : 135;
        var stirColor = DiaBrush(_snap.StirrupDia, fallback: Color.FromArgb(255, 14, 165, 233));

        // ——— Cross-section (true proportion, letterboxed) ———
        const double secBoxW = 160, secBoxH = 170;
        double scale = Math.Min(secBoxW / bMm, secBoxH / dMm);
        double w = bMm * scale, h = dMm * scale;
        double x = 48 + (secBoxW - w) / 2, y = 22 + (secBoxH - h) / 2;
        ScaleBadge(8, 6, scale, muted);

        // Concrete outline (sharp)
        AddTechRect(x, y, w, h, null, Hot(DiagramPart.Geometry) ? accent : line,
            Hot(DiagramPart.Geometry) ? 2.2 : 1.4, Hot(DiagramPart.Geometry));

        // Cover line (to stirrup outer face)
        double cPx = coverMm * scale;
        if (coverMm > 0 && cPx >= 0.5)
        {
            AddTechRect(x + cPx, y + cPx, w - 2 * cPx, h - 2 * cPx, null,
                Hot(DiagramPart.Cover) ? accent : Soft(muted, 160),
                Hot(DiagramPart.Cover) ? 1.6 : 0.9, false);
            TechDimHorizontal(x, y - 10, cPx, Fmt(coverMm), Hot(DiagramPart.Cover) ? accent : muted, Hot(DiagramPart.Cover));
        }

        // Stirrup centreline inset = cover + φs/2
        double sInset = (coverMm + stirDia * 0.5) * scale;
        double sx = x + sInset, sy = y + sInset, sw = w - 2 * sInset, sh = h - 2 * sInset;
        if (sw > 4 && sh > 4)
        {
            DrawClosedStirrup(sx, sy, sw, sh, hookAng, stirDia, scale, stirColor, Hot(DiagramPart.Stirrups));
            if (legs >= 4)
            {
                AddGlowLine(sx + sw / 2, sy + 1, sx + sw / 2, sy + sh - 1, stirColor, Hot(DiagramPart.Stirrups));
                AddGlowLine(sx + 1, sy + sh / 2, sx + sw - 1, sy + sh / 2, Soft(stirColor, 180), Hot(DiagramPart.Stirrups));
            }
            if (Hot(DiagramPart.Stirrups))
            {
                Label(x + w / 2, y + h / 2 - 4, $"{legs}-leg · {hookAng}° · {Fmt(stirDia)}Φ", stirColor, 8);
                AddLegendDia(_snap.StirrupDia);
            }
        }

        var hangers = FlattenBars(ParseBars(_snap.HangerBars, 12, 2));
        var tops = FlattenBars(ParseBars(_snap.TopBars, 16, 0));
        var bottoms = FlattenBars(ParseBars(_snap.BottomBars, 16, 3));

        // Bar centres: cover + φs + φ/2 from concrete face
        double TopY(int dia) => y + (coverMm + stirDia + dia * 0.5) * scale;
        double BotY(int dia) => y + h - (coverMm + stirDia + dia * 0.5) * scale;
        double LayerX0(int maxDia) => x + (coverMm + stirDia + maxDia * 0.5) * scale;
        double LayerX1(int maxDia) => x + w - (coverMm + stirDia + maxDia * 0.5) * scale;

        // Hangers at corners of top layer; tops between
        int topMax = Math.Max(
            hangers.Count > 0 ? hangers.Max() : 0,
            tops.Count > 0 ? tops.Max() : 16);
        if (topMax <= 0) topMax = 16;
        PlaceHangerAndTopScaled(hangers, tops, LayerX0(topMax), LayerX1(topMax),
            hangers.Count > 0 ? TopY(hangers[0]) : TopY(topMax),
            Hot(DiagramPart.Hangers), Hot(DiagramPart.TopBars) || Hot(DiagramPart.Extra), scale);

        if (bottoms.Count > 0)
        {
            int botMax = bottoms.Max();
            PlaceBottomBarsScaled(bottoms, LayerX0(botMax), LayerX1(botMax), BotY(botMax),
                Hot(DiagramPart.BottomBars), scale);
        }

        if (!string.IsNullOrWhiteSpace(_snap.SkinDia) || _snap.SkinNos > 0 || _snap.SkinSpacing > 0)
        {
            int perFace = SkinBarsPerFace(_snap, dMm, coverMm);
            if (perFace > 0 && int.TryParse(_snap.SkinDia, out var skinDia) && skinDia > 0)
            {
                var skin = DiaBrush(_snap.SkinDia);
                double skinX0 = x + (coverMm + stirDia + skinDia * 0.5) * scale;
                double skinX1 = x + w - (coverMm + stirDia + skinDia * 0.5) * scale;
                double y0 = TopY(topMax) + Math.Max(skinDia, 8) * scale;
                double y1 = (bottoms.Count > 0 ? BotY(bottoms.Max()) : BotY(16)) - Math.Max(skinDia, 8) * scale;
                PlaceSkinFaceScaled(skinX0, y0, y1, perFace, skin, Hot(DiagramPart.SideFace), skinDia, scale);
                PlaceSkinFaceScaled(skinX1, y0, y1, perFace, skin, Hot(DiagramPart.SideFace), skinDia, scale);
                if (Hot(DiagramPart.SideFace))
                {
                    AddLegendDia(_snap.SkinDia);
                    Label(x + w / 2, y + h / 2 + 10, $"{perFace}/face @{Fmt(_snap.SkinSpacing)}", skin, 8);
                }
            }
        }

        // Overall dims b, D, effective depth d
        TechDimHorizontal(x, y + h + 8, w, Fmt(bMm, "b"), muted, Hot(DiagramPart.Geometry));
        TechDimVertical(x + w + 10, y, h, Fmt(dMm, "D"), muted, Hot(DiagramPart.Geometry));
        double dEff = Math.Max(0, dMm - coverMm - (bottoms.Count > 0 ? bottoms.Max() * 0.5 : 8));
        if (dEff > 0 && bottoms.Count > 0)
        {
            double dTop = TopY(topMax);
            double dBot = BotY(bottoms.Max());
            TechDimVertical(x - 16, dTop, Math.Max(1, dBot - dTop), Fmt(dEff, "d"),
                Hot(DiagramPart.Geometry) || Hot(DiagramPart.Cover) ? accent : muted,
                Hot(DiagramPart.Geometry) || Hot(DiagramPart.Cover));
        }

        // ——— Elevation (true span + depth scale) ———
        double ey = y + h + 36;
        const double elevAvailW = 248, elevAvailH = 56;
        double elevScale = lMm > 0
            ? Math.Min(elevAvailW / lMm, elevAvailH / Math.Max(dMm, 1))
            : scale;
        double eW = Math.Max(40, lMm > 0 ? lMm * elevScale : elevAvailW);
        double eH = Math.Max(10, dMm * elevScale);
        double eX = 26 + (elevAvailW - eW) / 2;
        ScaleBadge(8, ey - 12, elevScale, muted, "EL");

        AddTechRect(eX, ey, eW, eH, Fill(line, 28),
            Hot(DiagramPart.Geometry) ? accent : line, Hot(DiagramPart.Geometry) ? 1.8 : 1.2, Hot(DiagramPart.Geometry));

        // Support zones ≈ 2d each end (to scale)
        double zoneMm = dEff > 0 ? 2 * dEff : 0.22 * Math.Max(lMm, 1);
        if (lMm > 0) zoneMm = Math.Min(zoneMm, lMm * 0.45);
        double zW = zoneMm * elevScale;
        double midX0 = eX + zW;
        double midW = Math.Max(2, eW - 2 * zW);

        if (_snap.SpacingSupport > 0 || Hot(DiagramPart.Stirrups))
        {
            DrawStirrupTicksScaled(eX, ey, zW, eH, _snap.SpacingSupport > 0 ? _snap.SpacingSupport : 100,
                elevScale, stirColor, Hot(DiagramPart.Stirrups));
            DrawStirrupTicksScaled(eX + eW - zW, ey, zW, eH, _snap.SpacingSupport > 0 ? _snap.SpacingSupport : 100,
                elevScale, stirColor, Hot(DiagramPart.Stirrups));
            DrawStirrupTicksScaled(midX0, ey, midW, eH, _snap.SpacingMiddle > 0 ? _snap.SpacingMiddle : 150,
                elevScale, Soft(stirColor, 170), Hot(DiagramPart.Stirrups));
            Label(eX + zW / 2, ey - 11, $"s1@{Fmt(_snap.SpacingSupport)}", Hot(DiagramPart.Stirrups) ? stirColor : muted, 8);
            Label(midX0 + midW / 2, ey - 11, $"s2@{Fmt(_snap.SpacingMiddle)}", Hot(DiagramPart.Stirrups) ? stirColor : muted, 8);
            Label(eX + eW - zW / 2, ey - 11, "s1", Hot(DiagramPart.Stirrups) ? stirColor : muted, 8);
            // Zone length dims
            if (zW > 12)
                TechDimHorizontal(eX, ey + eH + 2, zW, Fmt(zoneMm, "2d"), muted, false);
        }

        if (hangers.Count > 0)
        {
            var hb = new SolidColorBrush(ColorForDia(hangers[0]));
            double barY = ey + Math.Max(2, (coverMm + stirDia + hangers[0] * 0.5) * elevScale);
            AddGlowLine(eX + 2, barY, eX + eW - 2, barY, hb, Hot(DiagramPart.Hangers));
        }
        if (bottoms.Count > 0)
        {
            var bb = new SolidColorBrush(ColorForDia(bottoms.Max()));
            double barY = ey + eH - Math.Max(2, (coverMm + stirDia + bottoms.Max() * 0.5) * elevScale);
            AddGlowLine(eX + 2, barY, eX + eW - 2, barY, bb, Hot(DiagramPart.BottomBars));
        }

        // Ld / hooks — true length
        double ldMm = _snap.LdMm > 0 ? _snap.LdMm : 0;
        double ldW = ldMm > 0 && elevScale > 0 ? ldMm * elevScale : eW * 0.12;
        ldW = Math.Min(ldW, eW * 0.4);
        var ldBrush = Hot(DiagramPart.BottomBars) || Hot(DiagramPart.TopBars) ? accent : muted;
        double ldY = ey + eH + 14;
        AddGlowLine(eX, ldY, eX + ldW, ldY, ldBrush, true);
        AddGlowLine(eX + eW - ldW, ldY, eX + eW, ldY, ldBrush, true);
        bool hooked = (_snap.EndAnchorage ?? "").Contains("Hook", StringComparison.OrdinalIgnoreCase);
        string endLab = hooked
            ? (_snap.EndAnchorage!.Contains("180") ? "180° hook" : "90° hook")
            : (ldMm > 0 ? $"Ld={Fmt(ldMm)}" : "Ld");
        Label(eX + ldW / 2, ldY + 4, endLab, ldBrush, 8);
        Label(eX + eW - ldW / 2, ldY + 4, endLab, ldBrush, 8);
        if (hooked)
        {
            DrawElevHook(eX, ldY, true, _snap.EndAnchorage!.Contains("180"), ldBrush);
            DrawElevHook(eX + eW, ldY, false, _snap.EndAnchorage!.Contains("180"), ldBrush);
        }

        if (_snap.ProvideLap && _snap.LapMm > 0)
        {
            double lapW = Math.Min(_snap.LapMm * elevScale, midW);
            double lapX = midX0 + (midW - lapW) / 2;
            AddTechRect(lapX, ey + 1, lapW, Math.Max(eH - 2, 6), Fill(accent, 55), accent, 1.4, true);
            Label(lapX + lapW / 2, ey - 22, $"Lap={Fmt(_snap.LapMm)}", accent, 8);
            TechDimHorizontal(lapX, ey + eH + 22, lapW, Fmt(_snap.LapMm), accent, true);
        }

        var botGroups = ParseBars(_snap.BottomBars, 16, 0);
        if (botGroups.Count > 0)
        {
            var mark = string.Join("+", botGroups.Select(g => $"{g.nos}-{g.dia}Φ"));
            Label(eX + eW / 2, ldY + 18, mark, muted, 8);
        }

        TechDimHorizontal(eX, ey + eH + 36, eW,
            lMm > 0 ? Fmt(lMm, "L") : "L —",
            Hot(DiagramPart.Geometry) ? accent : muted,
            Hot(DiagramPart.Geometry) || lMm <= 0);
        TechDimVertical(eX + eW + 8, ey, eH, Fmt(dMm, "D"), muted, false);
    }

    private void DrawStirrupTicksScaled(double x, double y, double w, double h, double spacingMm,
        double elevScale, Brush brush, bool glow)
    {
        if (w <= 0 || elevScale <= 0) return;
        double pitch = Math.Max(spacingMm * elevScale, 3);
        int maxTicks = 40;
        int n = 0;
        for (double cx = x + pitch * 0.5; cx < x + w - 1 && n < maxTicks; cx += pitch, n++)
            AddGlowLine(cx, y + 1, cx, y + h - 1, brush, glow);
    }

    private void DrawElevHook(double x, double y, bool leftEnd, bool is180, Brush brush)
    {
        double dir = leftEnd ? 1 : -1;
        double arm = is180 ? 10 : 7;
        AddLine(x, y, x, y - arm, brush, 1.4);
        if (is180)
            AddLine(x, y - arm, x + dir * arm, y - arm, brush, 1.4);
        else
            AddLine(x, y - arm, x + dir * arm * 0.7, y - arm * 0.35, brush, 1.4);
    }

    private void DrawClosedStirrup(double x, double y, double w, double h, int hookAng, double diaMm,
        double scale, Brush brush, bool glow)
    {
        double thick = Math.Max(1.1, diaMm * scale * 0.65);
        // Closed rectangle (centreline)
        AddLineThick(x, y, x + w, y, brush, thick, glow);
        AddLineThick(x + w, y, x + w, y + h, brush, thick, glow);
        AddLineThick(x + w, y + h, x, y + h, brush, thick, glow);
        AddLineThick(x, y + h, x, y, brush, thick, glow);

        // Hooks at bottom-left (IS 2502 / SP 34 convention)
        double hookLen = Math.Max(5, Math.Min(Math.Min(w, h) * 0.35,
            Math.Max(hookAng >= 135 ? 10 : 9, 75.0 / Math.Max(diaMm, 1)) * diaMm * scale * 0.35));
        if (hookAng >= 135)
        {
            // 135°: legs at ~45° into the section
            double dx = hookLen * 0.707, dy = hookLen * 0.707;
            AddLineThick(x, y + h, x + dx, y + h - dy, brush, thick, glow);
            AddLineThick(x, y + h, x + dx, y + h - dy * 0.55, Soft(brush, 200), thick * 0.85, glow);
        }
        else
        {
            AddLineThick(x, y + h, x + hookLen, y + h, brush, thick, glow);
            AddLineThick(x + hookLen, y + h, x + hookLen, y + h - hookLen * 0.6, brush, thick, glow);
        }
    }

    private void AddLineThick(double x1, double y1, double x2, double y2, Brush brush, double thick, bool glow)
    {
        if (glow)
            AddLine(x1, y1, x2, y2, Soft(brush, 70), thick + 4);
        AddLine(x1, y1, x2, y2, brush, thick);
    }

    private void DrawStair(Brush muted, Brush line, Brush accent)
    {
        int n = Math.Clamp(_snap.NRisers > 0 ? _snap.NRisers : 8, 3, 16);
        double going = Math.Max(_snap.Going, 1);
        double riser = Math.Max(_snap.Riser, 1);
        double waist = Math.Max(_snap.D, 80);
        double widthMm = Math.Max(_snap.B, 1);

        // Profile: stepped outline
        double maxW = 200, maxH = 140;
        double run = (n - 1) * going;
        double rise = n * riser;
        double sx = maxW / Math.Max(run, 1);
        double sy = maxH / Math.Max(rise + waist, 1);
        double scale = Math.Min(sx, sy);
        double ox = 28, oy = 28 + rise * scale;

        // Landings
        double landW = 28;
        AddRect(ox - landW, oy - 8, landW, 10, Fill(line, 40), line, 1);
        double topX = ox + run * scale;
        double topY = oy - rise * scale;
        AddRect(topX, topY - 8, landW, 10, Fill(line, 40),
            Hot(DiagramPart.Landing) ? accent : line, Hot(DiagramPart.Landing) ? 2 : 1, Hot(DiagramPart.Landing));

        // Steps
        var pts = new List<(double x, double y)>();
        pts.Add((ox, oy));
        for (int i = 0; i < n; i++)
        {
            double x0 = ox + i * going * scale;
            double y0 = oy - i * riser * scale;
            pts.Add((x0, y0 - riser * scale));
            if (i < n - 1)
                pts.Add((x0 + going * scale, y0 - riser * scale));
        }
        for (int i = 0; i < pts.Count - 1; i++)
            AddGlowLine(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y,
                Hot(DiagramPart.Geometry) ? accent : line, Hot(DiagramPart.Geometry));

        // Waist underside (parallel to slope)
        double ang = Math.Atan2(rise, run);
        double wx = waist * scale * Math.Sin(ang);
        double wy = waist * scale * Math.Cos(ang);
        AddGlowLine(ox, oy + wy * 0.15, topX, topY + wy * 0.15 + 4,
            Hot(DiagramPart.Geometry) || Hot(DiagramPart.StairMain) ? accent : muted,
            Hot(DiagramPart.Geometry) || Hot(DiagramPart.StairMain));

        // Main bars along slope
        if (!string.IsNullOrWhiteSpace(_snap.DiaX) || Hot(DiagramPart.StairMain))
        {
            var mb = DiaBrush(_snap.DiaX, Color.FromArgb(255, 249, 115, 22));
            AddGlowLine(ox + 6, oy - 4, topX - 4, topY + 6, mb, Hot(DiagramPart.StairMain));
            if (Hot(DiagramPart.StairMain)) AddLegendDia(_snap.DiaX);
        }

        // Dist ticks
        if (!string.IsNullOrWhiteSpace(_snap.DiaY) || Hot(DiagramPart.StairDist))
        {
            var db = DiaBrush(_snap.DiaY, Color.FromArgb(255, 34, 197, 94));
            int ticks = 5;
            for (int i = 0; i < ticks; i++)
            {
                double t = (i + 1) / (double)(ticks + 1);
                double cx = ox + run * scale * t;
                double cy = oy - rise * scale * t;
                AddGlowLine(cx - 4, cy + 2, cx + 4, cy - 6, db, Hot(DiagramPart.StairDist));
            }
            if (Hot(DiagramPart.StairDist)) AddLegendDia(_snap.DiaY);
        }

        DimHorizontal(ox, oy + 18, run * scale, Fmt(run, "going Σ"), muted, Hot(DiagramPart.Geometry));
        DimVertical(topX + landW + 6, topY, rise * scale, Fmt(rise, "rise"), muted, Hot(DiagramPart.Geometry));
        Label(ox + run * scale / 2, topY - 14, $"waist {Fmt(waist)} · {n}R",
            Hot(DiagramPart.Geometry) ? accent : muted, 9);
        Label(ox + 4, oy + 32, $"w={Fmt(widthMm)}", muted, 9);
    }

    private static int SkinBarsPerFace(DiagramSnapshot snap, double depthMm, double coverMm)
    {
        if (snap.SkinNos > 0) return snap.SkinNos;
        if (snap.SkinSpacing <= 0) return 0;
        double clear = Math.Max(0, depthMm - 2 * coverMm);
        if (clear <= 0) return 0;
        return (int)Math.Floor(clear / snap.SkinSpacing) + 1;
    }

    private void PlaceHangerAndTopScaled(List<int> hangers, List<int> tops, double x0, double x1, double y,
        bool hotHang, bool hotTop, double scale)
    {
        if (hangers.Count == 0 && tops.Count == 0) return;
        if (x1 <= x0) { x0 -= 2; x1 += 2; }

        var leftHang = hangers.Count > 0 ? hangers[0] : (int?)null;
        var rightHang = hangers.Count > 1 ? hangers[^1] : leftHang;
        var midHangers = hangers.Count > 2 ? hangers.Skip(1).Take(hangers.Count - 2).ToList() : new List<int>();

        if (leftHang is int lh)
        {
            AddBarDot(x0, y, new SolidColorBrush(ColorForDia(lh)), hotHang, r: BarR(lh, scale));
            if (hotHang) AddLegendDia(lh.ToString());
        }
        if (rightHang is int rh && hangers.Count >= 1)
        {
            AddBarDot(x1, y, new SolidColorBrush(ColorForDia(rh)), hotHang, r: BarR(rh, scale));
            if (hotHang) AddLegendDia(rh.ToString());
        }

        var inner = midHangers.Concat(tops).ToList();
        if (inner.Count == 0) return;
        // Keep clear of corner hangers when present
        double inner0 = hangers.Count > 0 ? x0 + Math.Max(4, (x1 - x0) * 0.12) : x0;
        double inner1 = hangers.Count > 0 ? x1 - Math.Max(4, (x1 - x0) * 0.12) : x1;
        for (int i = 0; i < inner.Count; i++)
        {
            double t = inner.Count == 1 ? 0.5 : i / (double)(inner.Count - 1);
            double cx = inner0 + (inner1 - inner0) * t;
            bool isHang = i < midHangers.Count;
            bool glow = isHang ? hotHang : hotTop;
            AddBarDot(cx, y, new SolidColorBrush(ColorForDia(inner[i])), glow, r: BarR(inner[i], scale));
            if (glow) AddLegendDia(inner[i].ToString());
        }
    }

    private void PlaceBottomBarsScaled(List<int> dias, double x0, double x1, double y, bool hot, double scale)
    {
        if (dias.Count == 0) return;
        if (x1 <= x0) { x0 -= 2; x1 += 2; }
        var ordered = dias.OrderByDescending(d => d).ToList();
        var arranged = new int[ordered.Count];
        int lo = 0, hi = arranged.Length - 1;
        bool left = true;
        foreach (var d in ordered)
        {
            if (left) arranged[lo++] = d;
            else arranged[hi--] = d;
            left = !left;
        }
        for (int i = 0; i < arranged.Length; i++)
        {
            double t = arranged.Length == 1 ? 0.5 : i / (double)(arranged.Length - 1);
            double cx = x0 + (x1 - x0) * t;
            AddBarDot(cx, y, new SolidColorBrush(ColorForDia(arranged[i])), hot, r: BarR(arranged[i], scale));
            if (hot) AddLegendDia(arranged[i].ToString());
        }
    }

    private void PlaceSkinFaceScaled(double x, double y0, double y1, int nos, Brush brush, bool hot,
        int dia, double scale)
    {
        if (nos <= 0 || y1 <= y0) return;
        for (int i = 0; i < nos; i++)
        {
            double t = nos == 1 ? 0.5 : i / (double)(nos - 1);
            double cy = y0 + (y1 - y0) * t;
            AddBarDot(x, cy, brush, hot, r: BarR(dia, scale));
        }
    }

    private static double BarR(int dia, double scale) => Math.Max(1.6, (dia * 0.5) * scale);

    private static List<int> FlattenBars(List<(int dia, int nos)> groups)
    {
        var list = new List<int>();
        foreach (var (dia, nos) in groups)
            for (int i = 0; i < Math.Clamp(nos, 0, 24); i++) list.Add(dia);
        return list;
    }

    private void DrawColumn(Brush muted, Brush line, Brush accent)
    {
        double bMm = Math.Max(_snap.B, 1);
        double dMm = Math.Max(_snap.D, 1);
        double coverMm = Math.Max(_snap.Cover, 0);
        double hMm = Math.Max(_snap.Height, 0);
        double.TryParse(_snap.StirrupDia, NumberStyles.Float, CultureInfo.InvariantCulture, out var tieDiaMm);
        if (tieDiaMm <= 0) tieDiaMm = 8;
        int hookAng = _snap.HookAngle > 0 ? _snap.HookAngle : 135;

        var arr = ColumnLayout.Arrange(bMm, dMm, coverMm, tieDiaMm, _snap.LongBars, _snap.TieType, _snap.ColumnType);
        bool circular = (_snap.ColumnType ?? "").Equals("Circular", StringComparison.OrdinalIgnoreCase)
                        || arr.TieCase is ColumnTieCase.Circular or ColumnTieCase.Spiral;

        const double secBox = 150;
        double scale = secBox / Math.Max(bMm, Math.Max(dMm, 1));
        double w = bMm * scale;
        double h = circular ? w : dMm * scale;
        double x = 50 + (secBox - w) / 2, y = 18 + (secBox - h) / 2;
        ScaleBadge(8, 6, scale, muted);

        bool hotTie = Hot(DiagramPart.Ties);
        bool hotBars = Hot(DiagramPart.LongBars);
        var tieBrush = DiaBrush(_snap.StirrupDia, Color.FromArgb(255, 14, 165, 233));

        if (circular)
        {
            double cx = x + w / 2, cy = y + h / 2, r = w / 2;
            AddEllipseRing(cx, cy, r, Hot(DiagramPart.Geometry) ? accent : line, Hot(DiagramPart.Geometry));
            double cPx = coverMm * scale;
            if (cPx > 0.5)
                AddEllipseRing(cx, cy, Math.Max(2, r - cPx), Hot(DiagramPart.Cover) ? accent : muted, Hot(DiagramPart.Cover));

            // Tie centreline radius = outer − cover − φt/2
            double cageR = Math.Max(4, r - (coverMm + tieDiaMm * 0.5) * scale);
            DrawColumnTies(arr.TieCase, cx - cageR, cy - cageR, cageR * 2, cageR * 2, tieBrush, hotTie, muted);
            if (hotTie) AddLegendDia(_snap.StirrupDia);

            foreach (var bp in arr.Bars)
            {
                double ang = Math.Atan2(bp.V - 0.5, bp.U - 0.5);
                // Bar centre on ring inside tie: cageR − φ/2
                double pr = Math.Max(2, cageR - BarR(bp.Dia, scale));
                AddBarDot(cx + pr * Math.Cos(ang), cy + pr * Math.Sin(ang),
                    new SolidColorBrush(ColorForDia(bp.Dia)), hotBars, r: BarR(bp.Dia, scale));
                if (hotBars) AddLegendDia(bp.Dia.ToString());
            }

            TechDimHorizontal(x, y + h + 6, w, Fmt(bMm, "Ø"), muted, Hot(DiagramPart.Geometry));
            Label(x + w / 2, y + h + 22, arr.Label, hotTie || hotBars ? accent : muted, 8);
            Label(x + w / 2, y + h + 34, $"{arr.TotalBars} bars on ring", muted, 8);
        }
        else
        {
            AddTechRect(x, y, w, h, null, Hot(DiagramPart.Geometry) ? accent : line,
                Hot(DiagramPart.Geometry) ? 2.2 : 1.4, Hot(DiagramPart.Geometry));
            double cPx = coverMm * scale;
            if (cPx > 0.5)
            {
                AddTechRect(x + cPx, y + cPx, w - 2 * cPx, h - 2 * cPx, null,
                    Hot(DiagramPart.Cover) ? accent : Soft(muted, 160),
                    Hot(DiagramPart.Cover) ? 1.6 : 0.9, false);
                TechDimHorizontal(x, y - 10, cPx, Fmt(coverMm), Hot(DiagramPart.Cover) ? accent : muted, Hot(DiagramPart.Cover));
            }

            // Tie centreline
            double tIns = (coverMm + tieDiaMm * 0.5) * scale;
            double cx0 = x + tIns, cy0 = y + tIns, cw = w - 2 * tIns, ch = h - 2 * tIns;
            if (cw > 4 && ch > 4)
            {
                DrawClosedStirrup(cx0, cy0, cw, ch, hookAng, tieDiaMm, scale, tieBrush, hotTie);
                // Extra tie patterns on top of closed perimeter
                if (arr.TieCase is not ColumnTieCase.Closed)
                    DrawColumnTiesInner(arr.TieCase, cx0, cy0, cw, ch, tieBrush, hotTie, muted);
                if (hotTie) AddLegendDia(_snap.StirrupDia);
            }

            // Long bars: UV in clear cage inside ties; offset by φ/2 toward centre from tie face
            foreach (var bp in arr.Bars)
            {
                double pad = BarR(bp.Dia, scale);
                double px = cx0 + pad + bp.U * Math.Max(1, cw - 2 * pad);
                double py = cy0 + pad + bp.V * Math.Max(1, ch - 2 * pad);
                AddBarDot(px, py, new SolidColorBrush(ColorForDia(bp.Dia)), hotBars, r: BarR(bp.Dia, scale));
                if (hotBars) AddLegendDia(bp.Dia.ToString());
            }

            TechDimHorizontal(x, y + h + 6, w, Fmt(bMm, "b"), muted, Hot(DiagramPart.Geometry));
            TechDimVertical(x + w + 8, y, h, Fmt(dMm, "D"), muted, Hot(DiagramPart.Geometry));
            Label(x + w / 2, y + h + 22, arr.Label, hotTie || hotBars ? accent : muted, 8);
            if (arr.EstBarSpacing > 0)
                Label(x + w / 2, y + h + 34, $"pitch={Fmt(arr.EstBarSpacing)} · {arr.TotalBars} bars", muted, 8);
        }

        // ——— Elevation (true height × breadth) ———
        double ey = y + h + 48;
        const double elevAvailH = 150, elevAvailW = 70;
        double elevScale = hMm > 0
            ? Math.Min(elevAvailH / hMm, elevAvailW / bMm)
            : scale * 0.5;
        double eW = Math.Max(12, bMm * elevScale);
        double eH = Math.Max(24, hMm > 0 ? hMm * elevScale : elevAvailH * 0.6);
        double eX = 40;
        ScaleBadge(8, ey - 12, elevScale, muted, "EL");

        AddTechRect(eX, ey, eW, eH, Fill(line, 28),
            Hot(DiagramPart.Geometry) ? accent : line, Hot(DiagramPart.Geometry) ? 1.8 : 1.2, Hot(DiagramPart.Geometry));

        // Long bars as vertical lines at cover+tie+φ/2
        var longFlat = FlattenBars(ParseBars(_snap.LongBars, 16, 4));
        if (longFlat.Count > 0)
        {
            int maxDia = longFlat.Max();
            double inset = (coverMm + tieDiaMm + maxDia * 0.5) * elevScale;
            double bx0 = eX + inset, bx1 = eX + eW - inset;
            if (bx1 < bx0) { bx0 = eX + 2; bx1 = eX + eW - 2; }
            // Show corner bars (+ mids if many)
            void VBar(double bx, int dia)
            {
                var br = new SolidColorBrush(ColorForDia(dia));
                AddGlowLine(bx, ey + 2, bx, ey + eH - 2, br, hotBars);
            }
            VBar(bx0, longFlat[0]);
            VBar(bx1, longFlat[^1]);
            if (longFlat.Count >= 4)
            {
                VBar((bx0 + bx1) / 2, longFlat[longFlat.Count / 2]);
            }
        }

        // Tie ticks at spacing
        double tieSp = _snap.SpacingSupport > 0 ? _snap.SpacingSupport : 150;
        if (tieSp > 0 && elevScale > 0)
        {
            double pitch = Math.Max(tieSp * elevScale, 3);
            int n = 0;
            for (double ty = ey + pitch * 0.5; ty < ey + eH - 1 && n < 50; ty += pitch, n++)
                AddGlowLine(eX + 1, ty, eX + eW - 1, ty, Soft(tieBrush, 180), hotTie);
            Label(eX + eW / 2, ey - 2, $"ties@{Fmt(tieSp)}", hotTie ? tieBrush : muted, 8);
        }

        if (_snap.ProvideLap && _snap.LapMm > 0)
        {
            double lapH = Math.Min(_snap.LapMm * elevScale, eH * 0.45);
            double lapY = ey + (eH - lapH) / 2;
            AddTechRect(eX - 3, lapY, eW + 6, lapH, Fill(accent, 55), accent, 1.4, true);
            Label(eX + eW + 28, lapY + lapH / 2 - 6, $"Lap={Fmt(_snap.LapMm)}", accent, 8);
            TechDimVertical(eX + eW + 10, lapY, lapH, Fmt(_snap.LapMm), accent, true);
        }

        TechDimVertical(eX + eW + 48, ey, eH, hMm > 0 ? Fmt(hMm, "ℓ") : "ℓ —",
            Hot(DiagramPart.Geometry) ? accent : muted, Hot(DiagramPart.Geometry) || hMm <= 0);
        TechDimHorizontal(eX, ey + eH + 6, eW, Fmt(bMm, "b"), muted, false);

        if (!string.IsNullOrWhiteSpace(_snap.LongBars))
        {
            var groups = ParseBars(_snap.LongBars, 16, 0);
            if (groups.Count > 0)
            {
                var mark = string.Join("+", groups.Select(g => $"{g.nos}-{g.dia}Φ"));
                Label(eX + eW / 2 + 80, ey + eH / 2, mark, muted, 8);
            }
        }

        if (Hot(DiagramPart.Pedestal) || _part == DiagramPart.Pedestal)
        {
            AddTechRect(eX - 8, ey + eH, eW + 16, 14, Fill(accent, 40), accent, 1.6, true);
            Label(eX + eW / 2, ey + eH + 16, "Pedestal", accent, 8);
        }

        if (hotTie || hotBars)
            _hint.Text = arr.Note;
    }

    private void DrawColumnTiesInner(ColumnTieCase tieCase, double x, double y, double w, double h,
        Brush tie, bool glow, Brush muted)
    {
        switch (tieCase)
        {
            case ColumnTieCase.CrossTies:
                AddGlowLine(x + 2, y + h / 2, x + w - 2, y + h / 2, tie, glow);
                AddGlowLine(x + w / 2, y + 2, x + w / 2, y + h - 2, tie, glow);
                break;
            case ColumnTieCase.DiagonalTies:
                AddGlowLine(x + w / 2, y + 2, x + w - 2, y + h / 2, tie, glow);
                AddGlowLine(x + w - 2, y + h / 2, x + w / 2, y + h - 2, tie, glow);
                AddGlowLine(x + w / 2, y + h - 2, x + 2, y + h / 2, tie, glow);
                AddGlowLine(x + 2, y + h / 2, x + w / 2, y + 2, tie, glow);
                break;
            case ColumnTieCase.OpenTies:
                double inset = Math.Min(w, h) * 0.28;
                AddGlowLine(x + inset, y + 2, x + inset, y + h - 2, tie, glow);
                AddGlowLine(x + w - inset, y + 2, x + w - inset, y + h - 2, tie, glow);
                break;
            case ColumnTieCase.UTies:
                double u = Math.Min(w, h) * 0.32;
                AddGlowLine(x + u, y + 2, x + u, y + h - 2, tie, glow);
                AddGlowLine(x + 2, y + 2, x + u, y + 2, Soft(tie, 200), glow);
                AddGlowLine(x + 2, y + h - 2, x + u, y + h - 2, Soft(tie, 200), glow);
                AddGlowLine(x + w - u, y + 2, x + w - u, y + h - 2, tie, glow);
                AddGlowLine(x + w - u, y + 2, x + w - 2, y + 2, Soft(tie, 200), glow);
                AddGlowLine(x + w - u, y + h - 2, x + w - 2, y + h - 2, Soft(tie, 200), glow);
                break;
            case ColumnTieCase.GroupTies:
                double g = Math.Min(w, h) * 0.34;
                AddTechRect(x + 1, y + 1, g, g, null, tie, glow ? 1.8 : 1.2, glow);
                AddTechRect(x + w - g - 1, y + 1, g, g, null, tie, glow ? 1.8 : 1.2, glow);
                AddTechRect(x + 1, y + h - g - 1, g, g, null, tie, glow ? 1.8 : 1.2, glow);
                AddTechRect(x + w - g - 1, y + h - g - 1, g, g, null, tie, glow ? 1.8 : 1.2, glow);
                break;
        }
    }

    private void DrawColumnTies(ColumnTieCase tieCase, double x, double y, double w, double h,
        Brush tie, bool glow, Brush muted)
    {
        // Peripheral closed (all except pure circular/spiral sketch)
        if (tieCase is ColumnTieCase.Circular)
        {
            double cx = x + w / 2, cy = y + h / 2, r = Math.Min(w, h) / 2 - 2;
            AddEllipseRing(cx, cy, r, tie, glow);
            return;
        }
        if (tieCase is ColumnTieCase.Spiral)
        {
            double cx = x + w / 2, cy = y + h / 2, r = Math.Min(w, h) / 2 - 2;
            AddEllipseRing(cx, cy, r, tie, glow);
            AddEllipseRing(cx, cy, r * 0.7, Soft(tie, 160), glow);
            return;
        }

        AddGlowRect(x, y, w, h, glow, tie, muted, false);

        switch (tieCase)
        {
            case ColumnTieCase.CrossTies:
                // Horizontal + vertical cross ties through mid-side bars
                AddGlowLine(x + 4, y + h / 2, x + w - 4, y + h / 2, tie, glow);
                AddGlowLine(x + w / 2, y + 4, x + w / 2, y + h - 4, tie, glow);
                break;
            case ColumnTieCase.DiagonalTies:
                // Rotated square connecting mid-sides
                AddGlowLine(x + w / 2, y + 4, x + w - 4, y + h / 2, tie, glow);
                AddGlowLine(x + w - 4, y + h / 2, x + w / 2, y + h - 4, tie, glow);
                AddGlowLine(x + w / 2, y + h - 4, x + 4, y + h / 2, tie, glow);
                AddGlowLine(x + 4, y + h / 2, x + w / 2, y + 4, tie, glow);
                break;
            case ColumnTieCase.OpenTies:
                // Open / U ties for intermediate bars on long sides
                double inset = Math.Min(w, h) * 0.28;
                AddGlowLine(x + inset, y + 4, x + inset, y + h - 4, tie, glow);
                AddGlowLine(x + w - inset, y + 4, x + w - inset, y + h - 4, tie, glow);
                AddGlowLine(x + 4, y + inset, x + inset, y + inset, Soft(tie, 180), glow);
                AddGlowLine(x + w - inset, y + inset, x + w - 4, y + inset, Soft(tie, 180), glow);
                AddGlowLine(x + 4, y + h - inset, x + inset, y + h - inset, Soft(tie, 180), glow);
                AddGlowLine(x + w - inset, y + h - inset, x + w - 4, y + h - inset, Soft(tie, 180), glow);
                break;
            case ColumnTieCase.UTies:
                double u = Math.Min(w, h) * 0.32;
                // Two U shapes from opposite faces
                AddGlowLine(x + u, y + 4, x + u, y + h - 4, tie, glow);
                AddGlowLine(x + 4, y + 4, x + u, y + 4, Soft(tie, 200), glow);
                AddGlowLine(x + 4, y + h - 4, x + u, y + h - 4, Soft(tie, 200), glow);
                AddGlowLine(x + w - u, y + 4, x + w - u, y + h - 4, tie, glow);
                AddGlowLine(x + w - u, y + 4, x + w - 4, y + 4, Soft(tie, 200), glow);
                AddGlowLine(x + w - u, y + h - 4, x + w - 4, y + h - 4, Soft(tie, 200), glow);
                break;
            case ColumnTieCase.GroupTies:
                double g = Math.Min(w, h) * 0.34;
                AddGlowRect(x + 2, y + 2, g, g, glow, tie, muted, false);
                AddGlowRect(x + w - g - 2, y + 2, g, g, glow, tie, muted, false);
                AddGlowRect(x + 2, y + h - g - 2, g, g, glow, tie, muted, false);
                AddGlowRect(x + w - g - 2, y + h - g - 2, g, g, glow, tie, muted, false);
                break;
        }
    }

    private void AddEllipseRing(double cx, double cy, double r, Brush stroke, bool glow)
    {
        if (glow)
        {
            AddEllipse(cx, cy, r + 3, Soft(stroke, 40), Soft(stroke, 80));
        }
        var el = new Ellipse
        {
            Width = r * 2,
            Height = r * 2,
            Fill = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
            Stroke = stroke,
            StrokeThickness = glow ? 2.5 : 1.5
        };
        Canvas.SetLeft(el, cx - r);
        Canvas.SetTop(el, cy - r);
        _canvas.Children.Add(el);
    }

    private void DrawSlab(Brush muted, Brush line, Brush accent)
    {
        double lx = Math.Max(_snap.L, 1);   // shorter span ℓx
        double ly = Math.Max(_snap.Ly, lx); // longer span ℓy
        double th = Math.Max(_snap.D, 1);
        double cover = Math.Max(_snap.Cover, 0);
        double spX = _snap.SpacingX > 0 ? _snap.SpacingX : 150;
        double spY = _snap.SpacingY > 0 ? _snap.SpacingY : 150;
        int.TryParse(_snap.DiaX, out var diaX); if (diaX <= 0) diaX = 10;
        int.TryParse(_snap.DiaY, out var diaY); if (diaY <= 0) diaY = 10;
        int cranks = Math.Clamp(_snap.CrankCount, 0, 2);
        double rise = _snap.CrankRise > 0 ? _snap.CrankRise : Math.Max(0, th - 2 * cover);
        bool oneWay = string.Equals(_snap.SlabType, "One-Way", StringComparison.OrdinalIgnoreCase);

        // ——— Plan: ℓy horizontal × ℓx vertical (IS convention) ———
        const double planBoxW = 220, planBoxH = 140;
        double planScale = Math.Min(planBoxW / ly, planBoxH / lx);
        double pw = ly * planScale, ph = lx * planScale;
        double px = 32 + (planBoxW - pw) / 2, py = 20 + (planBoxH - ph) / 2;
        ScaleBadge(8, 4, planScale, muted, "PLAN");

        AddTechRect(px, py, pw, ph, Fill(line, 18),
            Hot(DiagramPart.Geometry) ? accent : line, Hot(DiagramPart.Geometry) ? 2 : 1.3, Hot(DiagramPart.Geometry));

        double cPx = cover * planScale;
        if (cPx > 0.6)
            AddTechRect(px + cPx, py + cPx, pw - 2 * cPx, ph - 2 * cPx, null,
                Hot(DiagramPart.Cover) ? accent : Soft(muted, 150), Hot(DiagramPart.Cover) ? 1.4 : 0.8, false);

        var xCol = DiaBrush(_snap.DiaX, Color.FromArgb(255, 34, 197, 94));
        var yCol = DiaBrush(_snap.DiaY, Color.FromArgb(255, 249, 115, 22));
        double mx0 = px + cPx + diaY * 0.5 * planScale;
        double mx1 = px + pw - cPx - diaY * 0.5 * planScale;
        double my0 = py + cPx + diaX * 0.5 * planScale;
        double my1 = py + ph - cPx - diaX * 0.5 * planScale;

        // Main ℓx bars run along ℓx (vertical on plan), spaced across ℓy at spacing_x
        DrawMeshLines(mx0, my0, mx1, my1, spX, planScale, horizontal: false,
            xCol, Hot(DiagramPart.MainX), max: 30);
        // ℓy / distribution bars run along ℓy (horizontal), spaced across ℓx at spacing_y
        DrawMeshLines(mx0, my0, mx1, my1, spY, planScale, horizontal: true,
            yCol, Hot(DiagramPart.MainY), max: 30);

        if (Hot(DiagramPart.MainX))
        {
            AddLegendDia(_snap.DiaX);
            Label(px + pw / 2, py + ph / 2 - 8, $"{diaX}Φ@{Fmt(spX)} //ℓx", xCol, 8);
        }
        if (Hot(DiagramPart.MainY))
        {
            AddLegendDia(_snap.DiaY);
            Label(px + pw / 2, py + ph / 2 + 6,
                oneWay ? $"{diaY}Φ@{Fmt(spY)} dist //ℓy" : $"{diaY}Φ@{Fmt(spY)} //ℓy", yCol, 8);
        }

        TechDimHorizontal(px, py + ph + 6, pw, Fmt(ly, "ℓy"), muted, Hot(DiagramPart.Geometry));
        TechDimVertical(px + pw + 8, py, ph, Fmt(lx, "ℓx"), muted, Hot(DiagramPart.Geometry));
        Label(px + pw / 2, py - 12,
            string.IsNullOrWhiteSpace(_snap.SlabType) ? "Slab" : _snap.SlabType, muted, 8);

        // ——— Section along ℓx (true thickness) ———
        double ey = py + ph + 34;
        const double elevAvailW = 230, elevAvailH = 72;
        double elevScale = Math.Min(elevAvailW / lx, elevAvailH / Math.Max(th, 1));
        double eW = lx * elevScale, eH = Math.Max(10, th * elevScale);
        double eX = 32 + (elevAvailW - eW) / 2;
        ScaleBadge(8, ey - 12, elevScale, muted, "SEC");

        AddTechRect(eX, ey, eW, eH, Fill(line, 22),
            Hot(DiagramPart.Geometry) ? accent : line, Hot(DiagramPart.Geometry) ? 1.8 : 1.2, Hot(DiagramPart.Geometry));

        double ec = cover * elevScale;
        if (ec > 0.4)
        {
            AddLine(eX + ec, ey + eH - ec, eX + eW - ec, ey + eH - ec,
                Hot(DiagramPart.Cover) ? accent : Soft(muted, 160), Hot(DiagramPart.Cover) ? 1.3 : 0.8);
            TechDimVertical(eX - 14, ey + eH - ec, ec, Fmt(cover),
                Hot(DiagramPart.Cover) ? accent : muted, Hot(DiagramPart.Cover));
        }

        // Bottom main bars (along ℓx) as continuous line + distribution dots
        double barY = ey + eH - (cover + diaX * 0.5) * elevScale;
        var bent = Hot(DiagramPart.BentUp) ? accent : xCol;
        if (cranks > 0 && rise > 0)
        {
            // Bent-up profile to scale (rise = D − 2c typically)
            double risePx = rise * elevScale;
            double seg = eW / (cranks * 2 + 2);
            double x0 = eX + ec;
            double yBot = barY;
            double yTop = ey + (cover + diaX * 0.5) * elevScale;
            // approximate: end rise, mid flat (or two cranks)
            var pts = new List<(double x, double y)> { (x0, yBot) };
            pts.Add((x0 + seg, yTop));
            pts.Add((eX + eW - ec - seg, yTop));
            pts.Add((eX + eW - ec, yBot));
            if (cranks >= 2)
            {
                // mid dip back toward bottom
                pts = new List<(double x, double y)>
                {
                    (x0, yBot),
                    (x0 + seg, yTop),
                    (eX + eW / 2 - seg * 0.3, yTop),
                    (eX + eW / 2, yBot),
                    (eX + eW / 2 + seg * 0.3, yTop),
                    (eX + eW - ec - seg, yTop),
                    (eX + eW - ec, yBot)
                };
            }
            for (int i = 0; i < pts.Count - 1; i++)
                AddGlowLine(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, bent, Hot(DiagramPart.BentUp) || Hot(DiagramPart.MainX));
            Label(eX + eW / 2, ey - 2, $"bent-up ×{cranks} rise={Fmt(rise)}",
                Hot(DiagramPart.BentUp) ? accent : muted, 8);
            TechDimVertical(eX + eW + 28, yTop, Math.Max(1, yBot - yTop), Fmt(rise, "rise"),
                Hot(DiagramPart.BentUp) ? accent : muted, Hot(DiagramPart.BentUp));
        }
        else
        {
            AddGlowLine(eX + ec, barY, eX + eW - ec, barY, xCol, Hot(DiagramPart.MainX));
        }

        // Distribution as dots along section (across ℓx = into plane of section along ly... 
        // section is along ℓx so dist bars appear as circles)
        for (double tx = eX + ec + diaY * 0.5 * elevScale;
             tx < eX + eW - ec;
             tx += Math.Max(spY * elevScale, 4))
            AddBarDot(tx, barY, yCol, Hot(DiagramPart.MainY), r: BarR(diaY, elevScale));

        if (Hot(DiagramPart.MainX)) AddLegendDia(_snap.DiaX);
        if (Hot(DiagramPart.MainY)) AddLegendDia(_snap.DiaY);

        TechDimHorizontal(eX, ey + eH + 8, eW, Fmt(lx, "ℓx"), muted, Hot(DiagramPart.Geometry));
        TechDimVertical(eX + eW + 10, ey, eH, Fmt(th, "D"), muted, Hot(DiagramPart.Geometry));
        Label(eX + eW / 2, ey + eH + 24,
            $"sec. along ℓx · {diaX}Φ@{Fmt(spX)} / {diaY}Φ@{Fmt(spY)}", muted, 8);
    }

    private void DrawFooting(Brush muted, Brush line, Brush accent)
    {
        double lenL = Math.Max(_snap.L, 1);  // footing length L
        double brB = Math.Max(_snap.B, 1);   // footing breadth B
        double D = Math.Max(_snap.D, 1);
        double cover = Math.Max(_snap.Cover, 0);
        double colL = Math.Max(_snap.ColDimL, 0);
        double colB = Math.Max(_snap.ColDimB, 0);
        // spacing_l = spacing of bars that run along L (counted across B)
        // spacing_b = spacing of bars that run along B (counted across L)
        double spL = _snap.MeshSpacingL > 0 ? _snap.MeshSpacingL : 150;
        double spB = _snap.MeshSpacingB > 0 ? _snap.MeshSpacingB : 150;
        int.TryParse(_snap.DiaL, out var diaL); if (diaL <= 0) diaL = 12;
        int.TryParse(_snap.DiaB, out var diaB); if (diaB <= 0) diaB = 12;
        bool hasStub = colL > 0 && colB > 0
            && !string.Equals(_snap.FootingType, "Strip", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(_snap.FootingType, "Raft", StringComparison.OrdinalIgnoreCase);

        // ——— Plan: L horizontal × B vertical ———
        const double planBoxW = 210, planBoxH = 130;
        double planScale = Math.Min(planBoxW / lenL, planBoxH / brB);
        double pw = lenL * planScale, ph = brB * planScale;
        double px = 36 + (planBoxW - pw) / 2, py = 20 + (planBoxH - ph) / 2;
        ScaleBadge(8, 4, planScale, muted, "PLAN");

        AddTechRect(px, py, pw, ph, Fill(line, 18),
            Hot(DiagramPart.Geometry) ? accent : line, Hot(DiagramPart.Geometry) ? 2 : 1.3, Hot(DiagramPart.Geometry));

        double cPx = cover * planScale;
        if (cPx > 0.8)
            AddTechRect(px + cPx, py + cPx, pw - 2 * cPx, ph - 2 * cPx, null,
                Hot(DiagramPart.Cover) ? accent : Soft(muted, 150), Hot(DiagramPart.Cover) ? 1.4 : 0.8, false);

        var brushL = DiaBrush(_snap.DiaL, Color.FromArgb(255, 239, 68, 68));
        var brushB = DiaBrush(_snap.DiaB, Color.FromArgb(255, 14, 165, 233));
        double meshX0 = px + cPx + diaL * 0.5 * planScale;
        double meshX1 = px + pw - cPx - diaL * 0.5 * planScale;
        double meshY0 = py + cPx + diaB * 0.5 * planScale;
        double meshY1 = py + ph - cPx - diaB * 0.5 * planScale;

        // Main-L: bars run along L (horizontal), spaced across B at spacing_l
        DrawMeshLines(meshX0, meshY0, meshX1, meshY1, spL, planScale, horizontal: true,
            brushL, Hot(DiagramPart.BottomMesh), max: 28);
        // Main-B: bars run along B (vertical), spaced across L at spacing_b
        DrawMeshLines(meshX0, meshY0, meshX1, meshY1, spB, planScale, horizontal: false,
            brushB, Hot(DiagramPart.BottomMesh), max: 28);
        if (Hot(DiagramPart.BottomMesh))
        {
            AddLegendDia(_snap.DiaL);
            AddLegendDia(_snap.DiaB);
            Label(px + pw / 2, py + ph / 2 - 6,
                $"{diaL}Φ@{Fmt(spL)} along L", brushL, 8);
            Label(px + pw / 2, py + ph / 2 + 8,
                $"{diaB}Φ@{Fmt(spB)} along B", brushB, 8);
        }

        if (hasStub)
        {
            double cw = colL * planScale, ch = colB * planScale;
            double cx = px + (pw - cw) / 2, cy = py + (ph - ch) / 2;
            AddTechRect(cx, cy, cw, ch, Fill(accent, 35),
                Hot(DiagramPart.ColumnStub) ? accent : line, Hot(DiagramPart.ColumnStub) ? 2 : 1.3,
                Hot(DiagramPart.ColumnStub));
            // Column size dims along L / B
            TechDimHorizontal(cx, cy - 8, cw, Fmt(colL, "col L"),
                Hot(DiagramPart.ColumnStub) ? accent : muted, Hot(DiagramPart.ColumnStub));
            TechDimVertical(cx + cw + 4, cy, ch, Fmt(colB, "col B"),
                Hot(DiagramPart.ColumnStub) ? accent : muted, Hot(DiagramPart.ColumnStub));
        }

        if (!string.IsNullOrWhiteSpace(_snap.TopDiaL) || Hot(DiagramPart.TopMesh))
        {
            var topB = DiaBrush(_snap.TopDiaL, Color.FromArgb(255, 168, 85, 247));
            AddGlowLine(px + 8, py + 8, px + pw - 8, py + 8, topB, Hot(DiagramPart.TopMesh));
            AddGlowLine(px + 8, py + ph - 8, px + pw - 8, py + ph - 8, topB, Hot(DiagramPart.TopMesh));
            if (Hot(DiagramPart.TopMesh)) AddLegendDia(_snap.TopDiaL);
        }

        // Clear L / B labels — length vs breadth
        TechDimHorizontal(px, py + ph + 8, pw, Fmt(lenL, "L (length)"), muted, Hot(DiagramPart.Geometry));
        TechDimVertical(px + pw + 10, py, ph, Fmt(brB, "B (breadth)"), muted, Hot(DiagramPart.Geometry));
        string typeLab = string.IsNullOrWhiteSpace(_snap.FootingType) ? "Footing" : _snap.FootingType;
        Label(px + pw / 2, py - 14, $"{typeLab} plan", muted, 8);

        // ——— Section along length L (cut parallel to L, looking at B face) ———
        double ey = py + ph + 36;
        const double elevAvailW = 220, elevAvailH = 68;
        double elevScale = Math.Min(elevAvailW / lenL, elevAvailH / D);
        double eW = lenL * elevScale, eH = D * elevScale;
        double eX = 36 + (elevAvailW - eW) / 2;
        ScaleBadge(8, ey - 14, elevScale, muted, "SEC");
        Label(eX + eW / 2, ey - 14, "Section along L", muted, 8);

        AddTechRect(eX, ey, eW, eH, Fill(line, 22),
            Hot(DiagramPart.Geometry) ? accent : line, Hot(DiagramPart.Geometry) ? 1.8 : 1.2, Hot(DiagramPart.Geometry));

        double ec = cover * elevScale;
        if (ec > 0.5)
        {
            AddLine(eX + ec, ey + eH - ec, eX + eW - ec, ey + eH - ec,
                Hot(DiagramPart.Cover) ? accent : Soft(muted, 160), Hot(DiagramPart.Cover) ? 1.4 : 0.8);
            TechDimVertical(eX - 14, ey + eH - ec, ec, Fmt(cover, "c"),
                Hot(DiagramPart.Cover) ? accent : muted, Hot(DiagramPart.Cover));
        }

        // In section along L: Main-L bars appear as continuous lines (length into section);
        // Main-B bars appear as dots spaced at spacing_b along L.
        double barY = ey + eH - (cover + diaL * 0.5) * elevScale;
        AddGlowLine(eX + ec, barY, eX + eW - ec, barY, brushL, Hot(DiagramPart.BottomMesh));
        for (double tx = eX + ec + diaB * 0.5 * elevScale;
             tx < eX + eW - ec;
             tx += Math.Max(spB * elevScale, 4))
            AddBarDot(tx, barY, brushB, Hot(DiagramPart.BottomMesh), r: BarR(diaB, elevScale));

        if (hasStub)
        {
            double stubW = colL * elevScale;
            double stubH = Math.Min(36, Math.Max(eH * 0.9, 18));
            double stubX = eX + (eW - stubW) / 2;
            AddTechRect(stubX, ey - stubH + 2, stubW, stubH, Fill(accent, 40),
                Hot(DiagramPart.ColumnStub) ? accent : line, 1.4, Hot(DiagramPart.ColumnStub));
            TechDimHorizontal(stubX, ey - stubH - 6, stubW, Fmt(colL, "col L"),
                Hot(DiagramPart.ColumnStub) ? accent : muted, Hot(DiagramPart.ColumnStub));
        }

        int steps = Math.Clamp(_snap.NRisers, 0, 6);
        if (string.Equals(_snap.FootingType, "Stepped", StringComparison.OrdinalIgnoreCase) && steps >= 2)
        {
            double stepH = eH / steps;
            for (int i = 1; i < steps; i++)
            {
                double inset = eW * (0.08 * i);
                AddLine(eX + inset, ey + stepH * i, eX + eW - inset, ey + stepH * i, Soft(line, 180), 1);
            }
            Label(eX + eW / 2, ey + 2, $"{steps} steps", muted, 8);
        }

        TechDimHorizontal(eX, ey + eH + 8, eW, Fmt(lenL, "L (length)"), muted, Hot(DiagramPart.Geometry));
        TechDimVertical(eX + eW + 10, ey, eH, Fmt(D, "D"), muted, Hot(DiagramPart.Geometry));
        Label(eX + eW / 2, ey + eH + 24,
            $"Main-L {diaL}Φ@{Fmt(spL)} · Main-B {diaB}Φ@{Fmt(spB)}", muted, 8);
    }

    private void DrawMeshLines(double x0, double y0, double x1, double y1, double spacingMm, double scale,
        bool horizontal, Brush brush, bool glow, int max = 24)
    {
        if (spacingMm <= 0 || scale <= 0) return;
        double pitch = Math.Max(spacingMm * scale, 3);
        int n = 0;
        if (horizontal)
        {
            // lines of constant y
            double span = Math.Abs(y1 - y0);
            if (span < 1) { AddGlowLine(x0, y0, x1, y0, brush, glow); return; }
            for (double y = Math.Min(y0, y1); y <= Math.Max(y0, y1) + 0.5 && n < max; y += pitch, n++)
                AddGlowLine(x0, y, x1, y, brush, glow);
        }
        else
        {
            for (double x = Math.Min(x0, x1); x <= Math.Max(x0, x1) + 0.5 && n < max; x += pitch, n++)
                AddGlowLine(x, y0, x, y1, brush, glow);
        }
    }

    private void DrawWall(Brush muted, Brush line, Brush accent)
    {
        double stemH = Math.Max(_snap.Height, 1);
        double stemT = Math.Max(_snap.D, 1);
        double heel = Math.Max(_snap.Heel, 0);
        double toe = Math.Max(_snap.Toe, 0);
        double baseT = Math.Max(_snap.BaseThickness, 1);
        double cover = Math.Max(_snap.Cover, 0);
        double wallLen = Math.Max(_snap.L, 0);
        bool tensionFront = !string.Equals(_snap.TensionFace, "Back", StringComparison.OrdinalIgnoreCase);

        int.TryParse(_snap.DiaX, out var vDia); if (vDia <= 0) vDia = 12;
        int.TryParse(_snap.DiaY, out var hDia); if (hDia <= 0) hDia = 10;
        int.TryParse(_snap.DiaL, out var baseLDia); if (baseLDia <= 0) baseLDia = 12;
        int.TryParse(_snap.DiaBaseB, out var baseBDia); if (baseBDia <= 0) baseBDia = 12;
        int.TryParse(_snap.DiaBack, out var backDia);
        int.TryParse(_snap.LinkDia, out var linkDia);

        double vSp = _snap.StemVSpacing > 0 ? _snap.StemVSpacing : 150;
        double hSp = _snap.StemHSpacing > 0 ? _snap.StemHSpacing : 200;
        double baseSp = _snap.BaseLSpacing > 0 ? _snap.BaseLSpacing : 150;

        // Overall section envelope: toe + stem + heel ; height = stem + base
        double totalW = toe + stemT + heel;
        double totalH = stemH + baseT;
        const double boxW = 240, boxH = 280;
        double scale = Math.Min(boxW / Math.Max(totalW, 1), boxH / Math.Max(totalH, 1));
        double ox = 40, oy = 20; // top-left of section bounding box
        // Stem sits on base: x of stem front face
        double stemX = ox + toe * scale;
        double stemY = oy;
        double stemW = stemT * scale;
        double stemHpx = stemH * scale;
        double baseY = oy + stemHpx;
        double baseX = ox;
        double baseW = totalW * scale;
        double baseHpx = baseT * scale;
        ScaleBadge(8, 4, scale, muted, "SEC");

        // Base slab
        AddTechRect(baseX, baseY, baseW, baseHpx, Fill(line, 22),
            Hot(DiagramPart.Geometry) ? accent : line, Hot(DiagramPart.Geometry) ? 2 : 1.3, Hot(DiagramPart.Geometry));
        // Stem
        AddTechRect(stemX, stemY, stemW, stemHpx, Fill(line, 22),
            Hot(DiagramPart.Geometry) ? accent : line, Hot(DiagramPart.Geometry) ? 2 : 1.3, Hot(DiagramPart.Geometry));

        // Cover on stem (both faces) and base top/bottom
        double c = cover * scale;
        if (c > 0.5)
        {
            var cBrush = Hot(DiagramPart.Cover) ? accent : Soft(muted, 150);
            AddTechRect(stemX + c, stemY + c, Math.Max(1, stemW - 2 * c), Math.Max(1, stemHpx - c),
                null, cBrush, Hot(DiagramPart.Cover) ? 1.3 : 0.8, false);
            AddLine(baseX + c, baseY + baseHpx - c, baseX + baseW - c, baseY + baseHpx - c, cBrush, 0.9);
            if (Hot(DiagramPart.Cover))
                TechDimHorizontal(stemX, stemY - 10, c, Fmt(cover), accent, true);
        }

        // Main vertical — tension face
        var vBrush = DiaBrush(_snap.DiaX, Color.FromArgb(255, 34, 197, 94));
        double tensX = tensionFront
            ? stemX + (cover + vDia * 0.5) * scale
            : stemX + stemW - (cover + vDia * 0.5) * scale;
        double vTop = stemY + (cover + vDia * 0.5) * scale;
        double vBot = baseY + baseHpx - (cover + vDia * 0.5) * scale; // embed into base
        AddGlowLine(tensX, vTop, tensX, vBot, vBrush, Hot(DiagramPart.StemMain));
        // Show spacing as additional verticals across wall length — on section, draw 2–3 parallel if thick enough
        if (stemW > 20)
        {
            double secondX = tensionFront
                ? tensX + Math.Min(8, stemW * 0.15)
                : tensX - Math.Min(8, stemW * 0.15);
            AddGlowLine(secondX, vTop, secondX, stemY + stemHpx - c, Soft(vBrush, 160), Hot(DiagramPart.StemMain));
        }
        if (Hot(DiagramPart.StemMain))
        {
            AddLegendDia(_snap.DiaX);
            Label(tensX + (tensionFront ? 14 : -14), stemY + stemHpx / 2,
                $"{vDia}Φ@{Fmt(vSp)}", vBrush, 8);
        }

        // Secondary face vertical
        if (backDia > 0)
        {
            var backBrush = DiaBrush(_snap.DiaBack, Color.FromArgb(255, 20, 184, 166));
            double backX = tensionFront
                ? stemX + stemW - (cover + backDia * 0.5) * scale
                : stemX + (cover + backDia * 0.5) * scale;
            AddGlowLine(backX, vTop, backX, stemY + stemHpx - c, backBrush, Hot(DiagramPart.StemMain));
            if (Hot(DiagramPart.StemMain)) AddLegendDia(_snap.DiaBack);
        }

        // Horizontal distribution — ticks at StemHSpacing
        var hBrush = DiaBrush(_snap.DiaY, Color.FromArgb(255, 249, 115, 22));
        double hx0 = stemX + (cover + hDia * 0.5) * scale;
        double hx1 = stemX + stemW - (cover + hDia * 0.5) * scale;
        double pitchH = Math.Max(hSp * scale, 4);
        int hn = 0;
        for (double hy = stemY + (cover + hDia * 0.5) * scale;
             hy < stemY + stemHpx - c && hn < 40;
             hy += pitchH, hn++)
            AddGlowLine(hx0, hy, hx1, hy, hBrush, Hot(DiagramPart.StemDist));
        if (Hot(DiagramPart.StemDist))
        {
            AddLegendDia(_snap.DiaY);
            Label(stemX + stemW / 2, stemY + 4, $"{hDia}Φ@{Fmt(hSp)}", hBrush, 8);
        }

        // Optional links
        if (linkDia > 0 && _snap.LinkSpacing > 0)
        {
            var linkBrush = DiaBrush(_snap.LinkDia, Color.FromArgb(255, 14, 165, 233));
            double lp = Math.Max(_snap.LinkSpacing * scale, 6);
            int ln = 0;
            for (double ly = stemY + lp; ly < stemY + stemHpx - 4 && ln < 30; ly += lp, ln++)
            {
                AddGlowLine(hx0, ly - 2, hx0, ly + 2, linkBrush, Hot(DiagramPart.Links));
                AddGlowLine(hx1, ly - 2, hx1, ly + 2, linkBrush, Hot(DiagramPart.Links));
                AddGlowLine(hx0, ly, hx1, ly, Soft(linkBrush, 160), Hot(DiagramPart.Links));
            }
            if (Hot(DiagramPart.Links))
            {
                AddLegendDia(_snap.LinkDia);
                Label(stemX + stemW / 2, stemY + stemHpx / 2 + 12,
                    $"links {_snap.LinkLegs}-leg @{Fmt(_snap.LinkSpacing)}", linkBrush, 8);
            }
        }

        // Base steel — longitudinal (into page shown as dots along base) + longl. lines
        var baseBrush = DiaBrush(_snap.DiaL, Color.FromArgb(255, 239, 68, 68));
        var baseBBrush = DiaBrush(_snap.DiaBaseB, Color.FromArgb(255, 168, 85, 247));
        double baseBarY = baseY + baseHpx - (cover + baseLDia * 0.5) * scale;
        AddGlowLine(baseX + c, baseBarY, baseX + baseW - c, baseBarY, baseBrush, Hot(DiagramPart.BaseSteel));
        // Top of base (heel/toe) sometimes
        double baseTopY = baseY + (cover + baseLDia * 0.5) * scale;
        AddGlowLine(baseX + c, baseTopY, stemX - 2, baseTopY, Soft(baseBrush, 170), Hot(DiagramPart.BaseSteel));
        if (toe > 0)
            AddGlowLine(stemX + stemW + 2, baseTopY, baseX + baseW - c, baseTopY, Soft(baseBrush, 170), Hot(DiagramPart.BaseSteel));

        // Transverse base bars as dots
        double bPitch = Math.Max(baseSp * scale, 4);
        int bn = 0;
        for (double bx = baseX + c + baseBDia * 0.5 * scale;
             bx < baseX + baseW - c && bn < 40;
             bx += bPitch, bn++)
            AddBarDot(bx, baseBarY, baseBBrush, Hot(DiagramPart.BaseSteel), r: BarR(baseBDia, scale));

        if (Hot(DiagramPart.BaseSteel))
        {
            AddLegendDia(_snap.DiaL);
            AddLegendDia(_snap.DiaBaseB);
            Label(baseX + baseW / 2, baseY + baseHpx + 18,
                $"base {baseLDia}Φ@{Fmt(baseSp)} / {baseBDia}Φ", baseBrush, 8);
        }

        // Dimensions
        TechDimVertical(stemX + stemW + 10, stemY, stemHpx, Fmt(stemH, "Hs"), muted, Hot(DiagramPart.Geometry));
        TechDimHorizontal(stemX, stemY + stemHpx - 14, stemW, Fmt(stemT, "ts"), muted, Hot(DiagramPart.Geometry));
        if (heel > 0)
            TechDimHorizontal(stemX + stemW, baseY + baseHpx + 6, heel * scale, Fmt(heel, "heel"), muted, false);
        if (toe > 0)
            TechDimHorizontal(baseX, baseY + baseHpx + 6, toe * scale, Fmt(toe, "toe"), muted, false);
        TechDimHorizontal(baseX, baseY + baseHpx + 22, baseW, Fmt(totalW, "B"), muted, Hot(DiagramPart.Geometry));
        TechDimVertical(baseX - 16, baseY, baseHpx, Fmt(baseT, "tb"), muted, Hot(DiagramPart.Geometry));
        if (wallLen > 0)
            Label(ox + baseW / 2, oy + totalH * scale + 40, Fmt(wallLen, "L"), muted, 9);

        Label(stemX + stemW / 2, oy - 2,
            tensionFront ? "tension→front" : "tension→back", muted, 8);
    }

    // ——— bar helpers ———

    private void DrawBarRow(List<(int dia, int nos)> groups, double x0, double x1, double y, bool hot)
    {
        var positions = ExpandBarPositions(groups, x0, x1);
        foreach (var (cx, dia) in positions)
        {
            var brush = new SolidColorBrush(ColorForDia(dia));
            AddBarDot(cx, y, brush, hot, r: 3.5 + Math.Min(dia, 32) / 10.0);
            if (hot) AddLegendDia(dia.ToString());
        }
    }

    private static List<(double cx, int dia)> ExpandBarPositions(List<(int dia, int nos)> groups, double x0, double x1)
    {
        var dias = new List<int>();
        foreach (var (dia, nos) in groups)
            for (int i = 0; i < Math.Clamp(nos, 1, 8); i++) dias.Add(dia);
        if (dias.Count == 0) dias.AddRange(new[] { 16, 16 });
        var list = new List<(double, int)>();
        for (int i = 0; i < dias.Count; i++)
        {
            double t = dias.Count == 1 ? 0.5 : i / (double)(dias.Count - 1);
            list.Add((x0 + (x1 - x0) * t, dias[i]));
        }
        return list;
    }

    private static List<(int dia, int nos)> ParseBars(string? text, int fallbackDia, int fallbackNos)
    {
        var list = new List<(int, int)>();
        if (string.IsNullOrWhiteSpace(text))
        {
            list.Add((fallbackDia, fallbackNos));
            return list;
        }
        foreach (Match m in Regex.Matches(text, @"(\d+)\s*:\s*(\d+)"))
        {
            if (int.TryParse(m.Groups[1].Value, out var d) && int.TryParse(m.Groups[2].Value, out var n))
                list.Add((d, n));
        }
        if (list.Count == 0 && int.TryParse(text.Trim(), out var onlyDia))
            list.Add((onlyDia, fallbackNos));
        if (list.Count == 0) list.Add((fallbackDia, fallbackNos));
        return list;
    }

    private static Color ColorForDia(int dia)
    {
        if (DiaPalette.TryGetValue(dia, out var c)) return c;
        // hash unknown dias into palette range
        var keys = DiaPalette.Keys.OrderBy(k => k).ToArray();
        return DiaPalette[keys[Math.Abs(dia) % keys.Length]];
    }

    private Brush DiaBrush(string? diaText, Color? fallback = null)
    {
        if (int.TryParse(diaText?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) && d > 0)
            return new SolidColorBrush(ColorForDia(d));
        return new SolidColorBrush(fallback ?? Color.FromArgb(255, 100, 116, 139));
    }

    private void AddLegendDia(string? diaText)
    {
        if (!int.TryParse(diaText?.Trim(), out var d) || d <= 0) return;
        if (_legend.Children.OfType<Border>().Any(b => Equals(b.Tag, d))) return;
        var chip = new Border
        {
            Tag = d,
            Background = new SolidColorBrush(ColorForDia(d)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1, 5, 1),
            Child = new TextBlock
            {
                Text = $"φ{d}",
                FontSize = 9,
                Foreground = new SolidColorBrush(Colors.White),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            }
        };
        _legend.Children.Add(chip);
    }

    // ——— draw primitives with glow ———

    private void AddGlowRect(double x, double y, double w, double h, bool glow, Brush hotBrush, Brush normal,
        bool fill = false, Brush? fillBrush = null)
    {
        if (glow)
        {
            AddRect(x - 3, y - 3, w + 6, h + 6, Soft(hotBrush, 40), Soft(hotBrush, 90), 5, false);
            AddRect(x - 1, y - 1, w + 2, h + 2, null, Soft(hotBrush, 160), 3, false);
        }
        AddRect(x, y, w, h, fill ? (fillBrush ?? Fill(hotBrush, 30)) : null,
            glow ? hotBrush : normal, glow ? 2.5 : 1.4, false);
    }

    private void AddGlowLine(double x1, double y1, double x2, double y2, Brush brush, bool glow)
    {
        if (glow)
        {
            AddLine(x1, y1, x2, y2, Soft(brush, 50), 8);
            AddLine(x1, y1, x2, y2, Soft(brush, 120), 4.5);
        }
        AddLine(x1, y1, x2, y2, brush, glow ? 2.4 : 1.2);
    }

    private void AddBarDot(double cx, double cy, Brush brush, bool glow, double r = 5)
    {
        if (glow)
        {
            AddEllipse(cx, cy, r + 6, Soft(brush, 45), null);
            AddEllipse(cx, cy, r + 3.5, Soft(brush, 110), null);
        }
        AddEllipse(cx, cy, glow ? r + 0.8 : r, brush, glow ? new SolidColorBrush(Colors.White) : null);
    }

    private void AddEllipse(double cx, double cy, double r, Brush fill, Brush? stroke)
    {
        var el = new Ellipse
        {
            Width = r * 2,
            Height = r * 2,
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = stroke is null ? 0 : 1.2
        };
        Canvas.SetLeft(el, cx - r);
        Canvas.SetTop(el, cy - r);
        _canvas.Children.Add(el);
    }

    private void AddTechRect(double x, double y, double w, double h, Brush? fill, Brush stroke,
        double thickness, bool glow = false)
    {
        if (glow)
            AddRect(x - 2, y - 2, w + 4, h + 4, Soft(stroke, 45), Soft(stroke, 100), 3.5, false, sharp: true);
        AddRect(x, y, w, h, fill, stroke, thickness, false, sharp: true);
    }

    private void ScaleBadge(double x, double y, double pxPerMm, Brush brush, string tag = "SEC")
    {
        if (pxPerMm <= 0) return;
        int ratio = (int)Math.Round(1.0 / pxPerMm);
        if (ratio < 1) ratio = 1;
        Label(x + 28, y, $"{tag} 1:{ratio}", Soft(brush, 200), 8);
    }

    private void TechDimHorizontal(double x, double y, double w, string text, Brush brush, bool glow)
    {
        if (w < 0.5) return;
        var b = glow ? brush : Soft(brush, 200);
        // Extension ticks
        AddLine(x, y - 3, x, y + 7, b, 0.9);
        AddLine(x + w, y - 3, x + w, y + 7, b, 0.9);
        AddLine(x, y + 2, x + w, y + 2, b, glow ? 1.3 : 0.9);
        // Arrowheads
        double ah = Math.Min(4, w * 0.15);
        AddLine(x, y + 2, x + ah, y, b, 0.9);
        AddLine(x, y + 2, x + ah, y + 4, b, 0.9);
        AddLine(x + w, y + 2, x + w - ah, y, b, 0.9);
        AddLine(x + w, y + 2, x + w - ah, y + 4, b, 0.9);
        Label(x + w / 2, y + 6, text, glow ? brush : b, glow ? 10 : 9);
    }

    private void TechDimVertical(double x, double y, double h, string text, Brush brush, bool glow)
    {
        if (h < 0.5) return;
        var b = glow ? brush : Soft(brush, 200);
        AddLine(x - 3, y, x + 7, y, b, 0.9);
        AddLine(x - 3, y + h, x + 7, y + h, b, 0.9);
        AddLine(x + 2, y, x + 2, y + h, b, glow ? 1.3 : 0.9);
        double ah = Math.Min(4, h * 0.15);
        AddLine(x + 2, y, x, y + ah, b, 0.9);
        AddLine(x + 2, y, x + 4, y + ah, b, 0.9);
        AddLine(x + 2, y + h, x, y + h - ah, b, 0.9);
        AddLine(x + 2, y + h, x + 4, y + h - ah, b, 0.9);
        var tb = new TextBlock
        {
            Text = text,
            FontSize = glow ? 10 : 9,
            FontWeight = glow ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            Foreground = glow ? brush : b
        };
        tb.Measure(new Windows.Foundation.Size(100, 80));
        Canvas.SetLeft(tb, x + 8);
        Canvas.SetTop(tb, y + h / 2 - tb.DesiredSize.Height / 2);
        _canvas.Children.Add(tb);
    }

    private void AddRect(double x, double y, double w, double h, Brush? fill, Brush stroke, double thickness,
        bool glow = false, bool sharp = false)
    {
        if (glow)
        {
            AddRect(x - 2, y - 2, w + 4, h + 4, Soft(stroke, 50), Soft(stroke, 100), 4, false, sharp);
        }
        var r = new Rectangle
        {
            Width = Math.Max(w, 1),
            Height = Math.Max(h, 1),
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = thickness,
            RadiusX = sharp ? 0 : 2,
            RadiusY = sharp ? 0 : 2
        };
        Canvas.SetLeft(r, x);
        Canvas.SetTop(r, y);
        _canvas.Children.Add(r);
    }

    private void AddLine(double x1, double y1, double x2, double y2, Brush brush, double thickness)
    {
        _canvas.Children.Add(new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = brush,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Flat,
            StrokeEndLineCap = PenLineCap.Flat
        });
    }

    private void DimHorizontal(double x, double y, double w, string text, Brush brush, bool glow) =>
        TechDimHorizontal(x, y, w, text, brush, glow);

    private void DimVertical(double x, double y, double h, string text, Brush brush, bool glow) =>
        TechDimVertical(x, y, h, text, brush, glow);

    private void Label(double cx, double y, string text, Brush brush, double size = 10)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = size,
            Foreground = brush,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        tb.Measure(new Windows.Foundation.Size(220, 40));
        Canvas.SetLeft(tb, cx - tb.DesiredSize.Width / 2);
        Canvas.SetTop(tb, y);
        _canvas.Children.Add(tb);
    }

    private static string Fmt(double mm, string? symbol = null)
    {
        var n = mm >= 100 ? mm.ToString("0", CultureInfo.InvariantCulture)
            : mm.ToString("0.#", CultureInfo.InvariantCulture);
        return symbol is null ? $"{n}" : $"{symbol}={n}";
    }

    private static Brush Brush(string key, Color fallback)
    {
        if (Application.Current.Resources.TryGetValue(key, out var b) && b is Brush brush)
            return brush;
        return new SolidColorBrush(fallback);
    }

    private static Brush Soft(Brush baseBrush, byte alpha)
    {
        if (baseBrush is SolidColorBrush scb)
        {
            var c = scb.Color;
            return new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
        }
        return baseBrush;
    }

    private static Brush Fill(Brush baseBrush, byte alpha) => Soft(baseBrush, alpha);
}
