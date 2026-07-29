// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

namespace BBSApp.Models;

/// <summary>Door / window option lists and stable rate-book item codes.</summary>
public static class DoorWindowCatalog
{
    public static readonly string[] DoorTypes = { "MS door", "Wood door" };

    public static readonly string[] WoodFrames =
    {
        "110×150", "110×125", "110×250", "110×200"
    };

    public static readonly string[] ShutterThicknesses = { "32 mm", "50 mm" };

    /// <summary>Display name → code abbreviation.</summary>
    public static readonly (string Name, string Abbr)[] ShutterTypes =
    {
        ("Block Board", "BB"),
        ("Solid Wood", "SW"),
        ("Wood Teak Class-1", "T1"),
        ("Wood Teak Class-2", "T2"),
        ("Wood Teak Class-3", "T3"),
        ("Prelam board", "PL"),
        ("WPC", "WPC"),
    };

    public static string[] ShutterTypeNames => ShutterTypes.Select(t => t.Name).ToArray();

    public static readonly string[] WindowSystems =
    {
        "System Aluminium", "UPVC", "Wooden"
    };

    public static readonly string[] Tracks = { "2.5 Track", "3 Track" };

    public static readonly string[] WoodOpenings =
    {
        "Single shutter — open outside",
        "Double shutter — open inside",
        "Double shutter — open outside"
    };

    public static string[] WoodFinishes => FinishCatalog.WoodFinishes;

    public static string FrameCode(string frame) =>
        frame.Replace("×", "x", StringComparison.Ordinal)
             .Replace(" ", "", StringComparison.Ordinal);

    public static string ThickCode(string thick) =>
        thick.Replace(" mm", "", StringComparison.OrdinalIgnoreCase).Trim();

    public static string ShutterAbbr(string type)
    {
        foreach (var (name, abbr) in ShutterTypes)
            if (name.Equals(type, StringComparison.OrdinalIgnoreCase)) return abbr;
        return "XX";
    }

    public static string WoodDoorCode(string frame, string thick, string shutterType) =>
        $"DR-WD-{FrameCode(frame)}-{ThickCode(thick)}-{ShutterAbbr(shutterType)}";

    public static string MsDoorCode() => "DR-MS";

    public static string DoorItemCode(string doorType, string frame, string thick, string shutterType)
    {
        if (doorType.Equals("MS door", StringComparison.OrdinalIgnoreCase))
            return MsDoorCode();
        return WoodDoorCode(frame, thick, shutterType);
    }

    public static string WindowCode(string system, string track, string woodOpening)
    {
        if (system.Equals("System Aluminium", StringComparison.OrdinalIgnoreCase))
            return track.StartsWith("3", StringComparison.Ordinal) ? "WN-AL-3T" : "WN-AL-2.5T";
        if (system.Equals("UPVC", StringComparison.OrdinalIgnoreCase))
            return track.StartsWith("3", StringComparison.Ordinal) ? "WN-UPVC-3T" : "WN-UPVC-2.5T";

        // Wooden
        if (woodOpening.Contains("Single", StringComparison.OrdinalIgnoreCase))
            return "WN-WD-SS-OO";
        if (woodOpening.Contains("open inside", StringComparison.OrdinalIgnoreCase))
            return "WN-WD-DS-OI";
        return "WN-WD-DS-OO";
    }

    public static string DoorDescription(string doorType, string frame, string thick, string shutterType, string woodFinish = "")
    {
        string baseDesc = doorType.Equals("MS door", StringComparison.OrdinalIgnoreCase)
            ? "MS door"
            : $"Wood door · frame {frame} · shutter {thick} · {shutterType}";
        if (!string.IsNullOrWhiteSpace(woodFinish)
            && !woodFinish.StartsWith("None", StringComparison.OrdinalIgnoreCase))
            baseDesc += $" · {woodFinish}";
        return baseDesc;
    }

    public static string WindowDescription(string system, string track, string woodOpening, string woodFinish = "")
    {
        string baseDesc = system.Equals("Wooden", StringComparison.OrdinalIgnoreCase)
            ? $"Wooden window · {woodOpening}"
            : $"{system} window · {track}";
        if (system.Equals("Wooden", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(woodFinish)
            && !woodFinish.StartsWith("None", StringComparison.OrdinalIgnoreCase))
            baseDesc += $" · {woodFinish}";
        return baseDesc;
    }
}
