using BBSApp.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BBSApp.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ProjectStore.Current.Changed += Refresh;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Refresh();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        ProjectStore.Current.Changed -= Refresh;
    }

    private void Refresh()
    {
        var s = ProjectStore.Current;
        NameText.Text = s.Name;
        int civilCount = s.MasonryWalls.Count + s.Plaster.Count + s.PccBeds.Count + s.Earthwork.Count
            + s.SizeStone.Count + s.Shuttering.Count + s.Flooring.Count + s.Painting.Count;
        CountsText.Text =
            $"Levels {s.Levels.Count} · RCC {s.Columns.Count + s.Beams.Count + s.Slabs.Count + s.Footings.Count + s.Walls.Count + s.Stairs.Count} · " +
            $"Civil {civilCount} (MW {s.MasonryWalls.Count} · PL {s.Plaster.Count} · SH {s.Shuttering.Count} · FL {s.Flooring.Count} · PT {s.Painting.Count}) · " +
            $"Takeoff {s.Takeoff.Items.Count}";
        if (s.LastCivilSummary?.Rows is { Count: > 0 } civil)
            SummaryList.ItemsSource = civil.Select(r => string.Join("  |  ", r)).ToList();
        else if (s.LastSummary?.Rows is { Count: > 0 } rows)
            SummaryList.ItemsSource = rows.Select(r => string.Join("  |  ", r)).ToList();
        else
            SummaryList.ItemsSource = new[] { "Import a PDF under Drawing takeoff, or generate civil / RCC quantities from the nav." };
    }
}
