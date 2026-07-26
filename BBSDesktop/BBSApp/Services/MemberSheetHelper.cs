using System.Globalization;
using System.Text.RegularExpressions;

namespace BBSApp.Services;

/// <summary>Floor-scoped sheet helpers: marks (C/RB/PB/P/L), Nos expansion, cover/height stamping.</summary>
public static class MemberSheetHelper
{
    private static readonly Regex MarkRx = new(@"^([A-Za-z]+)(\d+)$", RegexOptions.Compiled);

    public static bool UsesFloorScope(string kind) => kind is
        "columns" or "beams" or "slabs" or "footings" or "walls" or "stairs"
        or "pedestals" or "lintels"
        or "masonry" or "plaster" or "pcc" or "earthwork" or "ssm"
        or "flooring" or "painting" or "waterproofing" or "dpc" or "coping"
        or "screed" or "vdf" or "skirting" or "parapet" or "plinth_protection"
        or "doors" or "windows";

    public static bool IsSheetHiddenKey(string kind, string key)
    {
        if (key.Equals("level", StringComparison.OrdinalIgnoreCase)) return true;
        if (kind == "columns" && key.Equals("height", StringComparison.OrdinalIgnoreCase)) return true;
        if (key.Equals("cover", StringComparison.OrdinalIgnoreCase)) return true;
        if (key.Equals("provide_lap", StringComparison.OrdinalIgnoreCase)) return true;
        if (key.Equals("lap_nos", StringComparison.OrdinalIgnoreCase)) return true;
        if (kind == "masonry")
        {
            if (key.Equals("unit_type", StringComparison.OrdinalIgnoreCase)) return true;
            if (key.Equals("thickness", StringComparison.OrdinalIgnoreCase)) return true;
            if (key.Equals("block_size", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public static string MarkPrefix(string kind, Dictionary<string, string> row)
    {
        return kind switch
        {
            "columns" => "C",
            "beams" => row.TryGetValue("beam_type", out var bt) &&
                       bt.Equals("PB", StringComparison.OrdinalIgnoreCase) ? "PB" : "RB",
            "pedestals" => "P",
            "lintels" => "L",
            "slabs" => "S",
            "footings" => "F",
            "walls" => "RW",
            "stairs" => "ST",
            "doors" => "D",
            "windows" => "W",
            _ => "R"
        };
    }

    public static string SuggestNextMark(string kind, IEnumerable<Dictionary<string, string>> rows, Dictionary<string, string>? prototype = null)
    {
        var proto = prototype ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string prefix = MarkPrefix(kind, proto);
        int max = 0;
        foreach (var r in rows)
        {
            if (!r.TryGetValue("mark", out var m) || string.IsNullOrWhiteSpace(m)) continue;
            var match = MarkRx.Match(m.Trim());
            if (!match.Success) continue;
            if (!match.Groups[1].Value.Equals(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (int.TryParse(match.Groups[2].Value, out var n) && n > max) max = n;
        }
        return $"{prefix}{max + 1}";
    }

    public static int ParseNos(Dictionary<string, string> row)
    {
        if (!row.TryGetValue("nos", out var s) || string.IsNullOrWhiteSpace(s)) return 1;
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return 1;
        return Math.Max(1, (int)Math.Round(v));
    }

    public static List<Dictionary<string, string>> ExpandForGenerate(
        string kind, IEnumerable<Dictionary<string, string>> rows)
    {
        var source = rows.ToList();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in source)
        {
            if (r.TryGetValue("mark", out var m) && !string.IsNullOrWhiteSpace(m))
                used.Add(m.Trim());
        }

        var result = new List<Dictionary<string, string>>();
        foreach (var row in source)
        {
            StampDefaults(kind, row);
            int nos = ParseNos(row);
            string prefix = MarkPrefix(kind, row);
            string baseMark = row.TryGetValue("mark", out var bm) && !string.IsNullOrWhiteSpace(bm)
                ? bm.Trim()
                : SuggestNextMark(kind, source.Concat(result), row);

            var marks = new List<string>();
            // Prefer keeping the design mark as first if free / matches prefix
            string? start = baseMark;
            var m0 = MarkRx.Match(baseMark);
            int startNum = m0.Success && m0.Groups[1].Value.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                ? int.Parse(m0.Groups[2].Value, CultureInfo.InvariantCulture)
                : NextFreeNumber(prefix, used);

            for (int i = 0; i < nos; i++)
            {
                string mark;
                if (i == 0 && m0.Success &&
                    m0.Groups[1].Value.Equals(prefix, StringComparison.OrdinalIgnoreCase) &&
                    (!used.Contains(baseMark) || nos == 1))
                {
                    mark = baseMark;
                }
                else
                {
                    int n = startNum;
                    while (used.Contains($"{prefix}{n}")) n++;
                    mark = $"{prefix}{n}";
                    startNum = n + 1;
                }
                used.Add(mark);
                marks.Add(mark);
            }

            foreach (var mark in marks)
            {
                var clone = new Dictionary<string, string>(row, StringComparer.OrdinalIgnoreCase)
                {
                    ["mark"] = mark,
                    ["nos"] = "1"
                };
                if (kind == "lintels") EnsureLintelSpan(clone);
                StampDefaults(kind, clone);
                result.Add(clone);
            }
        }
        return result;
    }

    private static int NextFreeNumber(string prefix, HashSet<string> used)
    {
        int n = 1;
        while (used.Contains($"{prefix}{n}")) n++;
        return n;
    }

    public static void StampDefaults(string kind, Dictionary<string, string> row)
    {
        var store = ProjectStore.Current;
        if (!row.TryGetValue("level", out var level) || string.IsNullOrWhiteSpace(level))
            level = store.Levels.FirstOrDefault()?.Id ?? "Lvl0";
        row["level"] = level;

        if (kind is "columns")
        {
            double h = store.ColumnHeightFor(level);
            if (h > 0)
                row["height"] = h.ToString("0", CultureInfo.InvariantCulture);
        }

        string coverKey = kind switch
        {
            "columns" => "column",
            "beams" or "lintels" => "beam",
            "slabs" => "slab",
            "footings" => "footing",
            "pedestals" => "pedestal",
            "walls" => "footing",
            "stairs" => "slab",
            _ => ""
        };
        if (!string.IsNullOrEmpty(coverKey) &&
            (!row.TryGetValue("cover", out var cov) || string.IsNullOrWhiteSpace(cov)))
            row["cover"] = store.DefaultCoverMm(coverKey).ToString("0", CultureInfo.InvariantCulture);

        if (kind == "columns" &&
            (!row.TryGetValue("provide_lap", out var pl) || string.IsNullOrWhiteSpace(pl)))
            row["provide_lap"] = store.DefaultColumnLap;

        if ((kind is "beams" or "lintels") &&
            (!row.TryGetValue("provide_lap", out var bl) || string.IsNullOrWhiteSpace(bl)))
            row["provide_lap"] = store.DefaultBeamLap;

        if (kind == "beams" &&
            (!row.TryGetValue("beam_type", out var bt) || string.IsNullOrWhiteSpace(bt)))
            row["beam_type"] = level.Equals("Lvl0", StringComparison.OrdinalIgnoreCase) ? "PB" : "RB";

        if (kind == "masonry")
            MasonryWallBuild.EnsureWallBuild(row);

        if (kind == "lintels") EnsureLintelSpan(row);
    }

    public static void EnsureLintelSpan(Dictionary<string, string> row)
    {
        if (row.TryGetValue("span", out var sp) && !string.IsNullOrWhiteSpace(sp) &&
            double.TryParse(sp, NumberStyles.Float, CultureInfo.InvariantCulture, out var existing) && existing > 0)
            return;
        double opening = ParseD(row, "opening", 900);
        double bearing = ParseD(row, "bearing", 150);
        row["span"] = (opening + 2 * bearing).ToString("0", CultureInfo.InvariantCulture);
    }

    private static double ParseD(Dictionary<string, string> row, string key, double def)
    {
        if (!row.TryGetValue(key, out var s) ||
            !double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            return def;
        return v;
    }

    /// <summary>Engine kind: pedestals → columns, lintels → beams.</summary>
    public static string EngineKind(string kind) => kind switch
    {
        "pedestals" => "columns",
        "lintels" => "beams",
        _ => kind
    };
}
