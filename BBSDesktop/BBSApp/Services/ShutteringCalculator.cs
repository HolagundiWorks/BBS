using System.Globalization;

namespace BBSApp.Services;

/// <summary>
/// Formwork / shuttering contact area (m²) from concrete member geometry (mm).
/// Standard Indian BOQ / site practice (contact area with form):
/// <list type="bullet">
/// <item>Column rect/square: 2(B+D)×H (four faces)</item>
/// <item>Column circular: π×Ø×H (cylindrical surface; width = Ø)</item>
/// <item>Pedestal: 2(pw+pd)×ph when present</item>
/// <item>Beam: L×(B+2D) soffit + two sides</item>
/// <item>Slab: Lx×Ly soffit + 2(Lx+Ly)×t vertical edges</item>
/// <item>Footing: 2(L+B)×D sides only (cast on PCC/earth; top open)</item>
/// <item>Retaining wall: stem 2LH + 2tH ends; base 2(L+Wb)×tb</item>
/// <item>Stairs: waist soffit + risers + side edges + landing soffit/edges × flights</item>
/// </list>
/// Wastage factor applied at BOQ time via <see cref="CivilYields.ShutteringWastage"/>.
/// </summary>
public static class ShutteringCalculator
{
    public sealed class FormworkPart
    {
        public string Mark { get; init; } = "";
        public string Level { get; init; } = "";
        public string MemberType { get; init; } = "";
        public double AreaM2 { get; init; }
        public string Formula { get; init; } = "";
        public string SourceMark { get; init; } = "";
        public Dictionary<string, string> DimSnapshot { get; init; } = new();
    }

    /// <summary>Net contact area before wastage (m²) from a shuttering sheet row.</summary>
    public static double AreaM2FromRow(Dictionary<string, string> r)
    {
        // Prefer stored computed area for auto-synced rows.
        if (S(r, "source", "").Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            double stored = F(r, "area_m2");
            if (stored > 0) return stored;
        }

        string mt = S(r, "member_type", "Manual");
        double L = FirstMm(r, "length", "span", "span_x", "length_l", "wall_length");
        double B = FirstMm(r, "breadth", "width", "span_y", "width_b");
        double D = FirstMm(r, "depth", "thickness", "stem_t", "waist_t");
        double H = FirstMm(r, "height", "stem_h");

        double areaMm2 = mt.ToLowerInvariant() switch
        {
            "column" => ColumnMm2(r, B, D, H),
            "beam" => BeamMm2(L, B, D, deductSoffit: false),
            "slab" => SlabMm2(L, B > 0 ? B : Mm(r, "span_y"), FirstMm(r, "depth", "thickness")),
            "footing" => FootingMm2(r, L, B, D),
            "wall" => WallMm2(r),
            "stair" or "stairs" => StairMm2(r),
            _ => ManualMm2(r, L, B, D, H)
        };
        return Math.Max(0, areaMm2) / 1e6;
    }

    public static IEnumerable<FormworkPart> FromRcc(ProjectStore store, IReadOnlySet<string>? levels = null)
    {
        var filter = levels ?? store.LevelIds().ToHashSet();
        if (filter.Count == 0) yield break;

        foreach (var r in MaterialsCalculator.FilterByLevels(store.Columns, filter))
        {
            double B = Mm(r, "width"), D = Mm(r, "depth"), H = Mm(r, "height");
            double areaMm2 = ColumnMm2(r, B, D, H);
            if (areaMm2 <= 0) continue;
            string type = S(r, "column_type", "Rectangular");
            string formula = type.Equals("Circular", StringComparison.OrdinalIgnoreCase)
                ? "π×Ø×H"
                : "2(B+D)×H";
            yield return Part(r, "Column", areaMm2, formula, new()
            {
                ["length"] = "0",
                ["breadth"] = Inv(B),
                ["depth"] = Inv(D),
                ["height"] = Inv(H),
                ["column_type"] = type
            });
        }

        foreach (var r in MaterialsCalculator.FilterByLevels(store.Pedestals, filter))
        {
            double B = Mm(r, "width"), D = Mm(r, "depth"), H = Mm(r, "height");
            double areaMm2 = PedestalOnlyMm2(r, B, D, H);
            if (areaMm2 <= 0) continue;
            yield return Part(r, "Pedestal", areaMm2, "Pedestal sides", new()
            {
                ["length"] = "0",
                ["breadth"] = Inv(B),
                ["depth"] = Inv(D),
                ["height"] = Inv(H),
                ["column_type"] = S(r, "column_type", "Square")
            });
        }

        foreach (var r in MaterialsCalculator.FilterByLevels(store.Beams, filter))
        {
            double L = Mm(r, "span"), B = Mm(r, "width"), D = Mm(r, "depth");
            bool deduct = store.Yields.BeamSlabInterfaceDeduct;
            double areaMm2 = BeamMm2(L, B, D, deductSoffit: deduct);
            if (areaMm2 <= 0) continue;
            string formula = deduct ? "2×D×L sides (soffit deducted vs slab)" : "L×(B+2D) soffit+sides";
            yield return Part(r, "Beam", areaMm2, formula, new()
            {
                ["length"] = Inv(L),
                ["breadth"] = Inv(B),
                ["depth"] = Inv(D),
                ["height"] = "0"
            });
        }

        foreach (var r in MaterialsCalculator.FilterByLevels(store.Slabs, filter))
        {
            double Lx = Mm(r, "span_x"), Ly = Mm(r, "span_y"), t = Mm(r, "thickness");
            double areaMm2 = SlabMm2(Lx, Ly, t);
            if (areaMm2 <= 0) continue;
            yield return Part(r, "Slab", areaMm2, "Lx×Ly soffit + 2(Lx+Ly)×t edges", new()
            {
                ["length"] = Inv(Lx),
                ["breadth"] = Inv(Ly),
                ["depth"] = Inv(t),
                ["height"] = "0"
            });
        }

        foreach (var r in MaterialsCalculator.FilterByLevels(store.Footings, filter))
        {
            double L = Mm(r, "length_l"), B = Mm(r, "width_b"), D = Mm(r, "depth");
            double areaMm2 = FootingMm2(r, L, B, D);
            if (areaMm2 <= 0) continue;
            yield return Part(r, "Footing", areaMm2, "2(L+B)×D sides (no top/bottom)", new()
            {
                ["length"] = Inv(L),
                ["breadth"] = Inv(B),
                ["depth"] = Inv(D),
                ["height"] = "0",
                ["include_top"] = "No"
            });
        }

        foreach (var r in MaterialsCalculator.FilterByLevels(store.Walls, filter))
        {
            double areaMm2 = WallMm2(r);
            if (areaMm2 <= 0) continue;
            yield return Part(r, "Wall", areaMm2, "stem 2LH+2tH · base 2(L+Wb)×tb", new()
            {
                ["length"] = Inv(Mm(r, "wall_length")),
                ["breadth"] = Inv(BaseWidthMm(r)),
                ["depth"] = Inv(Mm(r, "base_t")),
                ["height"] = Inv(Mm(r, "stem_h"))
            });
        }

        foreach (var r in MaterialsCalculator.FilterByLevels(store.Stairs, filter))
        {
            double areaMm2 = StairMm2(r);
            if (areaMm2 <= 0) continue;
            yield return Part(r, "Stairs", areaMm2, "waist soffit+risers+edges + landing × flights", new()
            {
                ["length"] = Inv(Mm(r, "flight_width")),
                ["breadth"] = Inv(Mm(r, "going")),
                ["depth"] = Inv(Mm(r, "waist_t")),
                ["height"] = Inv(Mm(r, "riser"))
            });
        }
    }

    public static IEnumerable<CivilLine> AutoFromRcc(ProjectStore store, IReadOnlySet<string>? levels)
    {
        double waste = store.Yields.ShutteringWastage;
        var excluded = store.Shuttering
            .Where(r => S(r, "source", "").Equals("auto", StringComparison.OrdinalIgnoreCase)
                        && S(r, "include", "Yes").Equals("No", StringComparison.OrdinalIgnoreCase))
            .Select(r => S(r, "rcc_mark", S(r, "mark", "")))
            .Where(m => m.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var p in FromRcc(store, levels))
        {
            if (excluded.Contains(p.SourceMark) || excluded.Contains($"SH-{p.SourceMark}"))
                continue;
            double area = p.AreaM2 * waste;
            double Lm = Mm(p.DimSnapshot, "length") / 1000.0;
            double Bm = Mm(p.DimSnapshot, "breadth") / 1000.0;
            if (Bm <= 0) Bm = Mm(p.DimSnapshot, "depth") / 1000.0;
            double Hm = Mm(p.DimSnapshot, "height") / 1000.0;
            yield return new CivilLine
            {
                Element = "Shuttering",
                Mark = p.Mark,
                Level = p.Level,
                Description = $"Shuttering · {p.MemberType} (from RCC {p.SourceMark})",
                Unit = "m²",
                Qty = Math.Round(area, 3),
                AreaM2 = Math.Round(area, 3),
                LengthM = Math.Round(Lm, 3),
                BreadthM = Math.Round(Bm, 3),
                HeightM = Math.Round(Hm, 3),
                Notes = $"{p.Formula} · ×{waste:0.###} wastage · audit: {p.SourceMark}"
            };
        }
    }

    /// <summary>Rebuild shuttering sheet from RCC — preserves per-member Include Yes/No.</summary>
    public static void SyncStore(ProjectStore store)
    {
        var prevInclude = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in store.Shuttering)
        {
            string key = S(r, "rcc_mark", "");
            if (key.Length == 0) key = S(r, "mark", "");
            if (key.Length == 0) continue;
            prevInclude[key] = S(r, "include", "Yes");
            // also key without SH- prefix
            if (key.StartsWith("SH-", StringComparison.OrdinalIgnoreCase))
                prevInclude[key[3..]] = S(r, "include", "Yes");
        }

        store.Shuttering.Clear();
        int i = 1;
        foreach (var p in FromRcc(store, null))
        {
            string include = "Yes";
            if (prevInclude.TryGetValue(p.SourceMark, out var inc)
                || prevInclude.TryGetValue($"SH-{p.SourceMark}", out inc))
                include = inc;

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["mark"] = string.IsNullOrWhiteSpace(p.Mark) ? $"SH{i}" : $"SH-{p.Mark}",
                ["level"] = p.Level,
                ["member_type"] = p.MemberType,
                ["area_m2"] = Inv(p.AreaM2),
                ["include_top"] = "No",
                ["source"] = "auto",
                ["include"] = include,
                ["notes"] = $"{p.Formula} · from {p.SourceMark}",
                ["rcc_mark"] = p.SourceMark
            };
            foreach (var kv in p.DimSnapshot)
                row[kv.Key] = kv.Value;
            store.Shuttering.Add(row);
            i++;
        }
    }

    // ——— Formulae (mm²) ———

    private static double ColumnMm2(Dictionary<string, string> r, double B, double D, double H)
    {
        if (S(r, "column_type", "Rectangular").Equals("Circular", StringComparison.OrdinalIgnoreCase))
        {
            double dia = B > 0 ? B : D;
            if (dia > 0 && H > 0)
                return Math.PI * dia * H;
            return 0;
        }
        if (B > 0 && D > 0 && H > 0)
            return 2 * (B + D) * H;
        if (B > 0 && H > 0 && D <= 0)
            return 4 * B * H;
        return 0;
    }

    private static double PedestalOnlyMm2(Dictionary<string, string> r, double B, double D, double H)
    {
        if (S(r, "column_type", "Square").Equals("Circular", StringComparison.OrdinalIgnoreCase))
        {
            double dia = B > 0 ? B : D;
            return dia > 0 && H > 0 ? Math.PI * dia * H : 0;
        }
        if (B > 0 && D > 0 && H > 0) return 2 * (B + D) * H;
        if (B > 0 && H > 0) return 4 * B * H;
        return 0;
    }

    private static double BeamMm2(double L, double B, double D, bool deductSoffit)
    {
        if (L <= 0 || D <= 0) return 0;
        if (deductSoffit)
            return 2 * D * L; // sides only — slab formwork covers soffit
        if (B <= 0) return 0;
        return L * (B + 2 * D); // soffit + two sides
    }

    private static double SlabMm2(double Lx, double Ly, double t)
    {
        if (Lx <= 0 || Ly <= 0) return 0;
        double soffit = Lx * Ly;
        double edges = t > 0 ? 2 * (Lx + Ly) * t : 0;
        return soffit + edges;
    }

    private static double FootingMm2(Dictionary<string, string> r, double L, double B, double D)
    {
        if (L <= 0) L = Mm(r, "length_l");
        if (B <= 0) B = Mm(r, "width_b");
        if (D <= 0) D = Mm(r, "depth");
        if (L <= 0 || B <= 0 || D <= 0) return 0;
        double sides = 2 * (L + B) * D;
        bool top = S(r, "include_top", "No").Equals("Yes", StringComparison.OrdinalIgnoreCase);
        return sides + (top ? L * B : 0);
    }

    private static double BaseWidthMm(Dictionary<string, string> r)
    {
        double heel = Mm(r, "heel"), toe = Mm(r, "toe"), t = Mm(r, "stem_t");
        if (!S(r, "include_toe", "Yes").Equals("Yes", StringComparison.OrdinalIgnoreCase))
            toe = 0;
        return heel + toe + t;
    }

    private static double WallMm2(Dictionary<string, string> r)
    {
        double L = Mm(r, "wall_length");
        double H = Mm(r, "stem_h");
        double t = Mm(r, "stem_t");
        double stem = 0;
        if (L > 0 && H > 0)
        {
            stem = 2 * L * H; // both faces
            if (t > 0) stem += 2 * t * H; // ends
        }

        double Wb = BaseWidthMm(r);
        double tb = Mm(r, "base_t");
        double bas = (L > 0 && Wb > 0 && tb > 0) ? 2 * (L + Wb) * tb : 0;
        return stem + bas;
    }

    private static double StairMm2(Dictionary<string, string> r)
    {
        int nR = (int)Math.Max(0, F(r, "n_risers"));
        double going = Mm(r, "going");
        double riser = Mm(r, "riser");
        double waist = Mm(r, "waist_t");
        double width = Mm(r, "flight_width");
        int flights = (int)Math.Max(1, F(r, "n_flights"));
        if (flights < 1) flights = 1;

        // Run uses (n−1) goings — matches section diagram convention.
        int nGoings = Math.Max(nR - 1, 0);
        double incline = nGoings > 0 && going > 0 && riser > 0
            ? nGoings * Math.Sqrt(going * going + riser * riser)
            : 0;

        double waistSoffit = incline * width;
        double risers = nR > 0 && riser > 0 && width > 0 ? nR * riser * width : 0;
        double sideEdges = incline > 0 && waist > 0 ? 2 * incline * waist : 0;

        double landL = Mm(r, "landing_len");
        double landW = Mm(r, "landing_width");
        if (landW <= 0) landW = width;
        double landT = Mm(r, "landing_t");
        if (landT <= 0) landT = waist;
        double landing = 0;
        if (landL > 0 && landW > 0)
        {
            landing = landL * landW;
            if (landT > 0) landing += 2 * (landL + landW) * landT;
            // Dog-legged typically has two landings over two flights — one landing per flight is a fair average.
        }

        return (waistSoffit + risers + sideEdges + landing) * flights;
    }

    private static double ManualMm2(Dictionary<string, string> r, double L, double B, double D, double H)
    {
        double am = F(r, "area_m2");
        if (am > 0) return am * 1e6;
        if (L > 0 && H > 0 && B <= 0 && D <= 0) return L * H;
        if (L > 0 && B > 0 && H <= 0) return L * B;
        if (L > 0 && B > 0 && H > 0) return 2 * (L + B) * H;
        return 0;
    }

    private static FormworkPart Part(
        Dictionary<string, string> r,
        string member,
        double areaMm2,
        string formula,
        Dictionary<string, string> dims)
    {
        string mark = S(r, "mark", member);
        return new FormworkPart
        {
            Mark = mark,
            Level = MaterialsCalculator.RowLevel(r),
            MemberType = member,
            AreaM2 = Math.Round(areaMm2 / 1e6, 4),
            Formula = formula,
            SourceMark = mark,
            DimSnapshot = dims
        };
    }

    private static double FirstMm(Dictionary<string, string> r, params string[] keys)
    {
        foreach (var k in keys)
        {
            double v = Mm(r, k);
            if (v > 0) return v;
        }
        return 0;
    }

    private static double Mm(Dictionary<string, string> r, string k) => F(r, k);
    private static double F(Dictionary<string, string> r, string k) =>
        r.TryGetValue(k, out var v) && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
    private static string S(Dictionary<string, string> r, string k, string def = "") =>
        r.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v) ? v : def;
    private static string Inv(double d) => d.ToString("0.####", CultureInfo.InvariantCulture);
}
