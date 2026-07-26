using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Nodes;

namespace BBSApp.Services;

public enum TakeoffTool
{
    /// <summary>Draw using the active element's pick mode (Point / Line / Area).</summary>
    Draw,
    Pan,
    /// <summary>Pick two points on a known dim (wall thickness etc.) and enter real mm → sets whole-drawing scale.</summary>
    Scale,
    Opening,
    Select
}

public enum TakeoffPickMode
{
    Point,
    Line,
    Area
}

/// <summary>Element toolbar entry: category drives pick geometry and field mapping.</summary>
public sealed class ElementPickProfile
{
    public string Category { get; init; } = "";
    public string Label { get; init; } = "";
    public TakeoffPickMode PickMode { get; init; }
    public string Prefix { get; init; } = "TK";
    /// <summary>Primary length field for Line mode (length / span).</summary>
    public string PrimaryField { get; init; } = "length";
    /// <summary>Second plan dimension for Area mode (breadth / span_y).</summary>
    public string SecondaryField { get; init; } = "breadth";
    public string ModeHint { get; init; } = "";

    public static IReadOnlyList<ElementPickProfile> All { get; } = new[]
    {
        new ElementPickProfile
        {
            Category = "columns", Label = "Column", PickMode = TakeoffPickMode.Point,
            Prefix = "C", PrimaryField = "height", ModeHint = "Point place"
        },
        new ElementPickProfile
        {
            Category = "beams", Label = "Beam", PickMode = TakeoffPickMode.Line,
            Prefix = "B", PrimaryField = "span", ModeHint = "Line measure"
        },
        new ElementPickProfile
        {
            Category = "pedestals", Label = "Pedestal", PickMode = TakeoffPickMode.Point,
            Prefix = "P", PrimaryField = "height", ModeHint = "Point place"
        },
        new ElementPickProfile
        {
            Category = "lintels", Label = "Lintel", PickMode = TakeoffPickMode.Line,
            Prefix = "L", PrimaryField = "opening", ModeHint = "Line measure"
        },
        new ElementPickProfile
        {
            Category = "slabs", Label = "Slab", PickMode = TakeoffPickMode.Area,
            Prefix = "S", PrimaryField = "span_x", SecondaryField = "span_y", ModeHint = "Polyline area"
        },
        new ElementPickProfile
        {
            Category = "footings", Label = "Footing", PickMode = TakeoffPickMode.Area,
            Prefix = "F", PrimaryField = "length_l", SecondaryField = "width_b", ModeHint = "Polyline area"
        },
        new ElementPickProfile
        {
            Category = "masonry", Label = "Wall", PickMode = TakeoffPickMode.Line,
            Prefix = "MW", PrimaryField = "length", ModeHint = "Line measure"
        },
        new ElementPickProfile
        {
            Category = "plaster", Label = "Plaster", PickMode = TakeoffPickMode.Line,
            Prefix = "PL", PrimaryField = "length", ModeHint = "Line measure"
        },
        new ElementPickProfile
        {
            Category = "pcc", Label = "PCC", PickMode = TakeoffPickMode.Area,
            Prefix = "PCC", PrimaryField = "length", SecondaryField = "breadth", ModeHint = "Polyline area"
        },
        new ElementPickProfile
        {
            Category = "earthwork", Label = "Earth", PickMode = TakeoffPickMode.Area,
            Prefix = "EW", PrimaryField = "length", SecondaryField = "breadth", ModeHint = "Polyline area"
        },
        new ElementPickProfile
        {
            Category = "ssm", Label = "SSM", PickMode = TakeoffPickMode.Area,
            Prefix = "SSM", PrimaryField = "length", SecondaryField = "breadth", ModeHint = "Polyline area"
        },
        // Shuttering is calculated from RCC concrete members — not measured on drawing.
        new ElementPickProfile
        {
            Category = "flooring", Label = "Flooring", PickMode = TakeoffPickMode.Area,
            Prefix = "FL", PrimaryField = "length", SecondaryField = "breadth", ModeHint = "Polyline area"
        },
        new ElementPickProfile
        {
            Category = "painting", Label = "Paint", PickMode = TakeoffPickMode.Line,
            Prefix = "PT", PrimaryField = "length", ModeHint = "Line measure"
        },
        new ElementPickProfile
        {
            Category = "waterproofing", Label = "Waterproof", PickMode = TakeoffPickMode.Area,
            Prefix = "WP", PrimaryField = "length", SecondaryField = "breadth", ModeHint = "Polyline area"
        },
        new ElementPickProfile
        {
            Category = "dpc", Label = "DPC", PickMode = TakeoffPickMode.Line,
            Prefix = "DPC", PrimaryField = "length", ModeHint = "Line measure"
        },
        new ElementPickProfile
        {
            Category = "screed", Label = "Screed", PickMode = TakeoffPickMode.Area,
            Prefix = "SC", PrimaryField = "length", SecondaryField = "breadth", ModeHint = "Polyline area"
        },
        new ElementPickProfile
        {
            Category = "vdf", Label = "VDF", PickMode = TakeoffPickMode.Area,
            Prefix = "VDF", PrimaryField = "length", SecondaryField = "breadth", ModeHint = "Polyline area"
        },
        new ElementPickProfile
        {
            Category = "skirting", Label = "Skirting", PickMode = TakeoffPickMode.Line,
            Prefix = "SK", PrimaryField = "length", ModeHint = "Line measure"
        },
        new ElementPickProfile
        {
            Category = "parapet", Label = "Parapet", PickMode = TakeoffPickMode.Line,
            Prefix = "PR", PrimaryField = "length", ModeHint = "Line measure"
        },
        new ElementPickProfile
        {
            Category = "plinth_protection", Label = "Plinth prot.", PickMode = TakeoffPickMode.Area,
            Prefix = "PP", PrimaryField = "length", SecondaryField = "breadth", ModeHint = "Polyline area"
        },
        new ElementPickProfile
        {
            Category = "coping", Label = "Coping", PickMode = TakeoffPickMode.Line,
            Prefix = "CP", PrimaryField = "length", ModeHint = "Line measure"
        },
    };

    public static ElementPickProfile? Find(string category) =>
        All.FirstOrDefault(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

    public static ElementPickProfile Default => All[4]; // Wall / masonry
}

public sealed class TakeoffPoint
{
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class TakeoffItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Category { get; set; } = "masonry";
    public string Level { get; set; } = "Lvl0";
    public string Mark { get; set; } = "";
    public string Tool { get; set; } = "MeasureLine";
    public List<TakeoffPoint> Points { get; } = new();
    public double LengthMm { get; set; }
    public Dictionary<string, string> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool Committed { get; set; }
    /// <summary>Which geometry field length maps to (length/span/height).</summary>
    public string MappedField { get; set; } = "length";
}

public sealed class TakeoffState
{
    public string? PdfPath { get; set; }
    public int Page { get; set; }
    public double MmPerPx { get; set; }
    public ObservableCollection<TakeoffItem> Items { get; } = new();

    public void Clear()
    {
        PdfPath = null;
        Page = 0;
        MmPerPx = 0;
        Items.Clear();
    }

    public JsonObject ToJson()
    {
        var items = new JsonArray();
        foreach (var it in Items)
        {
            var pts = new JsonArray();
            foreach (var p in it.Points)
                pts.Add(new JsonObject { ["x"] = p.X, ["y"] = p.Y });
            var fields = new JsonObject();
            foreach (var kv in it.Fields) fields[kv.Key] = kv.Value;
            items.Add(new JsonObject
            {
                ["id"] = it.Id,
                ["category"] = it.Category,
                ["level"] = it.Level,
                ["mark"] = it.Mark,
                ["tool"] = it.Tool,
                ["points"] = pts,
                ["length_mm"] = it.LengthMm,
                ["fields"] = fields,
                ["committed"] = it.Committed ? 1 : 0,
                ["mapped_field"] = it.MappedField
            });
        }
        return new JsonObject
        {
            ["pdf_path"] = PdfPath ?? "",
            ["page"] = Page,
            ["mm_per_px"] = MmPerPx,
            ["items"] = items
        };
    }

    public void LoadFrom(JsonObject? o)
    {
        Clear();
        if (o is null) return;
        PdfPath = o["pdf_path"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(PdfPath)) PdfPath = null;
        Page = (int)Num(o, "page", 0);
        MmPerPx = Num(o, "mm_per_px", 0);
        if (o["items"] is not JsonArray arr) return;
        foreach (var node in arr)
        {
            if (node is not JsonObject jo) continue;
            var it = new TakeoffItem
            {
                Id = jo["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N"),
                Category = jo["category"]?.GetValue<string>() ?? "masonry",
                Level = jo["level"]?.GetValue<string>() ?? "Lvl0",
                Mark = jo["mark"]?.GetValue<string>() ?? "",
                Tool = jo["tool"]?.GetValue<string>() ?? "MeasureLine",
                LengthMm = Num(jo, "length_mm", 0),
                Committed = Num(jo, "committed", 0) != 0,
                MappedField = jo["mapped_field"]?.GetValue<string>() ?? "length"
            };
            if (jo["points"] is JsonArray pa)
            {
                foreach (var p in pa)
                {
                    if (p is not JsonObject po) continue;
                    it.Points.Add(new TakeoffPoint { X = Num(po, "x", 0), Y = Num(po, "y", 0) });
                }
            }
            if (jo["fields"] is JsonObject fo)
            {
                foreach (var kv in fo)
                    it.Fields[kv.Key] = kv.Value?.GetValue<string>()
                        ?? (kv.Value is JsonValue jv && jv.TryGetValue<double>(out var d)
                            ? d.ToString(CultureInfo.InvariantCulture) : "");
            }
            Items.Add(it);
        }
    }

    private static double Num(JsonObject o, string key, double def)
    {
        if (o[key] is JsonValue jv && jv.TryGetValue<double>(out var d)) return d;
        return def;
    }

    public static string PrefixForCategory(string category) =>
        ElementPickProfile.Find(category)?.Prefix
        ?? category.ToLowerInvariant() switch
        {
            "column" or "rcc-column" => "C",
            "beam" or "rcc-beam" => "B",
            "slab" or "rcc-slab" => "S",
            "footing" => "F",
            "earth" => "EW",
            "paint" => "PT",
            _ => "TK"
        };

    public static string NextMark(ProjectStore store, string category, string level)
    {
        string prefix = PrefixForCategory(category);
        var existing = CollectMarks(store, category)
            .Concat(store.Takeoff.Items.Where(i => i.Category.Equals(category, StringComparison.OrdinalIgnoreCase)
                                                && i.Level.Equals(level, StringComparison.OrdinalIgnoreCase))
                .Select(i => i.Mark));
        int max = 0;
        string needle = prefix + "-" + level + "-";
        foreach (var m in existing)
        {
            if (string.IsNullOrWhiteSpace(m)) continue;
            if (m.StartsWith(needle, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(m.AsSpan(needle.Length), out var n))
                max = Math.Max(max, n);
            else if (m.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                     && int.TryParse(new string(m.SkipWhile(c => !char.IsDigit(c)).ToArray()), out var n2))
                max = Math.Max(max, n2);
        }
        return $"{prefix}-{level}-{(max + 1).ToString("000", CultureInfo.InvariantCulture)}";
    }

    private static IEnumerable<string> CollectMarks(ProjectStore store, string category)
    {
        var rows = category.ToLowerInvariant() switch
        {
            "columns" or "column" => store.Columns,
            "beams" or "beam" => store.Beams,
            "pedestals" or "pedestal" => store.Pedestals,
            "lintels" or "lintel" => store.Lintels,
            "slabs" or "slab" => store.Slabs,
            "footings" or "footing" => store.Footings,
            "masonry" => store.MasonryWalls,
            "plaster" => store.Plaster,
            "pcc" => store.PccBeds,
            "earthwork" or "earth" => store.Earthwork,
            "ssm" => store.SizeStone,
            "shuttering" => store.Shuttering,
            "flooring" => store.Flooring,
            "painting" or "paint" => store.Painting,
            "waterproofing" => store.Waterproofing,
            "dpc" => store.Dpc,
            "coping" => store.Coping,
            "screed" => store.Screed,
            "vdf" => store.Vdf,
            "skirting" => store.Skirting,
            "parapet" => store.Parapet,
            "plinth_protection" or "plinth" => store.PlinthProtection,
            "doors" or "door" => store.Doors,
            "windows" or "window" => store.Windows,
            _ => null
        };
        if (rows is null) yield break;
        foreach (var r in rows)
            if (r.TryGetValue("mark", out var m)) yield return m;
    }

    public static ObservableCollection<Dictionary<string, string>>? CollectionFor(ProjectStore store, string category) =>
        category.ToLowerInvariant() switch
        {
            "columns" or "column" => store.Columns,
            "beams" or "beam" => store.Beams,
            "pedestals" or "pedestal" => store.Pedestals,
            "lintels" or "lintel" => store.Lintels,
            "slabs" or "slab" => store.Slabs,
            "footings" or "footing" => store.Footings,
            "masonry" => store.MasonryWalls,
            "plaster" => store.Plaster,
            "pcc" => store.PccBeds,
            "earthwork" or "earth" => store.Earthwork,
            "ssm" => store.SizeStone,
            "shuttering" => store.Shuttering,
            "flooring" => store.Flooring,
            "painting" or "paint" => store.Painting,
            "waterproofing" => store.Waterproofing,
            "dpc" => store.Dpc,
            "coping" => store.Coping,
            "screed" => store.Screed,
            "vdf" => store.Vdf,
            "skirting" => store.Skirting,
            "parapet" => store.Parapet,
            "plinth_protection" or "plinth" => store.PlinthProtection,
            "doors" or "door" => store.Doors,
            "windows" or "window" => store.Windows,
            _ => null
        };

    public static Dictionary<string, string> DefaultRow(string category, string mark, string level, double lengthMm, string mappedField)
        => DefaultRow(category, mark, level, lengthMm, 0, mappedField, null);

    public static Dictionary<string, string> DefaultRow(
        string category, string mark, string level,
        double lengthMm, double breadthMm, string mappedField,
        IReadOnlyDictionary<string, string>? extraFields)
    {
        string len = lengthMm.ToString("0.###", CultureInfo.InvariantCulture);
        string br = breadthMm > 0
            ? breadthMm.ToString("0.###", CultureInfo.InvariantCulture)
            : "0";
        var profile = ElementPickProfile.Find(category);
        string primary = string.IsNullOrWhiteSpace(mappedField)
            ? (profile?.PrimaryField ?? "length")
            : mappedField;
        string secondary = profile?.SecondaryField ?? "breadth";

        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["mark"] = mark,
            ["level"] = level
        };
        switch (category.ToLowerInvariant())
        {
            case "columns":
            case "column":
                row["width"] = "300"; row["depth"] = "450";
                {
                    double ch = ProjectStore.Current.ColumnHeightFor(level);
                    row["height"] = ch > 0 ? ch.ToString("0", CultureInfo.InvariantCulture) : "3000";
                }
                row["cover"] = "40"; row["concrete_grade"] = "M25"; row["column_type"] = "Rectangular";
                row["stirrup_dia"] = "8"; row["spacing"] = "150"; row["bars"] = "16:8"; row["steel_grade"] = "Fe500";
                break;
            case "pedestals":
            case "pedestal":
                row["width"] = "450"; row["depth"] = "450";
                row["height"] = "600";
                row["cover"] = "50"; row["concrete_grade"] = "M25"; row["column_type"] = "Rectangular";
                row["stirrup_dia"] = "8"; row["spacing"] = "150"; row["bars"] = "16:4"; row["steel_grade"] = "Fe500";
                break;
            case "beams":
            case "beam":
                row["span"] = lengthMm > 0 ? len : "4000";
                row["width"] = "230"; row["depth"] = "450"; row["cover"] = "25";
                row["concrete_grade"] = "M25"; row["steel_grade"] = "Fe500";
                break;
            case "lintels":
            case "lintel":
                row["opening"] = lengthMm > 0 ? len : "1200";
                row["bearing"] = "150"; row["width"] = "230"; row["depth"] = "150";
                row["cover"] = "25"; row["concrete_grade"] = "M20"; row["steel_grade"] = "Fe500";
                break;
            case "slabs":
            case "slab":
                row["span_x"] = lengthMm > 0 ? len : "3000";
                row["span_y"] = breadthMm > 0 ? br : "4500";
                row["thickness"] = "125"; row["cover"] = "20"; row["slab_type"] = "Two-Way";
                break;
            case "footings":
            case "footing":
                row["footing_type"] = "Isolated";
                row["length_l"] = lengthMm > 0 ? len : "2000";
                row["width_b"] = breadthMm > 0 ? br : "2000";
                row["col_dim_l"] = "400"; row["col_dim_b"] = "400";
                row["depth"] = "500"; row["cover"] = "50";
                row["concrete_grade"] = "M25"; row["steel_grade"] = "Fe500";
                row["dia_l"] = "12"; row["spacing_l"] = "150"; row["dia_b"] = "12"; row["spacing_b"] = "150";
                break;
            case "masonry":
                row["length"] = lengthMm > 0 ? len : "5000";
                {
                    double wh = ProjectStore.Current.ColumnHeightFor(level);
                    row["height"] = wh > 0 ? wh.ToString("0", CultureInfo.InvariantCulture) : "3000";
                }
                row["mortar_mix"] = "1:6";
                row["deduct_rule"] = "IS1200 masonry";
                MasonryWallBuild.Apply(row, "Brick · 230 mm");
                break;
            case "plaster":
                row["length"] = lengthMm > 0 ? len : "5000";
                row["height"] = "3000";
                row["thickness"] = "12"; row["faces"] = "1"; row["mortar_mix"] = "1:4";
                row["deduct_rule"] = "IS1200 plaster/paint"; row["add_jambs"] = "No";
                break;
            case "pcc":
                row["length"] = lengthMm > 0 ? len : "3000";
                row["breadth"] = breadthMm > 0 ? br : "2000";
                row["thickness"] = "100"; row["mix"] = "1:4:8";
                break;
            case "earthwork":
            case "earth":
                row["length"] = lengthMm > 0 ? len : "10000";
                row["breadth"] = breadthMm > 0 ? br : "3000";
                row["depth"] = "1500"; row["work_type"] = "Excavation";
                break;
            case "ssm":
                row["length"] = lengthMm > 0 ? len : "5000";
                row["breadth"] = breadthMm > 0 ? br : "450";
                row["height"] = "1500"; row["mortar_mix"] = "1:6";
                break;
            case "shuttering":
                row["member_type"] = "Manual";
                row["length"] = lengthMm > 0 ? len : "4000";
                row["breadth"] = "230"; row["depth"] = "450"; row["height"] = "0";
                row["area_m2"] = "0"; row["notes"] = "from takeoff";
                break;
            case "flooring":
                row["length"] = lengthMm > 0 ? len : "4000";
                row["breadth"] = breadthMm > 0 ? br : "3000";
                row["finish_type"] = "Vitrified tiles";
                row["surface_kind"] = "Floor";
                row["tile_size"] = "600×600";
                row["deduct_rule"] = "Openings full";
                break;
            case "painting":
            case "paint":
                row["length"] = lengthMm > 0 ? len : "5000";
                row["height"] = "3000";
                row["paint_type"] = "Emulsion";
                row["paint_location"] = "Inside walls";
                row["paint_system"] = "2 coat primer + 3 coat putty + 2 coat paint";
                row["faces"] = "1";
                row["coats"] = "2";
                row["deduct_rule"] = "IS1200 plaster/paint";
                break;
            case "waterproofing":
                row["length"] = lengthMm > 0 ? len : "4000";
                row["breadth"] = breadthMm > 0 ? br : "3000";
                row["type"] = "Membrane"; row["notes"] = "";
                break;
            case "dpc":
                row["length"] = lengthMm > 0 ? len : "5000";
                row["width"] = "230"; row["thickness"] = "40"; row["mix"] = "1:2:4";
                break;
            case "coping":
                row["length"] = lengthMm > 0 ? len : "5000";
                row["width"] = "300"; row["thickness"] = "50"; row["concrete_grade"] = "M20";
                break;
            case "screed":
                row["length"] = lengthMm > 0 ? len : "4000";
                row["breadth"] = breadthMm > 0 ? br : "3000";
                row["thickness"] = "40"; row["mix"] = "1:4";
                break;
            case "vdf":
                row["length"] = lengthMm > 0 ? len : "4000";
                row["breadth"] = breadthMm > 0 ? br : "3000";
                row["thickness"] = "100"; row["concrete_grade"] = "M25";
                break;
            case "skirting":
                row["length"] = lengthMm > 0 ? len : "5000";
                row["height"] = "100"; row["thickness"] = "10"; row["finish_type"] = "Tile";
                break;
            case "parapet":
                row["length"] = lengthMm > 0 ? len : "5000";
                row["height"] = "900"; row["thickness"] = "115"; row["unit_type"] = "Brick";
                break;
            case "plinth_protection":
            case "plinth":
                row["length"] = lengthMm > 0 ? len : "5000";
                row["breadth"] = breadthMm > 0 ? br : "600";
                row["thickness"] = "75"; row["concrete_grade"] = "M15";
                break;
            default:
                row["length"] = len;
                break;
        }

        if (lengthMm > 0) row[primary] = len;
        if (breadthMm > 0 && !string.IsNullOrEmpty(secondary)) row[secondary] = br;
        if (extraFields is not null)
            foreach (var kv in extraFields) row[kv.Key] = kv.Value;
        return row;
    }
}
