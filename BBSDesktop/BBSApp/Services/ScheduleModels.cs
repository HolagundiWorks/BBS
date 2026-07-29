// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using System.Globalization;
using System.Text.Json.Nodes;

namespace BBSApp.Services;

/// <summary>Precedence relationship between two activities (PDM).</summary>
public enum DependencyType { FS, SS, FF, SF }

/// <summary>A single predecessor link on an activity.</summary>
public sealed class ActivityLink
{
    public string PredecessorId { get; set; } = "";
    public DependencyType Type { get; set; } = DependencyType.FS;
    /// <summary>Lag (+) or lead (−) in working days.</summary>
    public double LagDays { get; set; }

    public JsonObject ToJson() => new()
    {
        ["pred"] = PredecessorId,
        ["type"] = Type.ToString(),
        ["lag"] = LagDays
    };

    public static ActivityLink FromJson(JsonObject o)
    {
        var link = new ActivityLink
        {
            PredecessorId = o["pred"]?.GetValue<string>() ?? "",
            LagDays = ScheduleJson.Num(o, "lag", 0)
        };
        if (Enum.TryParse<DependencyType>(o["type"]?.GetValue<string>() ?? "FS", true, out var t))
            link.Type = t;
        return link;
    }
}

/// <summary>One schedule activity/task. Durations and computed offsets are in working days.</summary>
public sealed class ScheduleActivity
{
    public string Id { get; set; } = NewId();
    public string Name { get; set; } = "New activity";
    public double DurationDays { get; set; } = 1;
    /// <summary>0–100.</summary>
    public double PercentComplete { get; set; }
    /// <summary>Optional grouping tag (e.g. level id / WBS phase).</summary>
    public string Wbs { get; set; } = "";
    public List<ActivityLink> Links { get; } = new();

    /// <summary>Network-editor position. NaN = auto-layout.</summary>
    public double X { get; set; } = double.NaN;
    public double Y { get; set; } = double.NaN;

    // ---- Computed by ScheduleCalculator (working-day offsets from project start) ----
    public double EarlyStart { get; set; }
    public double EarlyFinish { get; set; }
    public double LateStart { get; set; }
    public double LateFinish { get; set; }
    public double TotalFloat { get; set; }
    public bool IsCritical { get; set; }
    public bool InCycle { get; set; }

    public bool IsMilestone => DurationDays <= 0;

    public static string NewId() => "A" + Guid.NewGuid().ToString("N")[..6];

    public JsonObject ToJson()
    {
        var links = new JsonArray();
        foreach (var l in Links) links.Add(l.ToJson());
        return new JsonObject
        {
            ["id"] = Id,
            ["name"] = Name,
            ["duration"] = DurationDays,
            ["percent"] = PercentComplete,
            ["wbs"] = Wbs,
            ["x"] = double.IsNaN(X) ? null : X,
            ["y"] = double.IsNaN(Y) ? null : Y,
            ["links"] = links
        };
    }

    public static ScheduleActivity FromJson(JsonObject o)
    {
        var a = new ScheduleActivity
        {
            Id = o["id"]?.GetValue<string>() ?? NewId(),
            Name = o["name"]?.GetValue<string>() ?? "Activity",
            DurationDays = ScheduleJson.Num(o, "duration", 1),
            PercentComplete = Math.Clamp(ScheduleJson.Num(o, "percent", 0), 0, 100),
            Wbs = o["wbs"]?.GetValue<string>() ?? "",
            X = o["x"] is JsonValue xv && xv.TryGetValue<double>(out var x) ? x : double.NaN,
            Y = o["y"] is JsonValue yv && yv.TryGetValue<double>(out var y) ? y : double.NaN
        };
        if (o["links"] is JsonArray arr)
            foreach (var n in arr)
                if (n is JsonObject lo) a.Links.Add(ActivityLink.FromJson(lo));
        return a;
    }
}

/// <summary>Project schedule: start date, working calendar, and the activity network.</summary>
public sealed class ProjectSchedule
{
    public DateTime StartDate { get; set; } = DateTime.Today;
    /// <summary>7 = calendar days, 6 = skip Sundays, 5 = skip Sat+Sun.</summary>
    public int WorkingDaysPerWeek { get; set; } = 6;
    public List<ScheduleActivity> Activities { get; } = new();

    public ScheduleActivity? Find(string id) => Activities.FirstOrDefault(a => a.Id == id);

    /// <summary>1-based display index of an activity (for predecessor entry / labels).</summary>
    public int IndexOf(ScheduleActivity a) => Activities.IndexOf(a) + 1;

    /// <summary>Map a working-day offset to a calendar date, skipping non-working days.</summary>
    public DateTime DateForOffset(double offset)
    {
        int whole = (int)Math.Round(offset, MidpointRounding.AwayFromZero);
        return AddWorkingDays(StartDate, whole);
    }

    public DateTime AddWorkingDays(DateTime from, int workingDays)
    {
        if (WorkingDaysPerWeek >= 7) return from.AddDays(workingDays);
        var d = from;
        int step = workingDays >= 0 ? 1 : -1;
        int remaining = Math.Abs(workingDays);
        // The first counted day is the start date itself when it is a working day.
        while (remaining > 0)
        {
            d = d.AddDays(step);
            if (IsWorkingDay(d)) remaining--;
        }
        return d;
    }

    public bool IsWorkingDay(DateTime d) => WorkingDaysPerWeek switch
    {
        <= 5 => d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday,
        6 => d.DayOfWeek != DayOfWeek.Sunday,
        _ => true
    };

    public void Clear()
    {
        Activities.Clear();
        StartDate = DateTime.Today;
        WorkingDaysPerWeek = 6;
    }

    public JsonObject ToJson()
    {
        var arr = new JsonArray();
        foreach (var a in Activities) arr.Add(a.ToJson());
        return new JsonObject
        {
            ["start_date"] = StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["working_days_per_week"] = WorkingDaysPerWeek,
            ["activities"] = arr
        };
    }

    public void LoadFrom(JsonObject? o)
    {
        Clear();
        if (o is null) return;
        var sd = o["start_date"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(sd) &&
            DateTime.TryParse(sd, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            StartDate = parsed;
        WorkingDaysPerWeek = (int)ScheduleJson.Num(o, "working_days_per_week", 6);
        if (o["activities"] is JsonArray arr)
            foreach (var n in arr)
                if (n is JsonObject ao) Activities.Add(ScheduleActivity.FromJson(ao));
    }
}

internal static class ScheduleJson
{
    public static double Num(JsonObject o, string key, double def)
    {
        if (o[key] is JsonValue jv && jv.TryGetValue<double>(out var d)) return d;
        return def;
    }
}
