// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using System.IO;
using BBSApp.Controls;
using BBSApp.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BBSApp.Views;

public sealed partial class DataModelPage : Page
{
    private readonly ErdCanvas _canvas = new();
    private ErdSchema _schema = new();
    private string _view = "both";

    private bool _userZoomed;

    public DataModelPage()
    {
        InitializeComponent();
        CanvasHost.Content = _canvas;
        _canvas.SelectionChanged += OnSelectionChanged;
        // Keep the diagram framed to the window until the user takes zoom control.
        CanvasHost.SizeChanged += (_, _) =>
        {
            if (!_userZoomed && CanvasHost.ViewportWidth > 0) FitToView();
        };
        CanvasHost.ViewChanged += (_, _) => UpdateZoomText();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadView();
        // The '*' row / page-in transition resolves the viewport size over a few frames, so re-fit a
        // handful of times as it settles (stops early once the user takes zoom control).
        int ticks = 0;
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(150);
        timer.IsRepeating = true;
        timer.Tick += (_, _) =>
        {
            if (!_userZoomed) FitToView();
            if (++ticks >= 6 || _userZoomed) timer.Stop();
        };
        timer.Start();
    }

    private void ViewCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return; // ignore the initial selection during XAML init
        _userZoomed = false;
        LoadView();
        FitToView();
    }

    private void LoadView()
    {
        _view = (ViewCombo?.SelectedItem as ComboBoxItem)?.Tag as string ?? "both";
        _schema = SchemaModel.FromExport();
        IEnumerable<string> keep = _view switch
        {
            "derivation" => SchemaModel.DerivationTables,
            "composition" => SchemaModel.CompositionTables,
            _ => SchemaModel.DerivationTables.Concat(SchemaModel.CompositionTables)
        };
        SchemaModel.RetainTables(_schema, keep);
        _canvas.Render(_schema);
        ShowStats();
    }

    private void ShowStats()
    {
        int cols = _schema.Tables.Sum(t => t.Columns.Count);
        string scope = _view switch
        {
            "derivation" => "Item → sub-item (derivation): trade → link_rule → derived_item.",
            "composition" => "Item → material (composition): trade / mix_design → material.",
            _ => "Item → sub-item (trade → link_rule → derived_item) and item → material (trade / mix_design → material)."
        };
        Info.Title = "Item relationships";
        Info.Message = $"{_schema.Tables.Count} entities · {cols} columns · {_schema.Relations.Count} relationships. {scope}";
        Info.Severity = InfoBarSeverity.Informational;
    }

    private void OnSelectionChanged(string? table)
    {
        if (table is null || _schema.Find(table) is not { } t) { ShowStats(); return; }
        var outward = _schema.Relations
            .Where(r => r.FromTable.Equals(t.Name, StringComparison.OrdinalIgnoreCase))
            .Select(r => $"{r.FromColumn} → {r.ToTable}");
        var inward = _schema.Relations
            .Where(r => r.ToTable.Equals(t.Name, StringComparison.OrdinalIgnoreCase))
            .Select(r => $"{r.FromTable}.{r.FromColumn}");
        string refs = string.Join(", ", outward);
        string usedBy = string.Join(", ", inward);
        Info.Title = $"{t.Name} · {t.Columns.Count} columns";
        Info.Message =
            (refs.Length > 0 ? $"References: {refs}. " : "")
            + (usedBy.Length > 0 ? $"Referenced by: {usedBy}." : "")
            + (refs.Length == 0 && usedBy.Length == 0 ? "No foreign keys." : "");
        Info.Severity = InfoBarSeverity.Informational;
    }

    private void Relayout_Click(object sender, RoutedEventArgs e)
    {
        foreach (var t in _schema.Tables) { t.X = double.NaN; t.Y = double.NaN; }
        _canvas.Render(_schema);
        _userZoomed = false;
        FitToView();
    }

    private void Fit_Click(object sender, RoutedEventArgs e) { _userZoomed = false; FitToView(); }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) { _userZoomed = true; Zoom(1.25); }
    private void ZoomOut_Click(object sender, RoutedEventArgs e) { _userZoomed = true; Zoom(1 / 1.25); }
    private void ZoomReset_Click(object sender, RoutedEventArgs e) { _userZoomed = true; CanvasHost.ChangeView(null, null, 1f); }

    private void Zoom(double factor)
    {
        float z = (float)Math.Clamp(CanvasHost.ZoomFactor * factor,
            CanvasHost.MinZoomFactor, CanvasHost.MaxZoomFactor);
        CanvasHost.ChangeView(null, null, z);
    }

    private void UpdateZoomText()
    {
        if (ZoomText is not null) ZoomText.Text = $"{CanvasHost.ZoomFactor * 100:0}%";
    }

    private void FitToView()
    {
        var size = _canvas.ContentSize;
        if (size.Width <= 0 || size.Height <= 0) return;
        if (CanvasHost.ViewportWidth <= 0 || CanvasHost.ViewportHeight <= 0) return;
        double zw = CanvasHost.ViewportWidth / size.Width;
        double zh = CanvasHost.ViewportHeight / size.Height;
        float zoom = (float)Math.Clamp(Math.Min(zw, zh), CanvasHost.MinZoomFactor, CanvasHost.MaxZoomFactor);
        if (zoom <= 0 || float.IsNaN(zoom)) zoom = CanvasHost.MinZoomFactor;
        CanvasHost.ChangeView(0, 0, zoom);
    }

    private void ExportSchema_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string dir = Path.Combine(Branding.AppDataDirectory, "schema");
            var (dbml, _) = SchemaExport.WriteFiles(dir);
            Info.Title = "ERD exported";
            Info.Message = $"aqc-core.dbml + .sql written to {Path.GetDirectoryName(dbml)}.";
            Info.Severity = InfoBarSeverity.Success;
        }
        catch (Exception ex)
        {
            Info.Title = "Export failed";
            Info.Message = ex.Message;
            Info.Severity = InfoBarSeverity.Warning;
        }
    }
}
