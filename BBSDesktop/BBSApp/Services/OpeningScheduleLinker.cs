using System.Globalization;
using BBSApp.Models;

namespace BBSApp.Services;

/// <summary>
/// Link takeoff Opening rectangles to MasonryOpenings (wall deduct) + Doors/Windows schedules.
/// </summary>
public static class OpeningScheduleLinker
{
    public enum ScheduleKind { Door, Window, DeductOnly }

    public sealed class CommitResult
    {
        public string WallMark { get; init; } = "";
        public string ScheduleMark { get; init; } = "";
        public ScheduleKind Kind { get; init; }
        public string Message { get; init; } = "";
    }

    /// <summary>Nearest masonry wall mark for an opening (takeoff geometry, then BOQ walls on level).</summary>
    public static string SuggestWallMark(ProjectStore store, TakeoffItem opening)
    {
        if (opening.Fields.TryGetValue("wall_mark", out var preset) && !string.IsNullOrWhiteSpace(preset))
            return preset.Trim();

        var center = OpeningCenter(opening);
        string? bestMark = null;
        double bestDist = double.MaxValue;

        foreach (var it in store.Takeoff.Items)
        {
            if (!it.Category.Equals("masonry", StringComparison.OrdinalIgnoreCase)) continue;
            if (it.Points.Count < 2) continue;
            if (!string.IsNullOrEmpty(opening.Level)
                && !it.Level.Equals(opening.Level, StringComparison.OrdinalIgnoreCase))
                continue;
            double d = DistToPolyline(center, it.Points);
            if (d < bestDist)
            {
                bestDist = d;
                bestMark = string.IsNullOrWhiteSpace(it.Mark) ? null : it.Mark.Trim();
            }
        }

        if (bestMark is not null && bestDist < 1e9)
            return bestMark;

        // Fallback: first masonry wall on same level
        foreach (var w in store.MasonryWalls)
        {
            string lvl = MaterialsCalculator.RowLevel(w);
            if (!string.IsNullOrEmpty(opening.Level)
                && !lvl.Equals(opening.Level, StringComparison.OrdinalIgnoreCase))
                continue;
            if (w.TryGetValue("mark", out var m) && !string.IsNullOrWhiteSpace(m))
                return m.Trim();
        }
        return store.MasonryWalls.FirstOrDefault()?.TryGetValue("mark", out var any) == true ? any! : "MW1";
    }

    public static ScheduleKind SuggestKind(TakeoffItem opening)
    {
        if (opening.Fields.TryGetValue("opening_kind", out var k))
        {
            if (k.Equals("Door", StringComparison.OrdinalIgnoreCase)
                || k.Equals("doors", StringComparison.OrdinalIgnoreCase))
                return ScheduleKind.Door;
            if (k.Equals("Window", StringComparison.OrdinalIgnoreCase)
                || k.Equals("windows", StringComparison.OrdinalIgnoreCase))
                return ScheduleKind.Window;
            if (k.Equals("Deduct", StringComparison.OrdinalIgnoreCase)
                || k.Equals("DeductOnly", StringComparison.OrdinalIgnoreCase))
                return ScheduleKind.DeductOnly;
        }
        double w = Parse(opening.Fields, "opening_l");
        double h = Parse(opening.Fields, "opening_h");
        // Tall openings → door; wide → window
        if (h > 0 && w > 0 && h >= w * 1.15) return ScheduleKind.Door;
        return ScheduleKind.Window;
    }

    public static IReadOnlyList<string> WallMarkChoices(ProjectStore store, string level)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in store.MasonryWalls)
        {
            if (!string.IsNullOrEmpty(level)
                && !MaterialsCalculator.RowLevel(w).Equals(level, StringComparison.OrdinalIgnoreCase))
                continue;
            if (w.TryGetValue("mark", out var m) && !string.IsNullOrWhiteSpace(m))
                set.Add(m.Trim());
        }
        foreach (var it in store.Takeoff.Items)
        {
            if (!it.Category.Equals("masonry", StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrEmpty(level)
                && !it.Level.Equals(level, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrWhiteSpace(it.Mark)) set.Add(it.Mark.Trim());
        }
        return set.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Create MasonryOpenings deduct line + Door/Window schedule (unless DeductOnly).
    /// </summary>
    public static CommitResult Commit(
        ProjectStore store,
        TakeoffItem opening,
        ScheduleKind kind,
        string wallMark)
    {
        double w = Parse(opening.Fields, "opening_l");
        double h = Parse(opening.Fields, "opening_h");
        int nos = Math.Max(1, (int)Parse(opening.Fields, "opening_nos", 1));
        if (w <= 0 || h <= 0)
            throw new InvalidOperationException("Opening needs width and height (mm).");

        string level = string.IsNullOrWhiteSpace(opening.Level) ? "Lvl0" : opening.Level;
        wallMark = string.IsNullOrWhiteSpace(wallMark) ? SuggestWallMark(store, opening) : wallMark.Trim();
        string takeoffId = opening.Id;

        // Avoid duplicate masonry opening for same takeoff
        bool hasOpen = store.MasonryOpenings.Any(o =>
            o.TryGetValue("takeoff_id", out var tid) && tid.Equals(takeoffId, StringComparison.OrdinalIgnoreCase));
        if (!hasOpen)
        {
            store.MasonryOpenings.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["wall_mark"] = wallMark,
                ["level"] = level,
                ["nos"] = nos.ToString(CultureInfo.InvariantCulture),
                ["opening_l"] = Inv(w),
                ["opening_h"] = Inv(h),
                ["takeoff_id"] = takeoffId,
                ["opening_kind"] = kind switch
                {
                    ScheduleKind.Door => "Door",
                    ScheduleKind.Window => "Window",
                    _ => "Other"
                }
            });
        }

        string scheduleMark = "";
        if (kind == ScheduleKind.Door)
        {
            scheduleMark = NextMark(store.Doors, "D");
            store.Doors.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["mark"] = scheduleMark,
                ["level"] = level,
                ["door_type"] = "Wood door",
                ["nos"] = nos.ToString(CultureInfo.InvariantCulture),
                ["width"] = Inv(w),
                ["height"] = Inv(h),
                ["frame_size"] = DoorWindowCatalog.WoodFrames[0],
                ["shutter_thick"] = DoorWindowCatalog.ShutterThicknesses[0],
                ["shutter_type"] = DoorWindowCatalog.ShutterTypeNames[0],
                ["wood_finish"] = FinishCatalog.WoodFinishes[0],
                ["wall_mark"] = wallMark,
                ["takeoff_id"] = takeoffId,
                ["deduct_from_wall"] = "Yes",
                ["notes"] = $"From takeoff · wall {wallMark}"
            });
        }
        else if (kind == ScheduleKind.Window)
        {
            scheduleMark = NextMark(store.Windows, "W");
            store.Windows.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["mark"] = scheduleMark,
                ["level"] = level,
                ["window_system"] = "System Aluminium",
                ["nos"] = nos.ToString(CultureInfo.InvariantCulture),
                ["width"] = Inv(w),
                ["height"] = Inv(h),
                ["track"] = DoorWindowCatalog.Tracks[0],
                ["wood_opening"] = DoorWindowCatalog.WoodOpenings[0],
                ["wood_finish"] = FinishCatalog.WoodFinishes[0],
                ["wall_mark"] = wallMark,
                ["takeoff_id"] = takeoffId,
                ["deduct_from_wall"] = "Yes",
                ["notes"] = $"From takeoff · wall {wallMark}"
            });
        }

        opening.Fields["wall_mark"] = wallMark;
        opening.Fields["opening_kind"] = kind.ToString();
        opening.Committed = true;

        string msg = kind switch
        {
            ScheduleKind.Door => $"Opening → Masonry deduct + Door {scheduleMark} on wall {wallMark}",
            ScheduleKind.Window => $"Opening → Masonry deduct + Window {scheduleMark} on wall {wallMark}",
            _ => $"Opening → Masonry deduct only on wall {wallMark}"
        };
        return new CommitResult
        {
            WallMark = wallMark,
            ScheduleMark = scheduleMark,
            Kind = kind,
            Message = msg
        };
    }

    private static string NextMark(IEnumerable<Dictionary<string, string>> rows, string prefix)
    {
        int max = 0;
        foreach (var r in rows)
        {
            if (!r.TryGetValue("mark", out var m) || string.IsNullOrWhiteSpace(m)) continue;
            m = m.Trim();
            if (!m.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            string num = m[prefix.Length..];
            if (int.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > max)
                max = n;
        }
        return $"{prefix}{max + 1}";
    }

    private static TakeoffPoint OpeningCenter(TakeoffItem opening)
    {
        if (opening.Points.Count >= 2)
        {
            return new TakeoffPoint
            {
                X = (opening.Points[0].X + opening.Points[1].X) * 0.5,
                Y = (opening.Points[0].Y + opening.Points[1].Y) * 0.5
            };
        }
        return new TakeoffPoint();
    }

    private static double DistToPolyline(TakeoffPoint p, IReadOnlyList<TakeoffPoint> pts)
    {
        double best = double.MaxValue;
        for (int i = 0; i + 1 < pts.Count; i++)
            best = Math.Min(best, DistToSegment(p, pts[i], pts[i + 1]));
        return best;
    }

    private static double DistToSegment(TakeoffPoint p, TakeoffPoint a, TakeoffPoint b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double len2 = dx * dx + dy * dy;
        if (len2 < 1e-9)
            return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
        double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2;
        t = Math.Clamp(t, 0, 1);
        double qx = a.X + t * dx, qy = a.Y + t * dy;
        return Math.Sqrt((p.X - qx) * (p.X - qx) + (p.Y - qy) * (p.Y - qy));
    }

    private static double Parse(IReadOnlyDictionary<string, string> f, string key, double def = 0)
    {
        if (!f.TryGetValue(key, out var s) || string.IsNullOrWhiteSpace(s)) return def;
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;
    }

    private static string Inv(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
}
