using System.Collections.ObjectModel;

namespace BBSApp.Services;

/// <summary>Builds a first-cut construction schedule from the project's levels and BOQ elements.</summary>
public static class ScheduleSeeder
{
    /// <summary>Produce an ordered, FS-linked activity list. Caller decides replace vs. append.</summary>
    public static List<ScheduleActivity> Build(ProjectStore store)
    {
        var list = new List<ScheduleActivity>();
        ScheduleActivity? prev = null;

        ScheduleActivity Add(string name, double dur, string wbs, ScheduleActivity? after)
        {
            var a = new ScheduleActivity { Name = name, DurationDays = Math.Max(0, Math.Round(dur)), Wbs = wbs };
            if (after is not null)
                a.Links.Add(new ActivityLink { PredecessorId = after.Id, Type = DependencyType.FS });
            list.Add(a);
            return a;
        }

        prev = Add("Site mobilization & setup", 3, "Preliminaries", null);

        var levels = store.Levels.Count > 0 ? store.Levels.ToList() : new List<LevelDef>();
        for (int i = 0; i < levels.Count; i++)
        {
            var lv = levels[i];
            string id = lv.Id;
            string wbs = string.IsNullOrWhiteSpace(lv.Name) ? id : $"{id} · {lv.Name}";
            ScheduleActivity levelAnchor = prev!;
            ScheduleActivity chain = prev!;

            int excav = CountAt(store.Earthwork, id);
            int footings = CountAt(store.Footings, id);
            int pcc = CountAt(store.PccBeds, id);
            int cols = CountAt(store.Columns, id) + CountAt(store.Pedestals, id);
            int beams = CountAt(store.Beams, id);
            int slabs = CountAt(store.Slabs, id);
            int masonry = CountAt(store.MasonryWalls, id);
            int plaster = CountAt(store.Plaster, id) + CountAt(store.FinishPropose, id);
            int flooring = CountAt(store.Flooring, id);

            if (i == 0 && (excav > 0 || footings > 0))
                chain = Add("Excavation & earthwork", 2 + excav + footings * 0.5, wbs, chain);
            if (footings > 0)
                chain = Add($"{Short(lv)} — footings / foundation", 3 + footings, wbs, chain);
            if (pcc > 0)
                chain = Add($"{Short(lv)} — PCC bed", 1 + pcc, wbs, chain);
            if (cols > 0)
                chain = Add($"{Short(lv)} — columns", 3 + cols, wbs, chain);
            if (beams + slabs > 0)
                chain = Add($"{Short(lv)} — beams & slab", 4 + beams + slabs, wbs, chain);
            if (masonry > 0)
                chain = Add($"{Short(lv)} — masonry", 4 + (int)Math.Round(masonry * 1.5), wbs, chain);
            if (plaster > 0)
                chain = Add($"{Short(lv)} — plastering", 3 + plaster, wbs, chain);
            if (flooring > 0)
                chain = Add($"{Short(lv)} — flooring / finishes", 3 + flooring * 2, wbs, chain);

            // Next level starts after this level's structure (or its last activity if none added).
            prev = chain == levelAnchor ? levelAnchor : chain;
        }

        string post = "Finishing & handover";
        int doors = store.Doors.Count, windows = store.Windows.Count;
        if (doors + windows > 0)
            prev = Add("Doors & windows", 2 + doors + windows, post, prev);
        if (store.Waterproofing.Count > 0)
            prev = Add("Waterproofing", 2 + store.Waterproofing.Count, post, prev);
        if (store.Painting.Count > 0 || store.Plaster.Count > 0)
            prev = Add("Painting", 4, post, prev);
        if (store.PlinthProtection.Count > 0)
            prev = Add("External / plinth protection", 3, post, prev);
        Add("Snagging & handover", 2, post, prev);

        return list;
    }

    private static string Short(LevelDef lv) =>
        string.IsNullOrWhiteSpace(lv.Name) ? lv.Id : lv.Name;

    private static int CountAt(ObservableCollection<Dictionary<string, string>> rows, string levelId)
    {
        int n = 0;
        foreach (var r in rows)
            if (r.TryGetValue("level", out var lv) && lv.Equals(levelId, StringComparison.OrdinalIgnoreCase))
                n++;
        return n;
    }
}
