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
    private readonly ResultTable _table = new();

    public DataModelPage()
    {
        InitializeComponent();
        _table.SetAutomationName("Item table");
        TableHost.Child = _table;
        Loaded += OnLoaded;
    }

    private bool Definitions => (ViewCombo.SelectedItem as ComboBoxItem)?.Tag as string == "definitions";

    private void OnLoaded(object sender, RoutedEventArgs e) => Render();

    private void ViewCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) Render();
    }

    private void Render()
    {
        if (Definitions) ShowDefinitions();
        else ShowRelationships();
    }

    // Item | UOM | Rate | Inputs | Outputs — the pricing / derivation summary.
    private void ShowRelationships()
    {
        SubtitleText.Text = "Each item with its unit and rate, what feeds it (inputs — source items + materials) "
                          + "and what it produces (outputs — derived items).";
        var rows = ItemRelations.Build();
        _table.SetTable(
            new[] { "Item", "UOM", "Rate", "Inputs", "Outputs" },
            rows.Select(r => (IReadOnlyList<string>)new[] { r.Item, r.Uom, r.Rate, r.Inputs, r.Outputs }).ToList());

        var ver = RateBookStore.Current.ActiveOrFirst();
        Info.Title = "Relationships";
        Info.Message = $"{rows.Count} items · unit, rate, inputs and outputs."
                     + (ver is not null ? $" Rates from “{ver.Name}”." : " No rate book loaded.");
        Info.Severity = InfoBarSeverity.Informational;
    }

    // Item | UOM | L | B | H | Area | Volume | Material 1-3 | Calculation recipe — the item master.
    private void ShowDefinitions()
    {
        SubtitleText.Text = "Each item's measurement dimensions and material recipe — the values that drive "
                          + "measurement → sub-item extraction and the material composition.";
        var rows = ItemDefinitions.Build();
        _table.SetTable(
            new[] { "Item", "UOM", "Length", "Breadth", "Height", "Area", "Volume",
                    "Material 1", "Material 2", "Material 3", "Calculation recipe" },
            rows.Select(r => (IReadOnlyList<string>)new[]
            {
                r.Item, r.Uom, r.Length, r.Breadth, r.Height, r.Area, r.Volume,
                r.Material1, r.Material2, r.Material3, r.Recipe
            }).ToList());

        Info.Title = "Definitions";
        Info.Message = $"{rows.Count} items · measurement dimensions (L/B/H → Area/Volume), materials, and the "
                     + "calculation recipe used to extract sub-items.";
        Info.Severity = InfoBarSeverity.Informational;
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
