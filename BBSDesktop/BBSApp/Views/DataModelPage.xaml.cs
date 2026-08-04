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
    private readonly ResultTable _itemTable = new();
    private readonly ErdCanvas _canvas = new();
    private ErdSchema _schema = new();
    private bool _userZoomed;
    private bool _diagramReady;

    public DataModelPage()
    {
        InitializeComponent();
        _itemTable.SetAutomationName("Item relationships");
        TableHost.Child = _itemTable;
        CanvasHost.Content = _canvas;
        CanvasHost.SizeChanged += (_, _) =>
        {
            if (Diagram && !_userZoomed && CanvasHost.ViewportWidth > 0) FitToView();
        };
        CanvasHost.ViewChanged += (_, _) => UpdateZoomText();
        Loaded += OnLoaded;
    }

    private bool Diagram => (ViewCombo.SelectedItem as ComboBoxItem)?.Tag as string == "diagram";

    private void OnLoaded(object sender, RoutedEventArgs e) => ShowItems();

    private void ViewCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (Diagram) ShowDiagram();
        else ShowItems();
    }

    // ── Items table ──────────────────────────────────────────────────────────

    private void ShowItems()
    {
        TableHost.Visibility = Visibility.Visible;
        CanvasHost.Visibility = Visibility.Collapsed;
        DiagramTools.Visibility = Visibility.Collapsed;

        var rows = ItemRelations.Build();
        _itemTable.SetTable(
            new[] { "Item", "UOM", "Rate", "Inputs", "Outputs" },
            rows.Select(r => (IReadOnlyList<string>)new[] { r.Item, r.Uom, r.Rate, r.Inputs, r.Outputs }).ToList());

        var ver = RateBookStore.Current.ActiveOrFirst();
        Info.Title = "Item relationships";
        Info.Message = $"{rows.Count} items · unit, rate, inputs (source items + materials) and outputs (derived items)."
                     + (ver is not null ? $" Rates from “{ver.Name}”." : " No rate book loaded.");
        Info.Severity = InfoBarSeverity.Informational;
    }

    // ── Schema diagram ───────────────────────────────────────────────────────

    private void ShowDiagram()
    {
        TableHost.Visibility = Visibility.Collapsed;
        CanvasHost.Visibility = Visibility.Visible;
        DiagramTools.Visibility = Visibility.Visible;

        if (!_diagramReady)
        {
            _schema = SchemaModel.FromExport();
            _canvas.Render(_schema);
            _diagramReady = true;
        }
        _userZoomed = false;
        FitToView();

        int cols = _schema.Tables.Sum(t => t.Columns.Count);
        Info.Title = "Schema diagram";
        Info.Message = $"Full logical schema — {_schema.Tables.Count} entities · {cols} columns · {_schema.Relations.Count} foreign keys.";
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

    private void UpdateZoomText()
    {
        if (ZoomText is not null) ZoomText.Text = $"{CanvasHost.ZoomFactor * 100:0}%";
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
