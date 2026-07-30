// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

namespace BBSApp.Services;

/// <summary>One computed linked-item line produced by a <see cref="LinkRule"/>.</summary>
public sealed class DerivedItem
{
    public string RuleId { get; set; } = "";
    public string RuleName { get; set; } = "";
    public string SourceTrade { get; set; } = "";
    public string SourceMark { get; set; } = "";
    public string Level { get; set; } = "";
    public LinkBasis Basis { get; set; }
    public double SourceQty { get; set; }
    public double Factor { get; set; }
    public string TargetTrade { get; set; } = "";
    public double TargetQty { get; set; }
    public string TargetUnit { get; set; } = "";
    /// <summary>True when this line's source was itself produced by an upstream rule (a chained link).</summary>
    public bool Chained { get; set; }
}

/// <summary>
/// Data-driven derivation of linked estimation quantities from a <see cref="LinkRuleBook"/>.
/// Reads source-trade quantities from <see cref="CivilBoqCalculator.BuildAll"/> (already unit-normalised),
/// orders rules so producers run before consumers, and lets outputs feed downstream rules
/// (masonry → plaster → paint). Read-only: it never mutates the take-off sheets.
/// </summary>
public static class DerivationEngine
{
    /// <summary>Accumulated measures for a single (trade, mark, level) node.</summary>
    private sealed class NodeQty
    {
        public string Mark = "";
        public string Level = "";
        public double AreaM2;
        public double VolumeM3;
        public double LengthM;
        public double BreadthM;
        public double Count;
    }

    public static IReadOnlyList<DerivedItem> Preview(
        ProjectStore store, LinkRuleBook book, IReadOnlySet<string>? levels = null)
    {
        var work = new Dictionary<string, List<NodeQty>>(StringComparer.OrdinalIgnoreCase);

        // 1. Seed the graph from the measured civil BOQ (source of truth, SI units).
        foreach (var c in CivilBoqCalculator.BuildAll(store, levels))
        {
            if (string.IsNullOrWhiteSpace(c.Element)) continue;
            var node = new NodeQty
            {
                Mark = string.IsNullOrWhiteSpace(c.Mark) ? "—" : c.Mark,
                Level = c.Level ?? "",
                AreaM2 = c.AreaM2,
                VolumeM3 = c.VolumeM3,
                LengthM = c.LengthM,
                BreadthM = c.BreadthM,
                Count = c.Unit.Contains("nos", StringComparison.OrdinalIgnoreCase) ? c.Qty : 1
            };
            AddNode(work, c.Element, node);
        }

        // 2. Run rules in producer-before-consumer order so chains resolve.
        var rules = book.Rules
            .Where(r => r.Enabled
                        && !string.IsNullOrWhiteSpace(r.SourceTrade)
                        && !string.IsNullOrWhiteSpace(r.TargetTrade))
            .ToList();
        var ordered = TopoOrder(rules);

        var seeded = new HashSet<string>(work.Keys, StringComparer.OrdinalIgnoreCase);
        var results = new List<DerivedItem>();

        foreach (var rule in ordered)
        {
            if (!work.TryGetValue(rule.SourceTrade, out var sources) || sources.Count == 0)
                continue;
            bool chained = !seeded.Contains(rule.SourceTrade);
            var produced = new List<NodeQty>();

            if (rule.PerItem)
            {
                foreach (var src in sources)
                {
                    double drive = Drive(src, rule.Basis);
                    if (drive <= 0) continue;
                    double tq = drive * rule.Factor;
                    results.Add(Make(rule, src.Mark, src.Level, drive, tq, chained));
                    produced.Add(TargetNode(rule, src.Mark, src.Level, tq));
                }
            }
            else
            {
                double drive = sources.Sum(s => Drive(s, rule.Basis));
                if (drive <= 0) continue;
                double tq = drive * rule.Factor;
                results.Add(Make(rule, "ALL", "", drive, tq, chained));
                produced.Add(TargetNode(rule, "ALL", "", tq));
            }

            foreach (var p in produced)
                AddNode(work, rule.TargetTrade, p);
        }

        return results;
    }

    /// <summary>Roll a derivation preview up to one total per target trade.</summary>
    public static IReadOnlyList<(string Trade, string Unit, double Qty, int Lines)> Totals(
        IReadOnlyList<DerivedItem> items)
    {
        return items
            .GroupBy(i => i.TargetTrade, StringComparer.OrdinalIgnoreCase)
            .Select(g => (
                Trade: LinkTradeRegistry.Display(g.Key),
                Unit: g.First().TargetUnit,
                Qty: g.Sum(i => i.TargetQty),
                Lines: g.Count()))
            .OrderBy(t => t.Trade, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static DerivedItem Make(
        LinkRule rule, string mark, string level, double drive, double tq, bool chained) => new()
    {
        RuleId = rule.Id,
        RuleName = rule.Name,
        SourceTrade = LinkTradeRegistry.Display(rule.SourceTrade),
        SourceMark = mark,
        Level = level,
        Basis = rule.Basis,
        SourceQty = Math.Round(drive, 3),
        Factor = rule.Factor,
        TargetTrade = LinkTradeRegistry.Display(rule.TargetTrade),
        TargetQty = Math.Round(tq, 3),
        TargetUnit = string.IsNullOrWhiteSpace(rule.TargetUnit)
            ? LinkTradeRegistry.Unit(rule.TargetTrade)
            : rule.TargetUnit,
        Chained = chained
    };

    /// <summary>Store a produced quantity under the target trade so downstream rules can read it.</summary>
    private static NodeQty TargetNode(LinkRule rule, string mark, string level, double tq)
    {
        var node = new NodeQty { Mark = mark, Level = level, Count = 1 };
        string unit = LinkTradeRegistry.Unit(rule.TargetTrade);
        if (unit.Contains("m³", StringComparison.OrdinalIgnoreCase) || unit.Contains("m3", StringComparison.OrdinalIgnoreCase))
            node.VolumeM3 = tq;
        else if (unit == "m")
            node.LengthM = tq;
        else if (unit.Contains("nos", StringComparison.OrdinalIgnoreCase))
            node.Count = tq;
        else
            node.AreaM2 = tq; // m² / default
        return node;
    }

    private static void AddNode(Dictionary<string, List<NodeQty>> work, string trade, NodeQty node)
    {
        if (!work.TryGetValue(trade, out var list))
        {
            list = new List<NodeQty>();
            work[trade] = list;
        }
        list.Add(node);
    }

    private static double Drive(NodeQty n, LinkBasis basis) => basis switch
    {
        LinkBasis.Area => n.AreaM2,
        LinkBasis.Volume => n.VolumeM3,
        LinkBasis.Length => n.LengthM,
        LinkBasis.Perimeter => n.LengthM > 0 && n.BreadthM > 0 ? 2 * (n.LengthM + n.BreadthM) : 0,
        LinkBasis.Count => n.Count > 0 ? n.Count : 1,
        _ => 0
    };

    /// <summary>Kahn topological sort of trades on source→target edges; rules ordered by source rank.</summary>
    private static List<LinkRule> TopoOrder(List<LinkRule> rules)
    {
        var trades = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rules) { trades.Add(r.SourceTrade); trades.Add(r.TargetTrade); }

        var indeg = trades.ToDictionary(t => t, _ => 0, StringComparer.OrdinalIgnoreCase);
        var adj = trades.ToDictionary(t => t, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var r in rules)
        {
            if (r.SourceTrade.Equals(r.TargetTrade, StringComparison.OrdinalIgnoreCase)) continue; // self-loop
            adj[r.SourceTrade].Add(r.TargetTrade);
            indeg[r.TargetTrade]++;
        }

        var rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(indeg.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        int order = 0;
        while (queue.Count > 0)
        {
            var t = queue.Dequeue();
            rank[t] = order++;
            foreach (var next in adj[t])
                if (--indeg[next] == 0) queue.Enqueue(next);
        }
        // Any trade left in a cycle ranks last (stable, arbitrary among themselves).
        foreach (var t in trades)
            if (!rank.ContainsKey(t)) rank[t] = order++;

        return rules
            .OrderBy(r => rank.TryGetValue(r.SourceTrade, out var k) ? k : int.MaxValue)
            .ToList();
    }
}
