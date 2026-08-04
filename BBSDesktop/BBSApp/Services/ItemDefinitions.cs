// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using System.Globalization;

namespace BBSApp.Services;

/// <summary>
/// Item "master" definition — the measurement dimensions and material recipe behind each work item.
/// These values drive the two mappings: the CALCULATION RECIPE + measurement columns (L/B/H/Area/Volume)
/// say how a quantity is measured and how sub-items are extracted from it (e.g. wall area → plaster);
/// the MATERIAL columns say what the item resolves into (the composition recipe).
/// </summary>
public static class ItemDefinitions
{
    public sealed record DefRow(
        string Item, string Uom,
        string Length, string Breadth, string Height, string Area, string Volume,
        string Material1, string Material2, string Material3, string Recipe);

    public static IReadOnlyList<DefRow> Build()
    {
        var rules = ProjectStore.Current.LinkRules.Rules;

        var materialsByTrade = SchemaExport.ItemMaterialEdges()
            .GroupBy(e => e.Trade, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.MaterialName).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var rows = new List<DefRow>();
        foreach (var t in LinkTradeRegistry.All)
        {
            var derivedFrom = rules
                .Where(r => r.TargetTrade.Equals(t.Key, StringComparison.OrdinalIgnoreCase))
                .ToList();
            bool isSource = rules.Any(r => r.SourceTrade.Equals(t.Key, StringComparison.OrdinalIgnoreCase));
            var mats = materialsByTrade.TryGetValue(t.Key, out var m) ? m : new List<string>();

            // Only real items — those that are measured into sub-items or resolve to materials.
            if (derivedFrom.Count == 0 && !isSource && mats.Count == 0) continue;

            // How this item is measured: a derived item follows its rule's basis; a base item follows its unit.
            LinkBasis measure = derivedFrom.Count > 0 ? derivedFrom[0].Basis : UnitBasis(t.Unit);

            bool vol = measure == LinkBasis.Volume;
            bool area = measure == LinkBasis.Area;
            bool len = measure == LinkBasis.Length;
            bool peri = measure == LinkBasis.Perimeter;

            string Chk(bool on) => on ? "✓" : "";

            string recipe = derivedFrom.Count > 0
                ? string.Join("  +  ", derivedFrom.Select(r =>
                    $"{Num(r.Factor)}× {LinkTradeRegistry.Display(r.SourceTrade)} {LinkBasisInfo.Label(r.Basis).ToLowerInvariant()}"))
                : measure switch
                {
                    LinkBasis.Volume => "L × B × H",
                    LinkBasis.Area => "L × B",
                    LinkBasis.Length => "L",
                    LinkBasis.Perimeter => "2 × (L + B)",
                    _ => "count (nos)"
                };

            rows.Add(new DefRow(
                t.Display, t.Unit,
                Chk(vol || area || len || peri),  // Length
                Chk(vol || area || peri),          // Breadth
                Chk(vol),                          // Height
                Chk(area),                         // Area
                Chk(vol),                          // Volume
                mats.Count > 0 ? mats[0] : "",
                mats.Count > 1 ? mats[1] : "",
                mats.Count > 2 ? (mats.Count > 3 ? $"{mats[2]} +{mats.Count - 3}" : mats[2]) : "",
                recipe));
        }
        return rows;
    }

    private static LinkBasis UnitBasis(string unit) => unit switch
    {
        "m³" => LinkBasis.Volume,
        "m²" => LinkBasis.Area,
        "m" => LinkBasis.Length,
        _ => LinkBasis.Count
    };

    private static string Num(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
