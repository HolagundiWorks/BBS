// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

namespace BBSApp.Services;

/// <summary>Combined wall unit + thickness; auto block size for ACC / cement blocks.</summary>
public static class MasonryWallBuild
{
    public sealed record Option(string Label, string UnitType, string ThicknessMm, string? BlockSize);

    public static IReadOnlyList<Option> Catalog { get; } = new Option[]
    {
        new("Brick · 230 mm", "Brick", "230", null),
        new("Brick · 110 mm", "Brick", "110", null),
        new("ACC · 100 mm", "ACC Block", "100", "600x200x100"),
        new("ACC · 150 mm", "ACC Block", "150", "600x200x150"),
        new("ACC · 200 mm", "ACC Block", "200", "600x200x200"),
        new("Cement block · 100 mm", "Cement Block", "100", "600x200x100"),
        new("Cement block · 150 mm", "Cement Block", "150", "600x200x150"),
        new("Cement block · 200 mm", "Cement Block", "200", "600x200x200"),
    };

    public static string[] Labels => Catalog.Select(o => o.Label).ToArray();

    public static string DefaultLabel => Catalog[0].Label;

    public static void Apply(Dictionary<string, string> row, string wallBuild)
    {
        var opt = Find(wallBuild) ?? Catalog[0];
        row["wall_build"] = opt.Label;
        row["unit_type"] = opt.UnitType;
        row["thickness"] = opt.ThicknessMm;
        if (string.IsNullOrEmpty(opt.BlockSize))
            row.Remove("block_size");
        else
            row["block_size"] = opt.BlockSize;
    }

    public static string FromRow(Dictionary<string, string> row)
    {
        string unit = row.TryGetValue("unit_type", out var u) ? u.Trim() : "Brick";
        string thick = row.TryGetValue("thickness", out var t) ? t.Trim() : "230";
        if (row.TryGetValue("wall_build", out var wb) && !string.IsNullOrWhiteSpace(wb)
            && Find(wb) is not null)
            return wb.Trim();

        foreach (var o in Catalog)
        {
            if (o.UnitType.Equals(unit, StringComparison.OrdinalIgnoreCase)
                && o.ThicknessMm.Equals(thick, StringComparison.OrdinalIgnoreCase))
                return o.Label;
        }

        // Nearest thickness match for same unit family
        foreach (var o in Catalog)
        {
            if (o.UnitType.Equals(unit, StringComparison.OrdinalIgnoreCase))
                return o.Label;
        }
        return DefaultLabel;
    }

    public static void EnsureWallBuild(Dictionary<string, string> row)
    {
        string label = FromRow(row);
        Apply(row, label);
    }

    public static Option? Find(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;
        return Catalog.FirstOrDefault(o => o.Label.Equals(label.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
