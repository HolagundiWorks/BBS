using System.Globalization;
using BBSApp.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BBSApp.Views;

public sealed partial class LevelsPage : Page
{
    public LevelsPage()
    {
        InitializeComponent();
        ProjectStore.Current.EnsureDefaultLevels();
        LevelList.ItemsSource = ProjectStore.Current.Levels;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var n = ProjectStore.Current.Levels.Count;
        ProjectStore.Current.Levels.Add(new LevelDef
        {
            Id = "Lvl" + n,
            Name = n == 0 ? "Plinth" : $"Level {n}",
            HeightMm = 3000,
            SlabThicknessMm = 150,
            BeamDepthMm = 450
        });
        ProjectStore.Current.RenumberLevels();
        ProjectStore.Current.Notify();
        Info.Message = "Level added.";
        Info.Severity = InfoBarSeverity.Success;
        Info.IsOpen = true;
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        var levels = ProjectStore.Current.Levels;
        if (levels.Count <= 1)
        {
            Info.Message = "Keep at least Lvl0 (plinth).";
            Info.Severity = InfoBarSeverity.Warning;
            Info.IsOpen = true;
            return;
        }
        levels.RemoveAt(levels.Count - 1);
        ProjectStore.Current.RenumberLevels();
        ProjectStore.Current.Notify();
    }

    private void ApplyColumns_Click(object sender, RoutedEventArgs e)
    {
        int n = 0;
        foreach (var col in ProjectStore.Current.Columns)
        {
            if (!col.TryGetValue("level", out var lv) || string.IsNullOrWhiteSpace(lv)) lv = "Lvl0";
            var h = ProjectStore.Current.ColumnHeightFor(lv);
            if (h <= 0) continue;
            col["height"] = h.ToString("0", CultureInfo.InvariantCulture);
            n++;
        }
        ProjectStore.Current.Notify();
        Info.Message = $"Updated height on {n} column(s).";
        Info.Severity = InfoBarSeverity.Success;
        Info.IsOpen = true;
    }
}
