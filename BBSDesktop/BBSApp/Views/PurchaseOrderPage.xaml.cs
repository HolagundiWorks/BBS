using BBSApp.Controls;
using BBSApp.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace BBSApp.Views;

public sealed partial class PurchaseOrderPage : Page
{
    private List<PoLine> _steel = new();
    private List<PoLine> _grade = new();
    private List<PoLine> _mat = new();
    private List<ConcreteLine> _concrete = new();
    private readonly ResultTable _steelTable = new();
    private readonly ResultTable _gradeTable = new();
    private readonly ResultTable _concreteTable = new();
    private readonly ResultTable _matTable = new();
    private readonly Dictionary<string, CheckBox> _levelBoxes = new();
    private bool _buildingLevels;
    private bool _syncingRmc;

    public PurchaseOrderPage()
    {
        InitializeComponent();
        _steelTable.SetAutomationName("Steel purchase order");
        _gradeTable.SetAutomationName("Concrete by grade");
        _concreteTable.SetAutomationName("Concrete by element");
        _matTable.SetAutomationName("Other materials purchase order");
        SteelHost.Child = _steelTable;
        GradeHost.Child = _gradeTable;
        ConcreteHost.Child = _concreteTable;
        MatHost.Child = _matTable;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _syncingRmc = true;
        RmcToggle.IsOn = ProjectStore.Current.ConcreteFromRmc;
        _syncingRmc = false;
        BuildLevelChecks(selectAll: true);
        Refresh();
        ProjectStore.Current.Changed += OnStoreChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ProjectStore.Current.Changed -= OnStoreChanged;
    }

    private void OnStoreChanged()
    {
        var selected = SelectedLevels();
        BuildLevelChecks(selectAll: false);
        foreach (var kv in _levelBoxes)
            kv.Value.IsChecked = selected.Contains(kv.Key) || selected.Count == 0;
        if (_levelBoxes.Count > 0 && !_levelBoxes.Values.Any(c => c.IsChecked == true))
            foreach (var cb in _levelBoxes.Values) cb.IsChecked = true;
        _syncingRmc = true;
        RmcToggle.IsOn = ProjectStore.Current.ConcreteFromRmc;
        _syncingRmc = false;
        Refresh();
    }

    private void RmcToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_syncingRmc) return;
        ProjectStore.Current.ConcreteFromRmc = RmcToggle.IsOn;
        ProjectStore.Current.Notify();
        Refresh();
    }

    private void BuildLevelChecks(bool selectAll)
    {
        _buildingLevels = true;
        LevelChecks.Children.Clear();
        _levelBoxes.Clear();
        foreach (var lv in ProjectStore.Current.Levels)
        {
            var label = string.IsNullOrWhiteSpace(lv.Name) ? lv.Id : $"{lv.Id} · {lv.Name}";
            var cb = new CheckBox
            {
                Content = label,
                IsChecked = selectAll,
                Tag = lv.Id,
                MinWidth = 120
            };
            cb.Checked += LevelCheck_Changed;
            cb.Unchecked += LevelCheck_Changed;
            _levelBoxes[lv.Id] = cb;
            LevelChecks.Children.Add(cb);
        }
        _buildingLevels = false;
    }

    private HashSet<string> SelectedLevels() =>
        _levelBoxes.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key).ToHashSet();

    private void LevelCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_buildingLevels) return;
        Refresh();
    }

    private void SelectAllLevels_Click(object sender, RoutedEventArgs e)
    {
        _buildingLevels = true;
        foreach (var cb in _levelBoxes.Values) cb.IsChecked = true;
        _buildingLevels = false;
        Refresh();
    }

    private void SelectNoLevels_Click(object sender, RoutedEventArgs e)
    {
        _buildingLevels = true;
        foreach (var cb in _levelBoxes.Values) cb.IsChecked = false;
        _buildingLevels = false;
        Refresh();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh()
    {
        var store = ProjectStore.Current;
        var levels = SelectedLevels();
        var levelLabel = levels.Count == 0
            ? "none"
            : string.Join(", ", levels.OrderBy(id => id));
        bool rmc = store.ConcreteFromRmc;

        var steelSummary = BuildMergedSteel(store, levels);
        _steel = MaterialsCalculator.SteelPurchaseOrder(steelSummary);
        _steelTable.SetTable(
            new[] { "Category", "Item", "Unit", "Qty", "Notes" },
            _steel.Select(p => (IReadOnlyList<string>)new[] { p.Category, p.Item, p.Unit, p.Qty.ToString("0.##"), p.Notes }).ToList());

        _concrete = MaterialsCalculator.BuildConcreteBoq(store, levels);
        _grade = MaterialsCalculator.ConcreteByGrade(_concrete, rmc);
        _gradeTable.SetTable(
            new[] { "Category", "Item", "Unit", "Qty", "Notes" },
            _grade.Select(p => (IReadOnlyList<string>)new[] { p.Category, p.Item, p.Unit, p.Qty.ToString("0.###"), p.Notes }).ToList());
        GradeTitle.Text = rmc ? "Concrete by grade (RMC m³)" : "Concrete by grade (m³)";

        if (rmc)
        {
            ConcreteTitle.Text = "Concrete by element (volume only)";
            _concreteTable.SetTable(
                new[] { "Level", "Element", "Mark", "Grade", "Vol m³" },
                _concrete.Select(c => (IReadOnlyList<string>)new[]
                {
                    c.Level, c.Element, c.Mark, c.Grade, c.VolumeM3.ToString("0.###")
                }).ToList());
        }
        else
        {
            ConcreteTitle.Text = "Concrete by element (with batching)";
            _concreteTable.SetTable(
                new[] { "Level", "Element", "Mark", "Grade", "Vol m³", "Cement bags", "Sand m³", "Agg m³" },
                _concrete.Select(c => (IReadOnlyList<string>)new[]
                {
                    c.Level, c.Element, c.Mark, c.Grade, c.VolumeM3.ToString("0.###"),
                    c.CementBags.ToString("0.##"), c.SandM3.ToString("0.###"), c.AggregateM3.ToString("0.###")
                }).ToList());
        }

        var civil = CivilBoqCalculator.BuildAll(store, levels);
        _mat = MaterialsCalculator.MaterialPurchaseOrder(_concrete, includeConcreteSplit: !rmc)
            .Concat(CivilBoqCalculator.MaterialPurchaseOrder(civil))
            .ToList();
        MatTitle.Text = rmc ? "Other materials (civil — no RCC batching)" : "Other materials (RCC batch + civil)";
        _matTable.SetTable(
            new[] { "Category", "Item", "Unit", "Qty", "Notes" },
            _mat.Select(p => (IReadOnlyList<string>)new[] { p.Category, p.Item, p.Unit, p.Qty.ToString("0.###"), p.Notes }).ToList());

        SteelTitle.Text = levels.Count == store.Levels.Count && store.Levels.Count > 0
            ? "Steel purchase order (all levels)"
            : $"Steel purchase order ({levelLabel})";

        Info.Title = "Levels";
        Info.Message = levels.Count == 0
            ? "Select at least one level to build a purchase order."
            : $"PO for {levelLabel} · RMC={(rmc ? "on" : "off")} · {_steel.Count} steel · {_grade.Count} grades · {_concrete.Count} RCC · {civil.Count} civil.";
        Info.Severity = levels.Count == 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success;
        Info.IsOpen = true;
    }

    private static GenTable BuildMergedSteel(ProjectStore store, IReadOnlySet<string> levels)
    {
        var merged = new Dictionary<string, (int nos, double len, double wt)>();
        void absorb(string kind, IEnumerable<Dictionary<string, string>> rows)
        {
            var filtered = MaterialsCalculator.FilterByLevels(rows, levels).ToList();
            if (filtered.Count == 0) return;
            var res = EngineClient.Generate(kind, store.SettingsJson(), filtered);
            if (!res.Ok) return;
            foreach (var r in res.Summary.Rows)
            {
                if (r.Count < 4 || r[0].Equals("TOTAL", StringComparison.OrdinalIgnoreCase)) continue;
                if (!int.TryParse(r[1], out var nos)) nos = 0;
                double.TryParse(r[2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var len);
                double.TryParse(r[3], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var wt);
                if (!merged.TryGetValue(r[0], out var cur)) cur = (0, 0, 0);
                merged[r[0]] = (cur.nos + nos, cur.len + len, cur.wt + wt);
            }
        }
        absorb("columns", store.Columns);
        absorb("beams", store.Beams);
        absorb("slabs", store.Slabs);
        absorb("footings", store.Footings);
        absorb("walls", store.Walls);
        absorb("stairs", store.Stairs);

        var t = new GenTable { Headers = new List<string> { "Dia (mm)", "Nos", "Total Length (m)", "Weight (kg)" } };
        foreach (var kv in merged.OrderBy(k => double.TryParse(k.Key, out var d) ? d : 0))
            t.Rows.Add(new List<string>
            {
                kv.Key, kv.Value.nos.ToString(), kv.Value.len.ToString("0.##"), kv.Value.wt.ToString("0.##")
            });
        return t;
    }

    private async void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        var levels = SelectedLevels();
        if (levels.Count == 0)
        {
            Info.Title = "PDF";
            Info.Message = "Select at least one level before exporting PDF.";
            Info.Severity = InfoBarSeverity.Warning;
            Info.IsOpen = true;
            return;
        }

        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow!));
        picker.SuggestedFileName = "po_" + string.Join("_", levels.OrderBy(x => x));
        picker.FileTypeChoices.Add("PDF", new List<string> { ".pdf" });
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;

        if (PdfExport.ExportPurchaseOrder(file.Path, ProjectStore.Current, levels, _steel, _grade, _concrete, _mat,
                ProjectStore.Current.ConcreteFromRmc, out var err))
        {
            Info.Title = "PDF";
            Info.Message = "Purchase order PDF saved.";
            Info.Severity = InfoBarSeverity.Success;
        }
        else
        {
            Info.Title = "PDF";
            Info.Message = err ?? "Export failed";
            Info.Severity = InfoBarSeverity.Error;
        }
        Info.IsOpen = true;
    }

    private async void ExportSteel_Click(object sender, RoutedEventArgs e) =>
        await ExportPo(_steel, "steel_po_" + string.Join("_", SelectedLevels().OrderBy(x => x)));

    private async void ExportMat_Click(object sender, RoutedEventArgs e) =>
        await ExportPo(_mat.Concat(_grade).ToList(), "materials_po_" + string.Join("_", SelectedLevels().OrderBy(x => x)));

    private async Task ExportPo(List<PoLine> lines, string name)
    {
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow!));
        picker.SuggestedFileName = string.IsNullOrWhiteSpace(name) ? "po" : name;
        picker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;
        var headers = new List<string> { "Category", "Item", "Unit", "Qty", "Notes" };
        var rows = lines.Select(p => (IList<string>)new List<string>
        {
            p.Category, p.Item, p.Unit, p.Qty.ToString("0.###"), p.Notes
        }).ToList();
        if (!EngineClient.ExportCsv(file.Path, headers, rows, out var err))
        {
            Info.Message = err ?? "Export failed";
            Info.Severity = InfoBarSeverity.Error;
            Info.IsOpen = true;
        }
    }
}
