// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using System.Globalization;

namespace BBSApp.Services;

/// <summary>
/// Item-centric view of the estimation relationships (not the DB schema): each work item with its
/// unit, rate, and what feeds it (INPUTS — source items it derives from + materials it consumes) and
/// what it produces (OUTPUTS — items derived from it). Drives the Data model page's item table.
/// </summary>
public static class ItemRelations
{
    public sealed record ItemRow(string Item, string Uom, string Rate, string Inputs, string Outputs);

    public static IReadOnlyList<ItemRow> Build()
    {
        var store = ProjectStore.Current;
        var rules = store.LinkRules.Rules;

        RateBookStore.Current.EnsureLoaded();
        var version = RateBookStore.Current.ActiveOrFirst();
        var rateIndex = version is null
            ? new Dictionary<string, RateItem>(StringComparer.OrdinalIgnoreCase)
            : RateBookStore.Current.IndexByCode(version);

        // material inputs grouped by trade
        var materialsByTrade = SchemaExport.ItemMaterialEdges()
            .GroupBy(e => e.Trade, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.MaterialName).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var rows = new List<ItemRow>();
        foreach (var t in LinkTradeRegistry.All)
        {
            var derivInputs = rules
                .Where(r => r.TargetTrade.Equals(t.Key, StringComparison.OrdinalIgnoreCase))
                .Select(r => Edge(LinkTradeRegistry.Display(r.SourceTrade), r))
                .ToList();
            var matInputs = materialsByTrade.TryGetValue(t.Key, out var m) ? m : new List<string>();
            var inputs = derivInputs.Concat(matInputs).ToList();

            var outputs = rules
                .Where(r => r.SourceTrade.Equals(t.Key, StringComparison.OrdinalIgnoreCase))
                .Select(r => Edge(LinkTradeRegistry.Display(r.TargetTrade), r))
                .ToList();

            // "Only related values" — skip items with no inputs and no outputs.
            if (inputs.Count == 0 && outputs.Count == 0) continue;

            string code = EstimateCalculator.CodeForElement(t.Key, t.Unit);
            string rate = rateIndex.TryGetValue(code, out var ri) && ri.Rate > 0
                ? "₹" + ri.Rate.ToString("0.##", CultureInfo.InvariantCulture)
                : "—";

            rows.Add(new ItemRow(
                t.Display,
                t.Unit,
                rate,
                inputs.Count > 0 ? string.Join(", ", inputs) : "—",
                outputs.Count > 0 ? string.Join(", ", outputs) : "—"));
        }
        return rows;
    }

    private static string Edge(string other, LinkRule r)
    {
        string factor = "×" + r.Factor.ToString("0.###", CultureInfo.InvariantCulture);
        string off = r.Enabled ? "" : " (off)";
        return $"{other} {factor}{off}";
    }
}
