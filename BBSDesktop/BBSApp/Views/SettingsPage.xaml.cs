using System.Globalization;
using BBSApp.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BBSApp.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        var s = ProjectStore.Current;
        DiaBox.Text = string.Join(", ", s.Diameters);
        HysdBondToggle.IsOn = s.HysdBond;
        HysdFactorBox.Value = s.HysdBondFactor;
        MinHookBox.Value = s.MinHookMm;
        HookBox.Text = FormatMap(s.HookAllowance);
        BendBox.Text = FormatMap(s.BendDeduction);
        var y = s.Yields;
        BricksM3Box.Value = y.BricksPerM3;
        BricksM2Box.Value = y.BricksPerM2Half;
        MortarFracBox.Value = y.MortarFraction;
        DryFactorBox.Value = y.MortarDryFactor;
        WastageBox.Value = y.Wastage;
        ShutterWasteBox.Value = y.ShutteringWastage;
        IgnoreOpenBox.Value = y.IgnoreOpeningBelowM2;
        BeamSlabDeductToggle.IsOn = y.BeamSlabInterfaceDeduct;
        CoverColBox.Value = s.CoverColumnMm;
        CoverBeamBox.Value = s.CoverBeamMm;
        CoverSlabBox.Value = s.CoverSlabMm;
        CoverFootBox.Value = s.CoverFootingMm;
        CoverPedBox.Value = s.CoverPedestalMm;
        CoverLintBox.Value = s.CoverLintelMm;
        ColLapBox.SelectedItem = s.DefaultColumnLap is "Yes" or "No" ? s.DefaultColumnLap : "No";
        BeamLapBox.SelectedItem = s.DefaultBeamLap is "None" or "Tension" ? s.DefaultBeamLap : "None";
    }

    private static string FormatMap(Dictionary<int, double> map) =>
        string.Join(", ", map.OrderBy(kv => kv.Key)
            .Select(kv => $"{kv.Key}:{kv.Value.ToString(CultureInfo.InvariantCulture)}"));

    private static Dictionary<int, double> ParseMap(string text, Dictionary<int, double> fallback)
    {
        var dest = new Dictionary<int, double>();
        foreach (var part in text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var bits = part.Split(':');
            if (bits.Length < 2) continue;
            if (int.TryParse(bits[0].Trim(), out var k) &&
                double.TryParse(bits[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                dest[k] = v;
        }
        if (dest.Count == 0)
            foreach (var kv in fallback) dest[kv.Key] = kv.Value;
        return dest;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var parts = DiaBox.Text.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
        var list = new List<int>();
        foreach (var p in parts)
            if (int.TryParse(p.Trim(), out var d) && d > 0) list.Add(d);
        if (list.Count == 0)
        {
            AppNotify.Error("Enter at least one diameter.");
            return;
        }

        var store = ProjectStore.Current;
        store.Diameters.Clear();
        foreach (var d in list.Distinct().OrderBy(x => x))
            store.Diameters.Add(d);

        store.HysdBond = HysdBondToggle.IsOn;
        store.HysdBondFactor = double.IsNaN(HysdFactorBox.Value) || HysdFactorBox.Value <= 0
            ? 1.6 : HysdFactorBox.Value;
        store.MinHookMm = double.IsNaN(MinHookBox.Value) ? 75 : Math.Max(0, MinHookBox.Value);

        store.HookAllowance.Clear();
        foreach (var kv in ParseMap(HookBox.Text, new Dictionary<int, double> { [90] = 9, [135] = 10, [180] = 16 }))
            store.HookAllowance[kv.Key] = kv.Value;

        store.BendDeduction.Clear();
        foreach (var kv in ParseMap(BendBox.Text, new Dictionary<int, double> { [45] = 1, [90] = 2, [135] = 3 }))
            store.BendDeduction[kv.Key] = kv.Value;

        var y = store.Yields;
        y.BricksPerM3 = Val(BricksM3Box.Value, 500);
        y.BricksPerM2Half = Val(BricksM2Box.Value, 55);
        y.MortarFraction = Val(MortarFracBox.Value, 0.30);
        y.MortarDryFactor = Val(DryFactorBox.Value, 1.33);
        y.Wastage = Val(WastageBox.Value, 1.05);
        y.ShutteringWastage = Val(ShutterWasteBox.Value, 1.05);
        y.IgnoreOpeningBelowM2 = Val(IgnoreOpenBox.Value, 0.1);
        y.BeamSlabInterfaceDeduct = BeamSlabDeductToggle.IsOn;

        store.CoverColumnMm = Val(CoverColBox.Value, 40);
        store.CoverBeamMm = Val(CoverBeamBox.Value, 25);
        store.CoverSlabMm = Val(CoverSlabBox.Value, 20);
        store.CoverFootingMm = Val(CoverFootBox.Value, 50);
        store.CoverPedestalMm = Val(CoverPedBox.Value, 50);
        store.CoverLintelMm = Val(CoverLintBox.Value, 25);
        store.DefaultColumnLap = ColLapBox.SelectedItem?.ToString() ?? "No";
        store.DefaultBeamLap = BeamLapBox.SelectedItem?.ToString() ?? "None";

        store.Notify();
        AppNotify.Success("Settings saved", "IS 456 covers/laps + civil yields.");
    }

    private static double Val(double v, double def) =>
        double.IsNaN(v) || v <= 0 ? def : v;
}
