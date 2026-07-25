using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace BBSApp.Controls;

/// <summary>Fluent table: header + scrollable body (Windows elevation via stroke, not shadow).</summary>
public sealed class ResultTable : UserControl
{
    private readonly Grid _root = new();
    private readonly Border _headerChrome = new();
    private readonly Grid _header = new();
    private readonly StackPanel _body = new() { Spacing = 0 };
    private readonly ScrollViewer _scroll = new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollMode = ScrollMode.Auto
    };

    public ResultTable()
    {
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _headerChrome.Child = _header;
        _headerChrome.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        _headerChrome.CornerRadius = new CornerRadius(4, 4, 0, 0);
        _headerChrome.Padding = new Thickness(0, 2, 0, 2);

        Grid.SetRow(_headerChrome, 0);
        _scroll.Content = _body;
        Grid.SetRow(_scroll, 1);
        _root.Children.Add(_headerChrome);
        _root.Children.Add(_scroll);
        Content = _root;
        AutomationProperties.SetName(this, "Results table");
    }

    public void SetAutomationName(string name) => AutomationProperties.SetName(this, name);

    public void SetTable(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        _header.Children.Clear();
        _header.ColumnDefinitions.Clear();
        _body.Children.Clear();

        for (int i = 0; i < headers.Count; i++)
        {
            _header.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star),
                MinWidth = 80
            });
            var tb = new TextBlock
            {
                Text = headers[i],
                Style = (Style)Application.Current.Resources["BodyStrongStyle"],
                Margin = new Thickness(8, 6, 8, 6),
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };
            AutomationProperties.SetName(tb, headers[i]);
            Grid.SetColumn(tb, i);
            _header.Children.Add(tb);
        }

        Brush? alt = null;
        if (Application.Current.Resources.TryGetValue("SubtleFillColorTertiaryBrush", out var brush))
            alt = brush as Brush;

        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            var g = new Grid();
            if (alt != null && r % 2 == 1) g.Background = alt;
            for (int i = 0; i < headers.Count; i++)
            {
                g.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star),
                    MinWidth = 80
                });
                var tb = new TextBlock
                {
                    Text = i < row.Count ? row[i] : "",
                    Margin = new Thickness(8, 6, 8, 6),
                    TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
                };
                Grid.SetColumn(tb, i);
                g.Children.Add(tb);
            }
            _body.Children.Add(g);
        }
    }

    public void Clear()
    {
        _header.Children.Clear();
        _header.ColumnDefinitions.Clear();
        _body.Children.Clear();
    }
}
