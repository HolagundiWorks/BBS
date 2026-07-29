// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using System.Globalization;
using System.Text.RegularExpressions;

namespace BBSApp.Services;

/// <summary>IS 456 : 2000 Cl. 26.5.3.2 — column longitudinal + tie arrangement cases.</summary>
public enum ColumnTieCase
{
    Closed,          // peripheral closed only (small / medium ≤75 mm spacing)
    OpenTies,        // peripheral + alternative open ties (rect.)
    UTies,           // peripheral + U / intermediate ties (large)
    GroupTies,       // peripheral + individual corner group ties
    CrossTies,       // peripheral + cross ties (spacing > 75 mm)
    DiagonalTies,    // peripheral + diagonal closed tie
    Circular,
    Spiral
}

public readonly record struct ColumnBarPoint(double U, double V, int Dia);
// U,V in 0..1 relative to clear cage (inside ties)

/// <summary>Resolved column detailing for sketch + captions.</summary>
public sealed class ColumnArrangement
{
    public ColumnTieCase TieCase { get; init; }
    public string Label { get; init; } = "";
    public string Note { get; init; } = "";
    public List<ColumnBarPoint> Bars { get; init; } = new();
    public int TotalBars { get; init; }
    public double ClearB { get; init; }   // mm inside cover
    public double ClearD { get; init; }
    public double EstBarSpacing { get; init; } // mm along longer face
}

public static class ColumnLayout
{
    public static int CountBars(string? barsToken)
    {
        int n = 0;
        if (string.IsNullOrWhiteSpace(barsToken)) return 0;
        foreach (Match m in Regex.Matches(barsToken, @"(\d+)\s*:\s*(\d+)"))
            if (int.TryParse(m.Groups[2].Value, out var nos)) n += nos;
        return n;
    }

    public static List<(int dia, int nos)> ParseGroups(string? barsToken)
    {
        var list = new List<(int, int)>();
        if (string.IsNullOrWhiteSpace(barsToken)) return list;
        foreach (Match m in Regex.Matches(barsToken, @"(\d+)\s*:\s*(\d+)"))
            if (int.TryParse(m.Groups[1].Value, out var d) && int.TryParse(m.Groups[2].Value, out var n))
                list.Add((d, n));
        return list;
    }

    public static List<int> Flatten(string? barsToken)
    {
        var dias = new List<int>();
        foreach (var (d, n) in ParseGroups(barsToken))
            for (int i = 0; i < Math.Clamp(n, 0, 24); i++) dias.Add(d);
        return dias;
    }

    public static string[] TiesForColumnType(string? columnType) => (columnType ?? "").Trim() switch
    {
        "Circular" => new[] { "Circular", "Spiral" },
        "Square" => new[] { "Auto", "Closed", "U-Ties", "Group Ties", "Cross Ties", "Diagonal Ties" },
        _ => new[] { "Auto", "Closed", "Open Ties", "U-Ties", "Group Ties", "Cross Ties", "Diagonal Ties" } // Rectangular
    };

    public static ColumnTieCase ResolveCase(string? tieType, string? columnType, double b, double d, int barCount, double cover, double tieDia)
    {
        var shape = (columnType ?? "Rectangular").Trim();
        if (shape.Equals("Circular", StringComparison.OrdinalIgnoreCase))
        {
            var ct = (tieType ?? "Circular").Trim();
            return ct.Equals("Spiral", StringComparison.OrdinalIgnoreCase)
                ? ColumnTieCase.Spiral
                : ColumnTieCase.Circular;
        }

        var t = (tieType ?? "Auto").Trim();
        if (t.Equals("Circular", StringComparison.OrdinalIgnoreCase)) return ColumnTieCase.Circular;
        if (t.Equals("Spiral", StringComparison.OrdinalIgnoreCase)) return ColumnTieCase.Spiral;
        if (t.Equals("Closed", StringComparison.OrdinalIgnoreCase)) return ColumnTieCase.Closed;
        if (t.Equals("Open Ties", StringComparison.OrdinalIgnoreCase))
            return shape.Equals("Square", StringComparison.OrdinalIgnoreCase) ? ColumnTieCase.Closed : ColumnTieCase.OpenTies;
        if (t.Equals("U-Ties", StringComparison.OrdinalIgnoreCase) || t.Equals("Double Tie", StringComparison.OrdinalIgnoreCase))
            return ColumnTieCase.UTies;
        if (t.Equals("Group Ties", StringComparison.OrdinalIgnoreCase)) return ColumnTieCase.GroupTies;
        if (t.Equals("Cross Ties", StringComparison.OrdinalIgnoreCase)) return ColumnTieCase.CrossTies;
        if (t.Equals("Diagonal Ties", StringComparison.OrdinalIgnoreCase)) return ColumnTieCase.DiagonalTies;

        // Auto for square / rectangular
        if (shape.Equals("Square", StringComparison.OrdinalIgnoreCase))
            d = b; // force square

        double minSide = Math.Min(b, d);
        double clearB = Math.Max(1, b - 2 * cover);
        double clearD = Math.Max(1, d - 2 * cover);
        double longer = Math.Max(clearB, clearD);
        int alongLong = Math.Max(2, (int)Math.Ceiling(barCount / 4.0) + 1);
        double spacing = longer / Math.Max(1, alongLong - 1);

        if (barCount <= 4 || minSide <= 300)
            return ColumnTieCase.Closed;
        if (barCount <= 8)
        {
            if (spacing > 75)
                return shape.Equals("Square", StringComparison.OrdinalIgnoreCase)
                    ? ColumnTieCase.CrossTies
                    : ColumnTieCase.DiagonalTies;
            return ColumnTieCase.Closed;
        }
        if (barCount >= 12 && shape.Equals("Square", StringComparison.OrdinalIgnoreCase))
            return ColumnTieCase.GroupTies;
        if (!shape.Equals("Square", StringComparison.OrdinalIgnoreCase) && Math.Abs(b - d) >= 50)
            return spacing > 75 || longer > 48 * Math.Max(tieDia, 6) ? ColumnTieCase.OpenTies : ColumnTieCase.UTies;
        return ColumnTieCase.CrossTies;
    }

    public static ColumnArrangement Arrange(double b, double d, double cover, double tieDia, string? barsToken, string? tieType, string? columnType = "Rectangular")
    {
        var shape = (columnType ?? "Rectangular").Trim();
        if (shape.Equals("Square", StringComparison.OrdinalIgnoreCase) ||
            shape.Equals("Circular", StringComparison.OrdinalIgnoreCase))
            d = b;

        var dias = Flatten(barsToken);
        if (dias.Count == 0) dias.AddRange(new[] { 16, 16, 16, 16 });
        int n = dias.Count;
        var tie = ResolveCase(tieType, shape, b, d, n, cover, tieDia);
        double clearB = Math.Max(1, b - 2 * Math.Max(cover, 0));
        double clearD = Math.Max(1, d - 2 * Math.Max(cover, 0));
        double longer = Math.Max(clearB, clearD);
        int along = Math.Max(2, (n + 3) / 4 + 1);
        double estSp = longer / Math.Max(1, along - 1);

        var pts = shape.Equals("Circular", StringComparison.OrdinalIgnoreCase)
            ? PlaceOnCircle(n, dias)
            : PlaceBarsSymmetric(n, dias, tie, clearB >= clearD);

        return new ColumnArrangement
        {
            TieCase = tie,
            Label = LabelOf(tie, shape),
            Note = NoteOf(tie, estSp, tieDia, shape),
            Bars = pts,
            TotalBars = n,
            ClearB = clearB,
            ClearD = clearD,
            EstBarSpacing = estSp
        };
    }

    private static List<ColumnBarPoint> PlaceOnCircle(int n, List<int> dias)
    {
        // Equal angles; diameters spaced for rotational symmetry (same φ opposite / interleaved).
        var ordered = OrderForCircularSymmetry(dias);
        var pts = new List<ColumnBarPoint>();
        for (int i = 0; i < n; i++)
        {
            double ang = -Math.PI / 2 + 2 * Math.PI * i / n; // start at top
            double u = 0.5 + 0.5 * Math.Cos(ang);
            double v = 0.5 + 0.5 * Math.Sin(ang);
            pts.Add(new ColumnBarPoint(u, v, ordered[i]));
        }
        return pts;
    }

    /// <summary>Place equal diameters at equal angular spacing around the ring.</summary>
    private static List<int> OrderForCircularSymmetry(List<int> dias)
    {
        int n = dias.Count;
        var result = Enumerable.Repeat(-1, n).ToList();
        int spin = 0;
        foreach (var g in dias.GroupBy(d => d).OrderByDescending(g => g.Key))
        {
            int count = g.Count();
            int dia = g.Key;
            for (int k = 0; k < count; k++)
            {
                int idx = (spin + (count == 1 ? 0 : (int)Math.Round(k * (double)n / count))) % n;
                int guard = 0;
                while (result[idx] >= 0 && guard++ < n)
                    idx = (idx + 1) % n;
                if (result[idx] < 0) result[idx] = dia;
            }
            spin++;
        }
        for (int i = 0; i < n; i++)
            if (result[i] < 0) result[i] = dias[Math.Min(i, dias.Count - 1)];
        return result;
    }

    private static string LabelOf(ColumnTieCase c, string shape) => c switch
    {
        ColumnTieCase.Circular => "Circular ties",
        ColumnTieCase.Spiral => "Spiral ties",
        ColumnTieCase.Closed => shape == "Square" ? "Peripheral closed (square)" : "Peripheral closed tie",
        ColumnTieCase.OpenTies => "Closed + open ties",
        ColumnTieCase.UTies => "Closed + U-ties",
        ColumnTieCase.GroupTies => "Closed + group ties",
        ColumnTieCase.CrossTies => "Closed + cross ties",
        ColumnTieCase.DiagonalTies => "Closed + diagonal tie",
        _ => "Ties"
    };

    private static string NoteOf(ColumnTieCase c, double spacing, double tieDia, string shape) => c switch
    {
        ColumnTieCase.Circular => "Circular column — circular ties (IS 456 Cl. 26.5.3)",
        ColumnTieCase.Spiral => "Circular column — continuous spiral ties",
        ColumnTieCase.Closed => spacing <= 75
            ? "IS 456 — peripheral closed; bar pitch ≤ 75 mm"
            : "IS 456 — peripheral closed (check pitch ≤ 75 mm or add crossties)",
        ColumnTieCase.CrossTies or ColumnTieCase.DiagonalTies =>
            $"Spacing > 75 mm — intermediate ties (48φ = {48 * Math.Max(tieDia, 6):0} mm limit)",
        ColumnTieCase.OpenTies => "Rectangular — alternative open ties for intermediate bars",
        ColumnTieCase.UTies => "Large section — U / intermediate ties",
        ColumnTieCase.GroupTies => "Corner groups with individual closed ties + peripheral",
        _ => $"IS 456 Cl. 26.5.3.2 · {shape}"
    };

    private static List<ColumnBarPoint> PlaceBarsSymmetric(int n, List<int> dias, ColumnTieCase tie, bool wide)
    {
        var slots = tie == ColumnTieCase.GroupTies && n >= 12
            ? GroupSlotsSymmetric(n)
            : SymmetricPerimeterSlots(n, wide);
        var diasAssigned = AssignDiametersByOrbit(slots, dias);
        var pts = new List<ColumnBarPoint>();
        for (int i = 0; i < slots.Count; i++)
            pts.Add(new ColumnBarPoint(slots[i].u, slots[i].v, diasAssigned[i]));
        return pts;
    }

    /// <summary>
    /// Perimeter slots with mirror symmetry: top≡bottom, left≡right; corners always occupied.
    /// </summary>
    private static List<(double u, double v)> SymmetricPerimeterSlots(int n, bool wide)
    {
        if (n <= 0) return new();
        if (n == 1) return new() { (0.5, 0.5) };
        if (n == 2) return new() { (0, 0), (1, 1) }; // opposite corners
        if (n == 3) return new() { (0, 0), (1, 0), (0.5, 1) };
        if (n == 4) return new() { (0, 0), (1, 0), (0, 1), (1, 1) };

        // Bars per face including corners; opposite faces equal.
        // Unique count: 2*T + 2*L - 4 = n  ⇒  T + L = (n + 4) / 2
        int sum = (n + 4) / 2;
        int tBars, lBars; // top/bottom count, left/right count (incl. corners)
        if (wide)
        {
            tBars = Math.Max(2, (sum + 1) / 2);
            lBars = Math.Max(2, sum - tBars);
        }
        else
        {
            lBars = Math.Max(2, (sum + 1) / 2);
            tBars = Math.Max(2, sum - lBars);
        }

        // Fix until unique count matches n (prefer even symmetry; odd n gets one mid face)
        int Unique() => 2 * tBars + 2 * lBars - 4;
        while (Unique() > n && (tBars > 2 || lBars > 2))
        {
            if (wide && tBars >= lBars && tBars > 2) tBars--;
            else if (lBars > 2) lBars--;
            else tBars--;
        }
        while (Unique() < n)
        {
            if (wide) tBars++;
            else lBars++;
        }

        var pts = new List<(double u, double v)>();
        // Top face v=0 (left→right), includes corners
        for (int i = 0; i < tBars; i++)
        {
            double u = tBars == 1 ? 0.5 : i / (double)(tBars - 1);
            pts.Add((u, 0));
        }
        // Right face u=1, skip corners
        for (int i = 1; i < lBars - 1; i++)
        {
            double v = i / (double)(lBars - 1);
            pts.Add((1, v));
        }
        // Bottom face v=1 (right→left), includes corners
        for (int i = 0; i < tBars; i++)
        {
            double u = tBars == 1 ? 0.5 : 1.0 - i / (double)(tBars - 1);
            pts.Add((u, 1));
        }
        // Left face u=0, skip corners
        for (int i = 1; i < lBars - 1; i++)
        {
            double v = 1.0 - i / (double)(lBars - 1);
            pts.Add((0, v));
        }

        // If odd leftover (Unique > n was fixed; Unique < n rare), trim farthest from corners
        while (pts.Count > n)
            pts.RemoveAt(pts.Count / 2);
        // If still short (shouldn't), add mid-side symmetrically
        while (pts.Count < n)
        {
            pts.Add((0.5, 0));
            if (pts.Count < n) pts.Add((0.5, 1));
            if (pts.Count < n) pts.Add((0, 0.5));
            if (pts.Count < n) pts.Add((1, 0.5));
        }
        return pts.Take(n).ToList();
    }

    private static List<(double u, double v)> GroupSlotsSymmetric(int n)
    {
        // Four identical corner clusters of 3 — chart case 5 (rotationally symmetric)
        var clusters = new (double u, double v)[]
        {
            (0.00, 0.00), (0.16, 0.00), (0.00, 0.16),
            (1.00, 0.00), (0.84, 0.00), (1.00, 0.16),
            (1.00, 1.00), (0.84, 1.00), (1.00, 0.84),
            (0.00, 1.00), (0.16, 1.00), (0.00, 0.84)
        };
        var list = clusters.Take(Math.Min(n, 12)).ToList();
        // Extra bars on mid-faces in opposite pairs
        var extras = new (double u, double v)[]
        {
            (0.5, 0), (0.5, 1), (0, 0.5), (1, 0.5),
            (0.33, 0), (0.67, 0), (0.33, 1), (0.67, 1),
            (0, 0.33), (0, 0.67), (1, 0.33), (1, 0.67)
        };
        int ei = 0;
        while (list.Count < n && ei < extras.Length)
            list.Add(extras[ei++]);
        return list;
    }

    /// <summary>
    /// Assign diameters so reflection orbits share the same φ (corners together, then face pairs).
    /// Opposite bars get matching φ; larger φ prefer corners.
    /// </summary>
    private static List<int> AssignDiametersByOrbit(List<(double u, double v)> slots, List<int> dias)
    {
        int n = slots.Count;
        var result = Enumerable.Repeat(-1, n).ToList();
        var pool = dias.ToList();

        var used = new bool[n];
        var orbits = new List<List<int>>();
        for (int i = 0; i < n; i++)
        {
            if (used[i]) continue;
            var orbit = new List<int>();
            foreach (var (uu, vv) in new[]
                     {
                         (slots[i].u, slots[i].v),
                         (1 - slots[i].u, slots[i].v),
                         (slots[i].u, 1 - slots[i].v),
                         (1 - slots[i].u, 1 - slots[i].v)
                     })
            {
                int j = FindSlot(slots, uu, vv, used, orbit);
                if (j >= 0 && !orbit.Contains(j))
                    orbit.Add(j);
            }
            foreach (var j in orbit) used[j] = true;
            if (orbit.Count > 0) orbits.Add(orbit);
        }

        orbits = orbits
            .OrderByDescending(o => o.Count(i => IsCorner(slots[i])))
            .ThenByDescending(o => o.Count)
            .ToList();

        foreach (var orbit in orbits)
            AssignOrbit(orbit, slots, pool, result);

        for (int i = 0; i < n; i++)
            if (result[i] < 0)
                result[i] = pool.Count > 0 ? TakeOne(pool, pool.Max()) : 16;

        return result;
    }

    private static void AssignOrbit(List<int> orbit, List<(double u, double v)> slots,
        List<int> pool, List<int> result)
    {
        int need = orbit.Count;
        // Full orbit same φ when enough bars of one dia
        int diaFull = LargestWithAtLeast(pool, need);
        if (diaFull > 0)
        {
            foreach (var i in orbit)
            {
                result[i] = diaFull;
                pool.Remove(diaFull);
            }
            return;
        }

        // Split into 180° opposite pairs so symmetry still holds (e.g. 20:2 at opposite corners)
        var pairs = OppositePairs(orbit, slots);
        foreach (var (a, b) in pairs)
        {
            int dia = LargestWithAtLeast(pool, b >= 0 ? 2 : 1);
            if (dia <= 0) dia = pool.Count > 0 ? pool.Max() : 16;
            result[a] = dia;
            if (pool.Contains(dia)) pool.Remove(dia);
            if (b >= 0)
            {
                result[b] = dia;
                if (pool.Contains(dia)) pool.Remove(dia);
                else if (pool.Count > 0) { result[b] = pool[0]; pool.RemoveAt(0); }
            }
        }
    }

    private static List<(int a, int b)> OppositePairs(List<int> orbit, List<(double u, double v)> slots)
    {
        var pairs = new List<(int, int)>();
        var left = orbit.ToList();
        while (left.Count > 0)
        {
            int a = left[0];
            left.RemoveAt(0);
            int b = -1;
            double bu = 1 - slots[a].u, bv = 1 - slots[a].v;
            for (int i = 0; i < left.Count; i++)
            {
                if (Math.Abs(slots[left[i]].u - bu) < 0.06 && Math.Abs(slots[left[i]].v - bv) < 0.06)
                {
                    b = left[i];
                    left.RemoveAt(i);
                    break;
                }
            }
            pairs.Add((a, b));
        }
        return pairs;
    }

    private static bool IsCorner((double u, double v) p) =>
        (p.u < 0.05 || p.u > 0.95) && (p.v < 0.05 || p.v > 0.95);

    private static int FindSlot(List<(double u, double v)> slots, double u, double v, bool[] used, List<int> currentOrbit)
    {
        const double tol = 0.05;
        int best = -1;
        double bestD = double.MaxValue;
        for (int i = 0; i < slots.Count; i++)
        {
            if (used[i] && !currentOrbit.Contains(i)) continue;
            double d = Math.Abs(slots[i].u - u) + Math.Abs(slots[i].v - v);
            if (d < bestD) { bestD = d; best = i; }
        }
        return bestD <= tol * 2 ? best : -1;
    }

    private static int LargestWithAtLeast(List<int> pool, int need)
    {
        if (pool.Count == 0 || need <= 0) return -1;
        foreach (var g in pool.GroupBy(d => d).OrderByDescending(g => g.Key))
            if (g.Count() >= need) return g.Key;
        return -1;
    }

    private static int TakeOne(List<int> pool, int prefer)
    {
        if (pool.Remove(prefer)) return prefer;
        if (pool.Count == 0) return prefer;
        int d = pool[0];
        pool.RemoveAt(0);
        return d;
    }
}
