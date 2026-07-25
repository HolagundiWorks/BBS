using BBSApp.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BBSApp.Views;

public sealed partial class QuantitiesPage : Page
{
    private readonly List<CheckBox> _levelBoxes = new();

    public QuantitiesPage()
    {
        InitializeComponent();
        BuildLevelChecks();
        Refresh();
    }

    private void BuildLevelChecks()
    {
        LevelChecks.Children.Clear();
        _levelBoxes.Clear();
        ProjectStore.Current.EnsureDefaultLevels();
        foreach (var lv in ProjectStore.Current.Levels)
        {
            var cb = new CheckBox
            {
                Content = $"{lv.Id} — {lv.Name}",
                Tag = lv.Id,
                IsChecked = true,
                MinWidth = 120
            };
            cb.Checked += (_, _) => Refresh();
            cb.Unchecked += (_, _) => Refresh();
            _levelBoxes.Add(cb);
            LevelChecks.Children.Add(cb);
        }
    }

    private HashSet<string> SelectedLevels()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cb in _levelBoxes)
        {
            if (cb.IsChecked == true && cb.Tag is string id)
                set.Add(id);
        }
        return set;
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var cb in _levelBoxes) cb.IsChecked = true;
        Refresh();
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var cb in _levelBoxes) cb.IsChecked = false;
        Refresh();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh()
    {
        var store = ProjectStore.Current;
        var levels = SelectedLevels();

        var concrete = MaterialsCalculator.BuildConcreteBoq(store, levels);
        double concSum = concrete.Sum(c => c.VolumeM3);
        ConcreteTotal.Text = $"{concSum:0.###} m³ total";
        ConcreteList.ItemsSource = concrete
            .Select(c => $"{c.Mark} · {c.Level} · {c.Element} · {c.Grade} · {c.VolumeM3:0.###} m³")
            .DefaultIfEmpty("No concrete for selected levels.")
            .ToList();

        ShutteringCalculator.SyncStore(store);
        var shutter = ShutteringCalculator.AutoFromRcc(store, levels).ToList();
        double shSum = shutter.Sum(s => s.AreaM2 > 0 ? s.AreaM2 : s.Qty);
        ShutterTotal.Text = $"{shSum:0.###} m² (incl. wastage)";
        ShutterList.ItemsSource = shutter
            .Select(s => $"{s.Mark} · {s.Level} · {s.Qty:0.###} m² · {s.Notes}")
            .DefaultIfEmpty("No formwork for selected levels.")
            .ToList();

        if (store.LastSummary?.Rows is { Count: > 0 } steelRows)
        {
            double kg = 0;
            var lines = new List<string>();
            foreach (var row in steelRows)
            {
                if (row.Count < 4) continue;
                if (string.Equals(row[0], "TOTAL", StringComparison.OrdinalIgnoreCase))
                {
                    if (double.TryParse(row[^1], out var t)) kg = t;
                    continue;
                }
                lines.Add(string.Join(" · ", row));
                if (double.TryParse(row[3], out var k)) kg += k;
            }
            SteelTotal.Text = lines.Count > 0 ? $"~{kg:0.#} kg (from last Generate BBS)" : "Generate BBS on an RCC page first.";
            SteelList.ItemsSource = lines.Count > 0 ? lines : new List<string> { "Generate BBS on Columns / Beams / … then refresh." };
        }
        else
        {
            SteelTotal.Text = "Generate BBS on an RCC page first.";
            SteelList.ItemsSource = new List<string> { "No steel summary yet." };
        }
    }
}
