using System.Text.Json.Nodes;

namespace BBSApp.Services;

/// <summary>Percentage add-ons applied on estimate base (civil + materials + steel).</summary>
public sealed class EstimateMarkups
{
    /// <summary>% of base for electrical works.</summary>
    public double ElectricalPct { get; set; } = 8;

    /// <summary>% of base for plumbing / sanitary.</summary>
    public double PlumbingPct { get; set; } = 6;

    /// <summary>% escalation on (base + electrical + plumbing).</summary>
    public double EscalationPct { get; set; } = 5;

    /// <summary>% consulting / PMC fees on (base + EP + escalation).</summary>
    public double ConsultingFeePct { get; set; } = 3;

    public JsonObject ToJson() => new()
    {
        ["electrical_pct"] = ElectricalPct,
        ["plumbing_pct"] = PlumbingPct,
        ["escalation_pct"] = EscalationPct,
        ["consulting_fee_pct"] = ConsultingFeePct
    };

    public void LoadFrom(JsonObject? o)
    {
        if (o is null) return;
        ElectricalPct = Num(o, "electrical_pct", ElectricalPct);
        PlumbingPct = Num(o, "plumbing_pct", PlumbingPct);
        EscalationPct = Num(o, "escalation_pct", EscalationPct);
        ConsultingFeePct = Num(o, "consulting_fee_pct", ConsultingFeePct);
    }

    public void Reset()
    {
        ElectricalPct = 8;
        PlumbingPct = 6;
        EscalationPct = 5;
        ConsultingFeePct = 3;
    }

    private static double Num(JsonObject o, string key, double def)
    {
        try
        {
            var n = o[key];
            if (n is null) return def;
            return n.GetValue<double>();
        }
        catch { return def; }
    }
}

/// <summary>Computed markup amounts for one estimate run.</summary>
public sealed class EstimateMarkupBreakdown
{
    public double BaseTotal { get; set; }
    public double ElectricalPct { get; set; }
    public double PlumbingPct { get; set; }
    public double EscalationPct { get; set; }
    public double ConsultingFeePct { get; set; }
    public double ElectricalAmount { get; set; }
    public double PlumbingAmount { get; set; }
    public double EscalationAmount { get; set; }
    public double ConsultingFeeAmount { get; set; }
    public double GrandTotal { get; set; }

    public static EstimateMarkupBreakdown Compute(double baseTotal, EstimateMarkups m)
    {
        double b = Math.Max(0, baseTotal);
        double elec = Round2(b * Math.Max(0, m.ElectricalPct) / 100.0);
        double plum = Round2(b * Math.Max(0, m.PlumbingPct) / 100.0);
        double afterEp = b + elec + plum;
        double esc = Round2(afterEp * Math.Max(0, m.EscalationPct) / 100.0);
        double afterEsc = afterEp + esc;
        double fee = Round2(afterEsc * Math.Max(0, m.ConsultingFeePct) / 100.0);
        return new EstimateMarkupBreakdown
        {
            BaseTotal = Round2(b),
            ElectricalPct = m.ElectricalPct,
            PlumbingPct = m.PlumbingPct,
            EscalationPct = m.EscalationPct,
            ConsultingFeePct = m.ConsultingFeePct,
            ElectricalAmount = elec,
            PlumbingAmount = plum,
            EscalationAmount = esc,
            ConsultingFeeAmount = fee,
            GrandTotal = Round2(afterEsc + fee)
        };
    }

    public JsonObject ToJson() => new()
    {
        ["base_total"] = BaseTotal,
        ["electrical_pct"] = ElectricalPct,
        ["plumbing_pct"] = PlumbingPct,
        ["escalation_pct"] = EscalationPct,
        ["consulting_fee_pct"] = ConsultingFeePct,
        ["electrical_amount"] = ElectricalAmount,
        ["plumbing_amount"] = PlumbingAmount,
        ["escalation_amount"] = EscalationAmount,
        ["consulting_fee_amount"] = ConsultingFeeAmount,
        ["grand_total"] = GrandTotal
    };

    public static EstimateMarkupBreakdown? FromJson(JsonObject? o)
    {
        if (o is null) return null;
        return new EstimateMarkupBreakdown
        {
            BaseTotal = o["base_total"]?.GetValue<double>() ?? 0,
            ElectricalPct = o["electrical_pct"]?.GetValue<double>() ?? 0,
            PlumbingPct = o["plumbing_pct"]?.GetValue<double>() ?? 0,
            EscalationPct = o["escalation_pct"]?.GetValue<double>() ?? 0,
            ConsultingFeePct = o["consulting_fee_pct"]?.GetValue<double>() ?? 0,
            ElectricalAmount = o["electrical_amount"]?.GetValue<double>() ?? 0,
            PlumbingAmount = o["plumbing_amount"]?.GetValue<double>() ?? 0,
            EscalationAmount = o["escalation_amount"]?.GetValue<double>() ?? 0,
            ConsultingFeeAmount = o["consulting_fee_amount"]?.GetValue<double>() ?? 0,
            GrandTotal = o["grand_total"]?.GetValue<double>() ?? 0
        };
    }

    private static double Round2(double v) => Math.Round(v, 2);
}
