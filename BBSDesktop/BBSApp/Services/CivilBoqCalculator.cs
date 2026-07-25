using System.Globalization;

namespace BBSApp.Services;

/// <summary>One civil quantity / material take-off line (no rates).</summary>
public sealed class CivilLine
{
    public string Element { get; set; } = "";
    public string Mark { get; set; } = "";
    public string Level { get; set; } = "";
    public string Description { get; set; } = "";
    public string Unit { get; set; } = "";
    public double Qty { get; set; }
    public double VolumeM3 { get; set; }
    public double AreaM2 { get; set; }
    public double Bricks { get; set; }
    public double AccBlocks { get; set; }
    public double CementBlocks { get; set; }
    public double CementBags { get; set; }
    public double SandM3 { get; set; }
    public double AggregateM3 { get; set; }
    public string Notes { get; set; } = "";
}

/// <summary>
/// Civil BOQ take-off — masonry, plaster, PCC, earthwork, SSM, shuttering, flooring, paint.
/// Yields come from ProjectStore.Yields (Settings).
/// </summary>
public static class CivilBoqCalculator
{
    private static CivilYields Y => ProjectStore.Current.Yields;

    public static GenResult Generate(string kind, IList<Dictionary<string, string>> rows)
    {
        var detail = new List<List<string>>();
        var summaryMap = new Dictionary<string, (string unit, double qty)>(StringComparer.OrdinalIgnoreCase);
        var matMap = new Dictionary<string, (string unit, double qty)>(StringComparer.OrdinalIgnoreCase);

        void AddDetail(CivilLine c)
        {
            detail.Add(new List<string>
            {
                c.Mark, c.Level, c.Description, c.Unit, Fmt(c.Qty),
                Fmt(c.Bricks), Fmt(c.AccBlocks), Fmt(c.CementBlocks),
                Fmt(c.CementBags), Fmt(c.SandM3), Fmt(c.AggregateM3), c.Notes
            });
            Acc(summaryMap, c.Description, c.Unit, c.Qty);
            if (c.Bricks > 0) Acc(matMap, "Bricks (modular)", "nos", c.Bricks);
            if (c.AccBlocks > 0) Acc(matMap, "ACC blocks", "nos", c.AccBlocks);
            if (c.CementBlocks > 0) Acc(matMap, "Cement blocks", "nos", c.CementBlocks);
            if (c.CementBags > 0) Acc(matMap, "Cement", "bags (50kg)", c.CementBags);
            if (c.SandM3 > 0) Acc(matMap, "Sand", "m³", c.SandM3);
            if (c.AggregateM3 > 0) Acc(matMap, "Aggregate", "m³", c.AggregateM3);
        }

        foreach (var r in rows)
        {
            // Manual shuttering sheet rows are ignored — formwork comes from RCC only.
            if (kind != "shuttering")
            {
                foreach (var line in LinesForRow(kind, r))
                    AddDetail(line);
            }
        }
        if (kind == "shuttering")
        {
            ShutteringCalculator.SyncStore(ProjectStore.Current);
            foreach (var line in ShutteringCalculator.AutoFromRcc(ProjectStore.Current, null))
                AddDetail(line);
        }

        var summaryRows = new List<List<string>>();
        foreach (var kv in summaryMap.OrderBy(k => k.Key))
            summaryRows.Add(new List<string> { kv.Key, kv.Value.unit, Fmt(kv.Value.qty) });
        foreach (var kv in matMap.OrderBy(k => k.Key))
            summaryRows.Add(new List<string> { "MAT · " + kv.Key, kv.Value.unit, Fmt(kv.Value.qty) });

        return new GenResult
        {
            Ok = true,
            Bbs = new GenTable
            {
                Headers = new List<string>
                {
                    "Mark", "Level", "Item", "Unit", "Qty",
                    "Bricks", "ACC", "CemBlk", "Cement bags", "Sand m³", "Agg m³", "Notes"
                },
                Rows = detail
            },
            Summary = new GenTable
            {
                Headers = new List<string> { "Item", "Unit", "Qty" },
                Rows = summaryRows
            },
            Checks = new GenTable
            {
                Headers = new List<string> { "Note" },
                Rows = new List<List<string>>
                {
                    new() { "Quantity take-off only — no rates. Yields are editable in Settings." },
                    new() { "Deduction rules: None · Openings full · IS1200 masonry · IS1200 plaster/paint." },
                    new() { "Shuttering / formwork is calculated from RCC concrete members (not entered manually)." }
                }
            }
        };
    }

    public static List<CivilLine> BuildAll(ProjectStore store, IReadOnlySet<string>? levels = null)
    {
        var lines = new List<CivilLine>();
        if (levels is { Count: 0 }) return lines;
        var filter = levels ?? store.LevelIds().ToHashSet();

        foreach (var r in MaterialsCalculator.FilterByLevels(store.MasonryWalls, filter))
            lines.AddRange(LinesForRow("masonry", r));
        foreach (var r in MaterialsCalculator.FilterByLevels(store.Plaster, filter))
            lines.AddRange(LinesForRow("plaster", r));
        foreach (var r in MaterialsCalculator.FilterByLevels(store.PccBeds, filter))
            lines.AddRange(LinesForRow("pcc", r));
        foreach (var r in MaterialsCalculator.FilterByLevels(store.Earthwork, filter))
            lines.AddRange(LinesForRow("earthwork", r));
        foreach (var r in MaterialsCalculator.FilterByLevels(store.SizeStone, filter))
            lines.AddRange(LinesForRow("ssm", r));
        // Shuttering is computed from RCC only (preserve Include flags via Sync).
        ShutteringCalculator.SyncStore(store);
        lines.AddRange(ShutteringCalculator.AutoFromRcc(store, filter));
        foreach (var r in MaterialsCalculator.FilterByLevels(store.Flooring, filter))
            lines.AddRange(LinesForRow("flooring", r));
        foreach (var r in MaterialsCalculator.FilterByLevels(store.Painting, filter))
            lines.AddRange(LinesForRow("painting", r));
        foreach (var r in MaterialsCalculator.FilterByLevels(store.Waterproofing, filter))
            lines.AddRange(LinesForRow("waterproofing", r));
        foreach (var r in MaterialsCalculator.FilterByLevels(store.Dpc, filter))
            lines.AddRange(LinesForRow("dpc", r));
        foreach (var r in MaterialsCalculator.FilterByLevels(store.Coping, filter))
            lines.AddRange(LinesForRow("coping", r));
        foreach (var r in MaterialsCalculator.FilterByLevels(store.Screed, filter))
            lines.AddRange(LinesForRow("screed", r));
        foreach (var r in MaterialsCalculator.FilterByLevels(store.Vdf, filter))
            lines.AddRange(LinesForRow("vdf", r));
        foreach (var r in MaterialsCalculator.FilterByLevels(store.Skirting, filter))
            lines.AddRange(LinesForRow("skirting", r));
        foreach (var r in MaterialsCalculator.FilterByLevels(store.Parapet, filter))
            lines.AddRange(LinesForRow("parapet", r));
        foreach (var r in MaterialsCalculator.FilterByLevels(store.PlinthProtection, filter))
            lines.AddRange(LinesForRow("plinth_protection", r));

        return lines;
    }

    public static List<PoLine> MaterialPurchaseOrder(IEnumerable<CivilLine> lines)
    {
        double bricks = 0, acc = 0, cblk = 0, bags = 0, sand = 0, agg = 0;
        double wall230 = 0, wall110 = 0, plaster = 0, pcc = 0, earth = 0, ssm = 0;
        double shutter = 0, floor = 0, paint = 0;
        foreach (var c in lines)
        {
            bricks += c.Bricks; acc += c.AccBlocks; cblk += c.CementBlocks;
            bags += c.CementBags; sand += c.SandM3; agg += c.AggregateM3;
            if (c.Unit == "m³" && c.Element == "Masonry" && c.Description.Contains("230")) wall230 += c.Qty;
            if (c.Unit == "m²" && c.Element == "Masonry" && c.Description.Contains("110")) wall110 += c.Qty;
            if (c.Element == "Plaster") plaster += c.Qty;
            if (c.Element == "PCC") pcc += c.VolumeM3;
            if (c.Element == "Earthwork") earth += c.VolumeM3;
            if (c.Element == "SSM") ssm += c.VolumeM3;
            if (c.Element == "Shuttering") shutter += c.AreaM2 > 0 ? c.AreaM2 : c.Qty;
            if (c.Element == "Flooring") floor += c.AreaM2 > 0 ? c.AreaM2 : c.Qty;
            if (c.Element == "Painting") paint += c.AreaM2 > 0 ? c.AreaM2 : c.Qty;
        }

        var po = new List<PoLine>();
        void Add(string cat, string item, string unit, double qty, string notes = "")
        {
            if (qty <= 0) return;
            po.Add(new PoLine { Category = cat, Item = item, Unit = unit, Qty = MaterialsCalculator.Round3(qty), Notes = notes });
        }

        Add("Masonry", "Brick wall 230 mm", "m³", wall230);
        Add("Masonry", "Brick / block wall 110 mm", "m²", wall110);
        Add("Plaster", "Plastering", "m²", plaster);
        Add("Concrete", "PCC bed", "m³", pcc);
        Add("Earthwork", "Earthwork excavation / filling", "m³", earth);
        Add("Masonry", "Size stone masonry", "m³", ssm);
        Add("Shuttering", "Formwork / shuttering", "m²", shutter, "incl. wastage from Settings");
        Add("Finishes", "Flooring", "m²", floor);
        Add("Finishes", "Painting", "m²", paint);
        Add("Units", "Bricks (modular)", "nos", bricks, "incl. wastage");
        Add("Units", "ACC blocks", "nos", acc, "incl. wastage");
        Add("Units", "Cement blocks", "nos", cblk, "incl. wastage");
        Add("Cement", "OPC / PPC (civil works)", "bags (50kg)", bags);
        Add("Sand", "Fine aggregate (civil)", "m³", sand);
        Add("Aggregate", "Coarse aggregate (civil)", "m³", agg);
        return po;
    }

    private static IEnumerable<CivilLine> LinesForRow(string kind, Dictionary<string, string> r) => kind switch
    {
        "masonry" => MasonryLines(r),
        "plaster" => PlasterLines(r),
        "pcc" => PccLines(r),
        "earthwork" => EarthLines(r),
        "ssm" => SsmLines(r),
        "shuttering" => ShutteringLines(r),
        "flooring" => FlooringLines(r),
        "painting" => PaintingLines(r),
        "waterproofing" => WaterproofingLines(r),
        "dpc" => DpcLines(r),
        "coping" => CopingLines(r),
        "screed" => ScreedLines(r),
        "vdf" => VdfLines(r),
        "skirting" => SkirtingLines(r),
        "parapet" => ParapetLines(r),
        "plinth_protection" => PlinthProtectionLines(r),
        _ => Array.Empty<CivilLine>()
    };

    /// <summary>Gross area mm², deducted mm², net mm², note.</summary>
    public static (double grossMm2, double deductMm2, double netMm2, string note) DeductFaceArea(
        double L, double H, Dictionary<string, string> r, string mode, bool addJambs)
    {
        double gross = Math.Max(0, L * H);
        string rule = S(r, "deduct_rule", mode);
        if (string.IsNullOrWhiteSpace(rule)) rule = mode;

        if (rule.Equals("None", StringComparison.OrdinalIgnoreCase))
            return (gross, 0, gross, "gross (no deduct)");

        double openArea = OpeningAreaMm2(r, rule);
        double jambAdd = 0;
        bool plasterStyle = rule.Contains("plaster", StringComparison.OrdinalIgnoreCase)
                         || rule.Contains("paint", StringComparison.OrdinalIgnoreCase);
        if (addJambs && plasterStyle)
            jambAdd = JambAreaMm2(r);

        double deduct = openArea;
        double net = Math.Max(0, gross - deduct + jambAdd);
        string note = $"gross {gross / 1e6:0.###} − deduct {deduct / 1e6:0.###}"
                    + (jambAdd > 0 ? $" + jambs {jambAdd / 1e6:0.###}" : "")
                    + $" = net {net / 1e6:0.###} m²";
        return (gross, deduct, net, note);
    }

    private static double OpeningAreaMm2(Dictionary<string, string> r, string rule)
    {
        double a1 = OneOpeningMm2(r, "opening_nos", "opening_l", "opening_h", rule);
        double a2 = OneOpeningMm2(r, "opening2_nos", "opening2_l", "opening2_h", rule);
        return a1 + a2;
    }

    private static double OneOpeningMm2(Dictionary<string, string> r, string nKey, string lKey, string hKey, string rule)
    {
        double openL = Mm(r, lKey), openH = Mm(r, hKey);
        int openN = (int)F(r, nKey);
        if (openN <= 0 && (openL > 0 || openH > 0)) openN = 1;
        if (openN <= 0 || openL <= 0 || openH <= 0) return 0;
        double eachMm2 = openL * openH;
        // IS 1200 masonry practice: ignore openings below threshold (default 0.1 m²)
        if (rule.Contains("masonry", StringComparison.OrdinalIgnoreCase))
        {
            double eachM2 = eachMm2 / 1e6;
            if (eachM2 < Y.IgnoreOpeningBelowM2) return 0;
        }
        return openN * eachMm2;
    }

    private static double JambAreaMm2(Dictionary<string, string> r)
    {
        // Simple: perimeter of openings × reveal depth (default 100 mm)
        double reveal = Mm(r, "jamb_depth");
        if (reveal <= 0) reveal = 100;
        double sum = 0;
        sum += JambFor(r, "opening_nos", "opening_l", "opening_h", reveal);
        sum += JambFor(r, "opening2_nos", "opening2_l", "opening2_h", reveal);
        return sum;
    }

    private static double JambFor(Dictionary<string, string> r, string nKey, string lKey, string hKey, double reveal)
    {
        double openL = Mm(r, lKey), openH = Mm(r, hKey);
        int openN = (int)F(r, nKey);
        if (openN <= 0 && (openL > 0 || openH > 0)) openN = 1;
        if (openN <= 0 || openL <= 0 || openH <= 0) return 0;
        // 2 sides + top (sill usually not plastered as jamb) — 2H + L
        return openN * (2 * openH + openL) * reveal;
    }

    private static IEnumerable<CivilLine> MasonryLines(Dictionary<string, string> r)
    {
        double L = Mm(r, "length"), H = Mm(r, "height");
        string rule = S(r, "deduct_rule", "Openings full");
        var (_, _, netMm2, note) = DeductFaceArea(L, H, r, rule, addJambs: false);
        double thick = F(r, "thickness");
        if (thick <= 0) thick = 230;
        string unitType = S(r, "unit_type", "Brick");
        string blockSize = S(r, "block_size", "600x200x150");
        string mortar = S(r, "mortar_mix", "1:6");
        string mark = S(r, "mark", "W1");
        string level = MaterialsCalculator.RowLevel(r);

        var line = new CivilLine
        {
            Element = "Masonry",
            Mark = mark,
            Level = level,
            Notes = $"{unitType}; {note}"
        };

        if (Math.Abs(thick - 110) < 1)
        {
            double areaM2 = netMm2 / 1e6;
            line.Unit = "m²";
            line.Qty = Round3(areaM2);
            line.AreaM2 = line.Qty;
            line.Description = $"Masonry wall 110 mm ({unitType})";
            YieldUnits(line, unitType, blockSize, volumeM3: 0, areaM2: areaM2, is110: true);
            YieldMortar(line, mortar, volumeM3: areaM2 * 0.110 * Y.MortarFraction);
        }
        else
        {
            double t = thick > 0 ? thick : 230;
            double vol = MaterialsCalculator.Mm3ToM3(netMm2 * t);
            line.Unit = "m³";
            line.Qty = Round3(vol);
            line.VolumeM3 = line.Qty;
            line.Description = $"Masonry wall {t:0} mm ({unitType})";
            YieldUnits(line, unitType, blockSize, volumeM3: vol, areaM2: 0, is110: false);
            YieldMortar(line, mortar, volumeM3: vol * Y.MortarFraction);
        }

        if (line.Qty > 0) yield return line;
    }

    private static IEnumerable<CivilLine> PlasterLines(Dictionary<string, string> r)
    {
        double L = Mm(r, "length"), H = Mm(r, "height");
        string rule = S(r, "deduct_rule", "IS1200 plaster/paint");
        bool addJambs = S(r, "add_jambs", "No").Equals("Yes", StringComparison.OrdinalIgnoreCase);
        var (_, _, netMm2, note) = DeductFaceArea(L, H, r, rule, addJambs);
        double areaM2 = netMm2 / 1e6;
        double thickMm = F(r, "thickness");
        if (thickMm <= 0) thickMm = 12;
        string mix = S(r, "mortar_mix", "1:4");
        int faces = (int)F(r, "faces");
        if (faces < 1) faces = 1;
        areaM2 *= faces;

        double wet = areaM2 * (thickMm / 1000.0);
        var line = new CivilLine
        {
            Element = "Plaster",
            Mark = S(r, "mark", "PL1"),
            Level = MaterialsCalculator.RowLevel(r),
            Description = $"Plaster {thickMm:0} mm · CM {mix}" + (faces > 1 ? $" · {faces} faces" : ""),
            Unit = "m²",
            Qty = Round3(areaM2),
            AreaM2 = Round3(areaM2),
            Notes = note
        };
        YieldMortar(line, mix, volumeM3: wet);
        if (line.Qty > 0) yield return line;
    }

    private static IEnumerable<CivilLine> PccLines(Dictionary<string, string> r)
    {
        double L = Mm(r, "length"), B = Mm(r, "breadth"), Th = Mm(r, "thickness");
        double vol = MaterialsCalculator.Mm3ToM3(L * B * Th);
        string mix = S(r, "mix", "1:4:8");
        var line = new CivilLine
        {
            Element = "PCC",
            Mark = S(r, "mark", "PCC1"),
            Level = MaterialsCalculator.RowLevel(r),
            Description = $"PCC bed · {mix}",
            Unit = "m³",
            Qty = Round3(vol),
            VolumeM3 = Round3(vol),
            Notes = "lean concrete bed"
        };
        YieldPcc(line, mix, vol);
        if (line.Qty > 0) yield return line;
    }

    private static IEnumerable<CivilLine> EarthLines(Dictionary<string, string> r)
    {
        double L = Mm(r, "length"), B = Mm(r, "breadth"), H = Mm(r, "depth");
        double vol = MaterialsCalculator.Mm3ToM3(L * B * H);
        string work = S(r, "work_type", "Excavation");
        var line = new CivilLine
        {
            Element = "Earthwork",
            Mark = S(r, "mark", "EW1"),
            Level = MaterialsCalculator.RowLevel(r),
            Description = $"Earthwork — {work}",
            Unit = "m³",
            Qty = Round3(vol),
            VolumeM3 = Round3(vol),
            Notes = "no material yield"
        };
        if (line.Qty > 0) yield return line;
    }

    private static IEnumerable<CivilLine> SsmLines(Dictionary<string, string> r)
    {
        double L = Mm(r, "length"), B = Mm(r, "breadth"), H = Mm(r, "height");
        double vol = MaterialsCalculator.Mm3ToM3(L * B * H);
        string mix = S(r, "mortar_mix", "1:6");
        var line = new CivilLine
        {
            Element = "SSM",
            Mark = S(r, "mark", "SSM1"),
            Level = MaterialsCalculator.RowLevel(r),
            Description = $"Size stone masonry · CM {mix}",
            Unit = "m³",
            Qty = Round3(vol),
            VolumeM3 = Round3(vol),
            Notes = "stone + mortar"
        };
        YieldMortar(line, mix, volumeM3: vol * Y.SsmMortarFraction);
        if (line.Qty > 0) yield return line;
    }

    private static IEnumerable<CivilLine> ShutteringLines(Dictionary<string, string> r)
    {
        // Skip auto-linked rows that are only markers — calculator uses geometry fields
        if (S(r, "source", "").Equals("auto", StringComparison.OrdinalIgnoreCase)
            && S(r, "include", "Yes").Equals("No", StringComparison.OrdinalIgnoreCase))
            yield break;

        double area = ShutteringCalculator.AreaM2FromRow(r);
        if (area <= 0) yield break;
        area *= Y.ShutteringWastage;
        yield return new CivilLine
        {
            Element = "Shuttering",
            Mark = S(r, "mark", "SH1"),
            Level = MaterialsCalculator.RowLevel(r),
            Description = $"Shuttering · {S(r, "member_type", "Manual")}",
            Unit = "m²",
            Qty = Round3(area),
            AreaM2 = Round3(area),
            Notes = S(r, "notes", "formwork")
        };
    }

    private static IEnumerable<CivilLine> FlooringLines(Dictionary<string, string> r)
    {
        double L = Mm(r, "length"), B = Mm(r, "breadth");
        string rule = S(r, "deduct_rule", "Openings full");
        // Treat as plan area L×B with opening deduct (use height field as breadth for DeductFaceArea)
        var (_, _, netMm2, note) = DeductFaceArea(L, B, r, rule, addJambs: false);
        double areaM2 = netMm2 / 1e6;
        yield return new CivilLine
        {
            Element = "Flooring",
            Mark = S(r, "mark", "FL1"),
            Level = MaterialsCalculator.RowLevel(r),
            Description = $"Flooring · {S(r, "finish_type", "Tile")}",
            Unit = "m²",
            Qty = Round3(areaM2),
            AreaM2 = Round3(areaM2),
            Notes = note
        };
    }

    private static IEnumerable<CivilLine> PaintingLines(Dictionary<string, string> r)
    {
        double L = Mm(r, "length"), H = Mm(r, "height");
        string rule = S(r, "deduct_rule", "IS1200 plaster/paint");
        bool addJambs = S(r, "add_jambs", "No").Equals("Yes", StringComparison.OrdinalIgnoreCase);
        var (_, _, netMm2, note) = DeductFaceArea(L, H, r, rule, addJambs);
        double areaM2 = netMm2 / 1e6;
        int faces = (int)F(r, "faces");
        if (faces < 1) faces = 1;
        areaM2 *= faces;
        int coats = (int)F(r, "coats");
        if (coats < 1) coats = 1;
        // qty is surface area (not × coats) — coats noted
        yield return new CivilLine
        {
            Element = "Painting",
            Mark = S(r, "mark", "PT1"),
            Level = MaterialsCalculator.RowLevel(r),
            Description = $"Painting · {S(r, "paint_type", "Emulsion")} · {coats} coat(s)",
            Unit = "m²",
            Qty = Round3(areaM2),
            AreaM2 = Round3(areaM2),
            Notes = note
        };
    }

    private static IEnumerable<CivilLine> WaterproofingLines(Dictionary<string, string> r)
    {
        string mode = S(r, "work_mode", "Area");
        double L = Mm(r, "length"), B = Mm(r, "breadth"), H = Mm(r, "height");
        double areaM2;
        string desc;
        if (mode.Contains("Periphery", StringComparison.OrdinalIgnoreCase))
        {
            areaM2 = (L * H) / 1e6;
            desc = "Waterproofing · periphery band";
        }
        else
        {
            areaM2 = (L * B) / 1e6;
            desc = "Waterproofing · area";
        }
        string notes = S(r, "notes", "");
        yield return new CivilLine
        {
            Element = "Waterproofing",
            Mark = S(r, "mark", "WP1"),
            Level = MaterialsCalculator.RowLevel(r),
            Description = desc,
            Unit = "m²",
            Qty = Round3(areaM2),
            AreaM2 = Round3(areaM2),
            Notes = notes
        };
    }

    private static IEnumerable<CivilLine> DpcLines(Dictionary<string, string> r)
    {
        double L = Mm(r, "length"), W = Mm(r, "width"), T = Mm(r, "thickness");
        double areaM2 = (L * W) / 1e6;
        double vol = (L * W * T) / 1e9;
        yield return new CivilLine
        {
            Element = "DPC",
            Mark = S(r, "mark", "DPC1"),
            Level = MaterialsCalculator.RowLevel(r),
            Description = $"Damp-proof course · {S(r, "mortar_mix", "1:3")}",
            Unit = "m²",
            Qty = Round3(areaM2),
            AreaM2 = Round3(areaM2),
            VolumeM3 = Round3(vol),
            Notes = $"t={T:0} mm · vol {Round3(vol)} m³"
        };
    }

    private static IEnumerable<CivilLine> CopingLines(Dictionary<string, string> r)
    {
        double L = Mm(r, "length"), W = Mm(r, "width"), D = Mm(r, "depth");
        double lenM = L / 1000.0;
        double vol = (L * W * D) / 1e9;
        yield return new CivilLine
        {
            Element = "Coping",
            Mark = S(r, "mark", "CP1"),
            Level = MaterialsCalculator.RowLevel(r),
            Description = $"Coping · {S(r, "concrete_grade", "PCC")}",
            Unit = "m",
            Qty = Round3(lenM),
            VolumeM3 = Round3(vol),
            Notes = $"b×D={W:0}×{D:0} · {Round3(vol)} m³"
        };
    }

    private static IEnumerable<CivilLine> ScreedLines(Dictionary<string, string> r)
    {
        double L = Mm(r, "length"), B = Mm(r, "breadth"), T = Mm(r, "thickness");
        double areaM2 = (L * B) / 1e6;
        double vol = (L * B * T) / 1e9;
        var line = new CivilLine
        {
            Element = "Screed",
            Mark = S(r, "mark", "SC1"),
            Level = MaterialsCalculator.RowLevel(r),
            Description = $"Screed · {S(r, "mix", "1:4:8")} · t={T:0} mm",
            Unit = "m³",
            Qty = Round3(vol),
            AreaM2 = Round3(areaM2),
            VolumeM3 = Round3(vol),
            Notes = $"{Round3(areaM2)} m²"
        };
        ApplyPccMaterials(line, vol, S(r, "mix", "1:4:8"));
        yield return line;
    }

    private static IEnumerable<CivilLine> VdfLines(Dictionary<string, string> r)
    {
        double L = Mm(r, "length"), B = Mm(r, "breadth"), T = Mm(r, "thickness");
        double areaM2 = (L * B) / 1e6;
        double vol = (L * B * T) / 1e9;
        yield return new CivilLine
        {
            Element = "VDF",
            Mark = S(r, "mark", "VDF1"),
            Level = MaterialsCalculator.RowLevel(r),
            Description = $"VDF flooring · t={T:0} mm",
            Unit = "m²",
            Qty = Round3(areaM2),
            AreaM2 = Round3(areaM2),
            VolumeM3 = Round3(vol),
            Notes = S(r, "notes", "")
        };
    }

    private static IEnumerable<CivilLine> SkirtingLines(Dictionary<string, string> r)
    {
        double L = Mm(r, "length"), H = Mm(r, "height");
        double areaM2 = (L * H) / 1e6;
        yield return new CivilLine
        {
            Element = "Skirting",
            Mark = S(r, "mark", "SK1"),
            Level = MaterialsCalculator.RowLevel(r),
            Description = $"Skirting · {S(r, "finish_type", "Tile")}",
            Unit = "m²",
            Qty = Round3(areaM2),
            AreaM2 = Round3(areaM2),
            Notes = $"L={L / 1000.0:0.###} m · H={H:0} mm"
        };
    }

    private static IEnumerable<CivilLine> ParapetLines(Dictionary<string, string> r)
    {
        double L = Mm(r, "length"), H = Mm(r, "height"), T = Mm(r, "thickness");
        double vol = (L * H * T) / 1e9;
        double faceM2 = (L * H) / 1e6;
        var line = new CivilLine
        {
            Element = "Parapet",
            Mark = S(r, "mark", "PR1"),
            Level = MaterialsCalculator.RowLevel(r),
            Description = $"Parapet · {S(r, "unit_type", "Brick")} · {T:0} mm",
            Unit = "m³",
            Qty = Round3(vol),
            VolumeM3 = Round3(vol),
            AreaM2 = Round3(faceM2),
            Notes = $"{Round3(faceM2)} m² face"
        };
        YieldUnits(line, S(r, "unit_type", "Brick"), "600x200x150", vol, faceM2, T <= 120);
        yield return line;
    }

    private static IEnumerable<CivilLine> PlinthProtectionLines(Dictionary<string, string> r)
    {
        double L = Mm(r, "length"), B = Mm(r, "breadth"), T = Mm(r, "thickness");
        double areaM2 = (L * B) / 1e6;
        double vol = (L * B * T) / 1e9;
        yield return new CivilLine
        {
            Element = "Plinth protection",
            Mark = S(r, "mark", "PP1"),
            Level = MaterialsCalculator.RowLevel(r),
            Description = $"Plinth protection · {S(r, "finish_type", "PCC")}",
            Unit = "m²",
            Qty = Round3(areaM2),
            AreaM2 = Round3(areaM2),
            VolumeM3 = Round3(vol),
            Notes = $"t={T:0} mm"
        };
    }

    private static void ApplyPccMaterials(CivilLine line, double volM3, string mix)
    {
        // Rough dry volume factor; cement bags approximate for common mixes
        double dry = volM3 * 1.52 * Y.Wastage;
        line.CementBags = mix switch
        {
            "1:3:6" => Round3(dry / 0.16),
            "1:5:10" => Round3(dry / 0.28),
            _ => Round3(dry / 0.22) // 1:4:8-ish
        };
        line.SandM3 = Round3(dry * 0.45);
        line.AggregateM3 = Round3(dry * 0.90);
    }

    private static void YieldUnits(CivilLine line, string unitType, string blockSize,
        double volumeM3, double areaM2, bool is110)
    {
        double wastage = Y.Wastage;
        if (unitType.Equals("ACC Block", StringComparison.OrdinalIgnoreCase))
        {
            double each = BlockVolumeM3(blockSize);
            if (is110)
            {
                var (fl, fh, _) = ParseBlockMm(blockSize);
                double face = Math.Max(1e-6, (fl / 1000.0) * (fh / 1000.0));
                line.AccBlocks = Round3(areaM2 / face * wastage);
            }
            else if (each > 0)
                line.AccBlocks = Round3(volumeM3 / each * wastage);
        }
        else if (unitType.Equals("Cement Block", StringComparison.OrdinalIgnoreCase))
        {
            double each = BlockVolumeM3(blockSize);
            if (is110)
            {
                var (fl, fh, _) = ParseBlockMm(blockSize);
                double face = Math.Max(1e-6, (fl / 1000.0) * (fh / 1000.0));
                line.CementBlocks = Round3(areaM2 / face * wastage);
            }
            else if (each > 0)
                line.CementBlocks = Round3(volumeM3 / each * wastage);
        }
        else
        {
            if (is110)
                line.Bricks = Round3(areaM2 * Y.BricksPerM2Half * wastage);
            else
                line.Bricks = Round3(volumeM3 * Y.BricksPerM3 * wastage);
        }
    }

    private static void YieldMortar(CivilLine line, string mix, double volumeM3)
    {
        if (volumeM3 <= 0) return;
        var (c, s, a) = ParseMix(mix);
        double dry = volumeM3 * Y.MortarDryFactor;
        double parts = c + s + a;
        if (parts <= 0) return;
        double cementM3 = dry * (c / parts);
        double sandM3 = dry * (s / parts);
        double aggM3 = a > 0 ? dry * (a / parts) : 0;
        line.CementBags += Round3(cementM3 * MaterialsCalculator.CementDensityKgPerM3 / MaterialsCalculator.CementBagKg);
        line.SandM3 += Round3(sandM3);
        line.AggregateM3 += Round3(aggM3);
    }

    private static void YieldPcc(CivilLine line, string mix, double wetVol)
    {
        if (wetVol <= 0) return;
        var (c, s, a) = ParseMix(mix);
        if (a <= 0) { c = 1; s = 4; a = 8; }
        double dry = wetVol * MaterialsCalculator.DryFactor;
        double parts = c + s + a;
        double cementM3 = dry * (c / parts);
        line.CementBags = Round3(cementM3 * MaterialsCalculator.CementDensityKgPerM3 / MaterialsCalculator.CementBagKg);
        line.SandM3 = Round3(dry * (s / parts));
        line.AggregateM3 = Round3(dry * (a / parts));
    }

    private static (double c, double s, double a) ParseMix(string mix)
    {
        var parts = (mix ?? "").Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        double c = 1, s = 6, a = 0;
        if (parts.Length >= 1 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var p0)) c = p0;
        if (parts.Length >= 2 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var p1)) s = p1;
        if (parts.Length >= 3 && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var p2)) a = p2;
        return (c, s, a);
    }

    private static (double L, double H, double T) ParseBlockMm(string size)
    {
        var p = (size ?? "").ToLowerInvariant().Split('x', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        double L = 600, H = 200, T = 150;
        if (p.Length >= 1 && double.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a)) L = a;
        if (p.Length >= 2 && double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var b)) H = b;
        if (p.Length >= 3 && double.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var c)) T = c;
        return (L, H, T);
    }

    private static double BlockVolumeM3(string size)
    {
        var (L, H, T) = ParseBlockMm(size);
        return (L * H * T) / 1e9;
    }

    private static void Acc(Dictionary<string, (string unit, double qty)> map, string key, string unit, double qty)
    {
        if (qty <= 0) return;
        if (map.TryGetValue(key, out var cur))
            map[key] = (unit, cur.qty + qty);
        else
            map[key] = (unit, qty);
    }

    private static double Mm(Dictionary<string, string> r, string k) => F(r, k);
    private static double F(Dictionary<string, string> r, string k) =>
        r.TryGetValue(k, out var v) && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
    private static string S(Dictionary<string, string> r, string k, string def = "") =>
        r.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v) ? v : def;
    private static double Round3(double x) => Math.Round(x, 3);
    private static string Fmt(double x) => x.ToString("0.###", CultureInfo.InvariantCulture);
}
