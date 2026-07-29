// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

namespace BBSApp.Models;

/// <summary>Floor / wall tiles and paint / wood-finish catalogs with rate-book codes.</summary>
public static class FinishCatalog
{
    public static readonly string[] SurfaceKinds = { "Floor", "Wall" };

    public static readonly string[] FloorFinishTypes =
    {
        "Vitrified tiles",
        "Granite",
        "Marble",
        "Kota",
        "Ceramic tiles",
        "IPS",
        "Other"
    };

    public static readonly string[] WallFinishTypes =
    {
        "Ceramic tiles",
        "Vitrified tiles",
        "Granite",
        "Marble",
        "Other"
    };

    /// <summary>Shared tile sizes for floor and wall (user can add same sizes on walls).</summary>
    public static readonly string[] TileSizes =
    {
        "300×600",
        "600×600",
        "600×1200",
        "800×1200",
        "800×2100",
        "1200×2400",
        "Other / mixed"
    };

    public static readonly string[] PaintLocations =
    {
        "Inside walls",
        "Outside walls",
        "Ceiling",
        "Other"
    };

    public static readonly string[] PaintTypes =
    {
        "Emulsion",
        "Distemper",
        "Exterior emulsion",
        "Enamel",
        "Other"
    };

    /// <summary>Standard wall paint build-up (primer / putty / finish).</summary>
    public static readonly string[] PaintSystems =
    {
        "2 coat primer + 3 coat putty + 2 coat paint",
        "1 coat primer + 2 coat putty + 2 coat paint",
        "2 coat paint only",
        "Primer only",
        "Other"
    };

    public static readonly string[] WoodFinishes =
    {
        "Varnish",
        "Polish",
        "Paint (enamel)",
        "Melamine",
        "None / unfinished"
    };

    public static bool NeedsTileSize(string finishType) =>
        finishType.Contains("tile", StringComparison.OrdinalIgnoreCase)
        || finishType.Contains("vitrified", StringComparison.OrdinalIgnoreCase)
        || finishType.Contains("ceramic", StringComparison.OrdinalIgnoreCase);

    public static string FloorItemCode(string surface, string finish, string size)
    {
        string surf = surface.Equals("Wall", StringComparison.OrdinalIgnoreCase) ? "WT" : "FL";
        string fin = finish.ToLowerInvariant() switch
        {
            var s when s.Contains("vitrified") => "VIT",
            var s when s.Contains("ceramic") => "CER",
            var s when s.Contains("granite") => "GRN",
            var s when s.Contains("marble") => "MRB",
            var s when s.Contains("kota") => "KOTA",
            var s when s.Contains("ips") => "IPS",
            _ => "OTH"
        };
        if (!NeedsTileSize(finish))
            return $"{surf}-{fin}";
        string sz = size.Replace("×", "x", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal)
            .Replace("/", "-", StringComparison.Ordinal);
        if (sz.Length > 18) sz = sz[..18];
        return $"{surf}-{fin}-{sz}";
    }

    public static string FloorDescription(string surface, string finish, string size)
    {
        string kind = surface.Equals("Wall", StringComparison.OrdinalIgnoreCase) ? "Wall tiles" : "Flooring";
        if (NeedsTileSize(finish) && !string.IsNullOrWhiteSpace(size))
            return $"{kind} · {finish} · {size}";
        return $"{kind} · {finish}";
    }

    public static string PaintItemCode(string location, string paintType, string system)
    {
        string loc = location.ToLowerInvariant() switch
        {
            var s when s.Contains("outside") || s.Contains("exterior") => "EXT",
            var s when s.Contains("ceiling") => "CLG",
            _ => "INT"
        };
        string pt = paintType.ToLowerInvariant() switch
        {
            var s when s.Contains("distemper") => "DIST",
            var s when s.Contains("exterior") => "EXTEM",
            var s when s.Contains("enamel") => "ENL",
            var s when s.Contains("emulsion") => "EMUL",
            _ => "PNT"
        };
        string sys = system.StartsWith("2 coat primer", StringComparison.OrdinalIgnoreCase) ? "P2U3F2"
            : system.StartsWith("1 coat primer", StringComparison.OrdinalIgnoreCase) ? "P1U2F2"
            : system.Contains("paint only", StringComparison.OrdinalIgnoreCase) ? "F2"
            : system.Contains("Primer only", StringComparison.OrdinalIgnoreCase) ? "PR"
            : "OTH";
        return $"PT-{loc}-{pt}-{sys}";
    }

    public static string PaintDescription(string location, string paintType, string system) =>
        $"Painting · {location} · {paintType} · {system}";

    public static string WoodFinishCode(string finish) => finish.ToLowerInvariant() switch
    {
        var s when s.Contains("varnish") => "WF-VARN",
        var s when s.Contains("polish") => "WF-POL",
        var s when s.Contains("paint") || s.Contains("enamel") => "WF-PNT",
        var s when s.Contains("melamine") => "WF-MEL",
        _ => "WF-NONE"
    };
}
