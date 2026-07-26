using BBSApp.Controls;
using BBSApp.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BBSApp.Views;

public sealed partial class QuantitiesPage : Page
{
    private readonly List<CheckBox> _levelBoxes = new();
    private readonly ResultTable _concreteTable = new();
    private readonly ResultTable _shutterTable = new();
    private readonly ResultTable _steelTable = new();

    public QuantitiesPage()
    {
        InitializeComponent();
        _concreteTable.SetAutomationName("Concrete quantities");
        _shutterTable.SetAutomationName("Formwork quantities");
        _steelTable.SetAutomationName("Steel quantities");
        ConcreteHost.Child = _concreteTable;
        ShutterHost.Child = _shutterTable;
        SteelHost.Child = _steelTable;
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
        _concreteTable.SetTable(
            new[] { "Mark", "Level", "Element", "Grade", "Vol m³" },
            concrete.Select(c => (IReadOnlyList<string>)new[]
            {
                c.Mark, c.Level, c.Element, c.Grade, c.VolumeM3.ToString("0.###")
            }).ToList());

        ShutteringCalculator.SyncStore(store);
        var shutter = ShutteringCalculator.AutoFromRcc(store, levels).ToList();
        double shSum = shutter.Sum(s => s.AreaM2 > 0 ? s.AreaM2 : s.Qty);
        ShutterTotal.Text = $"{shSum:0.###} m² (incl. wastage)";
        _shutterTable.SetTable(
            new[] { "Mark", "Level", "Qty m²", "Notes" },
            shutter.Select(s => (IReadOnlyList<string>)new[]
            {
                s.Mark, s.Level, s.Qty.ToString("0.###"), s.Notes
            }).ToList());

        if (store.LastSummary?.Rows is { Count: > 0 } steelRows)
        {
            double kg = 0;
            var rows = new List<IReadOnlyList<string>>();
            var headers = store.LastSummary.Headers.Count > 0
                ? store.LastSummary.Headers.ToArray()
                : new[] { "Dia (mm)", "Nos", "Length (m)", "Weight (kg)" };
            foreach (var row in steelRows)
            {
                if (row.Count == 0) continue;
                if (string.Equals(row[0], "TOTAL", StringComparison.OrdinalIgnoreCase))
                {
                    if (double.TryParse(row[^1], out var t)) kg = t;
                    continue;
                }
                rows.Add(row);
                if (row.Count > 3 && double.TryParse(row[3], out var k)) kg += k;
            }
            SteelTotal.Text = rows.Count > 0 ? $"~{kg:0.#} kg (from last Generate BBS)" : "Generate BBS on an RCC page first.";
            _steelTable.SetTable(headers, rows);
        }
        else
        {
            SteelTotal.Text = "Generate BBS on an RCC page first.";
            _steelTable.SetTable(
                new[] { "Note" },
                new IReadOnlyList<string>[] { new[] { "No steel summary yet — generate BBS on Columns / Beams / …" } });
        }
    }
}
