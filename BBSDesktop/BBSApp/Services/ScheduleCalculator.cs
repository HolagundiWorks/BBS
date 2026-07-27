namespace BBSApp.Services;

public sealed class ScheduleResult
{
    public double ProjectDurationDays { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime FinishDate { get; set; }
    public int ActivityCount { get; set; }
    public int CriticalCount { get; set; }
    public bool HasCycle { get; set; }
    public List<string> CycleIds { get; } = new();
}

/// <summary>Critical Path Method engine (PDM: FS/SS/FF/SF with lag). Pure C#.</summary>
public static class ScheduleCalculator
{
    private const double Eps = 1e-6;

    /// <summary>Computes ES/EF/LS/LF, total float, critical flags and network ranks in place.</summary>
    public static ScheduleResult Compute(ProjectSchedule schedule)
    {
        var result = new ScheduleResult { StartDate = schedule.StartDate };
        var acts = schedule.Activities;
        result.ActivityCount = acts.Count;

        // Reset
        foreach (var a in acts)
        {
            a.EarlyStart = a.EarlyFinish = a.LateStart = a.LateFinish = 0;
            a.TotalFloat = 0;
            a.IsCritical = false;
            a.InCycle = false;
        }
        if (acts.Count == 0)
        {
            result.FinishDate = schedule.StartDate;
            return result;
        }

        var byId = new Dictionary<string, ScheduleActivity>();
        foreach (var a in acts) byId[a.Id] = a;

        // Successor map: predecessorId -> [(successor, link)]
        var successors = new Dictionary<string, List<(ScheduleActivity Succ, ActivityLink Link)>>();
        var indegree = new Dictionary<string, int>();
        foreach (var a in acts) indegree[a.Id] = 0;
        foreach (var a in acts)
        {
            foreach (var l in a.Links)
            {
                if (string.IsNullOrEmpty(l.PredecessorId) || !byId.ContainsKey(l.PredecessorId)) continue;
                if (l.PredecessorId == a.Id) continue; // ignore self-links
                if (!successors.TryGetValue(l.PredecessorId, out var list))
                    successors[l.PredecessorId] = list = new();
                list.Add((a, l));
                indegree[a.Id]++;
            }
        }

        // Kahn topological sort; leftovers are in a cycle.
        var topo = new List<ScheduleActivity>();
        var queue = new Queue<ScheduleActivity>(acts.Where(a => indegree[a.Id] == 0));
        var deg = new Dictionary<string, int>(indegree);
        while (queue.Count > 0)
        {
            var a = queue.Dequeue();
            topo.Add(a);
            if (successors.TryGetValue(a.Id, out var succs))
                foreach (var (s, _) in succs)
                    if (--deg[s.Id] == 0) queue.Enqueue(s);
        }

        if (topo.Count < acts.Count)
        {
            result.HasCycle = true;
            var sorted = new HashSet<string>(topo.Select(a => a.Id));
            foreach (var a in acts)
                if (!sorted.Contains(a.Id))
                {
                    a.InCycle = true;
                    result.CycleIds.Add(a.Id);
                }
        }

        // Forward pass (topological order) over the acyclic set.
        foreach (var a in topo)
        {
            double es = 0;
            foreach (var l in a.Links)
            {
                if (!byId.TryGetValue(l.PredecessorId, out var p) || p.InCycle) continue;
                double c = l.Type switch
                {
                    DependencyType.FS => p.EarlyFinish + l.LagDays,
                    DependencyType.SS => p.EarlyStart + l.LagDays,
                    DependencyType.FF => p.EarlyFinish + l.LagDays - a.DurationDays,
                    DependencyType.SF => p.EarlyStart + l.LagDays - a.DurationDays,
                    _ => 0
                };
                if (c > es) es = c;
            }
            a.EarlyStart = Math.Max(0, es);
            a.EarlyFinish = a.EarlyStart + Math.Max(0, a.DurationDays);
        }

        double projectFinish = topo.Count == 0 ? 0 : topo.Max(a => a.EarlyFinish);
        result.ProjectDurationDays = projectFinish;

        // Backward pass (reverse topological order).
        for (int i = topo.Count - 1; i >= 0; i--)
        {
            var a = topo[i];
            double lf = projectFinish;
            bool hasSucc = false;
            if (successors.TryGetValue(a.Id, out var succs))
            {
                foreach (var (s, l) in succs)
                {
                    if (s.InCycle) continue;
                    hasSucc = true;
                    double c = l.Type switch
                    {
                        DependencyType.FS => s.LateStart - l.LagDays,
                        DependencyType.SS => s.LateStart - l.LagDays + a.DurationDays,
                        DependencyType.FF => s.LateFinish - l.LagDays,
                        DependencyType.SF => s.LateFinish - l.LagDays + a.DurationDays,
                        _ => projectFinish
                    };
                    if (c < lf) lf = c;
                }
            }
            if (!hasSucc) lf = projectFinish;
            a.LateFinish = lf;
            a.LateStart = a.LateFinish - Math.Max(0, a.DurationDays);
            a.TotalFloat = a.LateStart - a.EarlyStart;
            a.IsCritical = !a.InCycle && Math.Abs(a.TotalFloat) < 1e-3;
        }

        result.CriticalCount = acts.Count(a => a.IsCritical);
        result.FinishDate = schedule.DateForOffset(projectFinish);
        return result;
    }

    /// <summary>Topological "rank" (longest predecessor chain) per activity — used to auto-lay-out the network.</summary>
    public static Dictionary<string, int> Ranks(ProjectSchedule schedule)
    {
        var byId = schedule.Activities.ToDictionary(a => a.Id, a => a);
        var rank = new Dictionary<string, int>();
        int Visit(ScheduleActivity a, HashSet<string> stack)
        {
            if (rank.TryGetValue(a.Id, out var r)) return r;
            if (!stack.Add(a.Id)) return 0; // cycle guard
            int best = 0;
            foreach (var l in a.Links)
                if (byId.TryGetValue(l.PredecessorId, out var p) && p.Id != a.Id)
                    best = Math.Max(best, Visit(p, stack) + 1);
            stack.Remove(a.Id);
            return rank[a.Id] = best;
        }
        foreach (var a in schedule.Activities) Visit(a, new HashSet<string>());
        return rank;
    }
}
