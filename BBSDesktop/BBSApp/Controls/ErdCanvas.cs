// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using BBSApp.Services;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace BBSApp.Controls;

/// <summary>
/// In-app ERD canvas — draws the data model as draggable entity boxes (table name + typed columns
/// with PK/FK badges) joined by foreign-key connectors. Auto-lays out by FK depth (referenced tables
/// left, dependents right); boxes can be dragged, and selecting one highlights its relationships.
/// Host in a ScrollViewer (ZoomMode enabled) for pan + zoom.
/// </summary>
public sealed class ErdCanvas : UserControl
{
    private readonly Canvas _canvas = new();
    private ErdSchema? _schema;

    private const double BoxW = 224;
    private const double HeaderH = 30;
    private const double RowH = 20;
    private const double ColGap = 320;
    private const double RowGap = 28;
    private const double Pad = 32;

    private readonly Dictionary<string, Rect> _rects = new(StringComparer.OrdinalIgnoreCase);
    private string? _selected;

    // drag state
    private string? _dragId;
    private Point _dragPointerStart;
    private Point _dragBoxStart;
    private bool _moved;

    public event Action<string?>? SelectionChanged;
    public string? SelectedTable => _selected;

    public ErdCanvas()
    {
        _canvas.Background = new SolidColorBrush(Color.FromArgb(255, 30, 33, 40));
        Content = _canvas;
    }

    private Brush TextBrush => Res("TextFillColorPrimaryBrush", Color.FromArgb(255, 235, 235, 235));
    private Brush FaintBrush => Res("TextFillColorSecondaryBrush", Color.FromArgb(255, 155, 160, 170));
    private Brush AccentBrush => Res("AccentFillColorDefaultBrush", Color.FromArgb(255, 90, 130, 230));
    private static readonly Color Pk = Color.FromArgb(255, 214, 162, 74);   // amber
    private static readonly Color Fk = Color.FromArgb(255, 79, 176, 176);   // teal
    private static readonly Color BoxBg = Color.FromArgb(255, 44, 47, 56);
    private static readonly Color EdgeCol = Color.FromArgb(160, 150, 156, 168);

    private static Brush Res(string key, Color fallback) =>
        Application.Current.Resources.TryGetValue(key, out var v) && v is Brush b ? b : new SolidColorBrush(fallback);

    public Size ContentSize => new(_canvas.Width, _canvas.Height);

    public void Render(ErdSchema schema)
    {
        _schema = schema;
        _canvas.Children.Clear();
        _rects.Clear();

        AutoLayout(schema);

        double maxX = Pad, maxY = Pad;
        foreach (var t in schema.Tables)
        {
            double h = BoxHeight(t);
            _rects[t.Name] = new Rect(t.X, t.Y, BoxW, h);
            maxX = Math.Max(maxX, t.X + BoxW);
            maxY = Math.Max(maxY, t.Y + h);
        }
        _canvas.Width = maxX + Pad;
        _canvas.Height = maxY + Pad;

        // Relationships first (behind boxes).
        foreach (var rel in schema.Relations)
        {
            if (!_rects.TryGetValue(rel.FromTable, out var fr) || !_rects.TryGetValue(rel.ToTable, out var tr))
                continue;
            bool hot = _selected is not null
                       && (rel.FromTable.Equals(_selected, StringComparison.OrdinalIgnoreCase)
                           || rel.ToTable.Equals(_selected, StringComparison.OrdinalIgnoreCase));
            DrawEdge(fr, tr, hot);
        }

        foreach (var t in schema.Tables)
            DrawTable(t);
    }

    private static double BoxHeight(ErdTable t) => HeaderH + t.Columns.Count * RowH + 8;

    // ── layout: FK depth = column, stack within column ──
    private void AutoLayout(ErdSchema schema)
    {
        bool needs = schema.Tables.Any(t => double.IsNaN(t.X) || double.IsNaN(t.Y));
        if (!needs) return;

        var refs = schema.Tables.ToDictionary(
            t => t.Name,
            t => schema.Relations.Where(r => r.FromTable.Equals(t.Name, StringComparison.OrdinalIgnoreCase))
                                  .Select(r => r.ToTable)
                                  .Where(x => !x.Equals(t.Name, StringComparison.OrdinalIgnoreCase))
                                  .Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            StringComparer.OrdinalIgnoreCase);

        var depth = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int Depth(string name, HashSet<string> path)
        {
            if (depth.TryGetValue(name, out var d)) return d;
            if (!path.Add(name)) return 0;               // cycle guard
            int best = 0;
            if (refs.TryGetValue(name, out var outs))
                foreach (var o in outs)
                    if (refs.ContainsKey(o)) best = Math.Max(best, Depth(o, path) + 1);
            path.Remove(name);
            depth[name] = best;
            return best;
        }
        foreach (var t in schema.Tables) Depth(t.Name, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var byDepth = schema.Tables
            .GroupBy(t => depth[t.Name])
            .OrderBy(g => g.Key);
        foreach (var col in byDepth)
        {
            double y = Pad;
            foreach (var t in col.OrderByDescending(x => x.Columns.Count))
            {
                t.X = Pad + col.Key * ColGap;
                t.Y = y;
                y += BoxHeight(t) + RowGap;
            }
        }
    }

    private void DrawTable(ErdTable t)
    {
        bool selected = t.Name.Equals(_selected, StringComparison.OrdinalIgnoreCase);
        var stack = new StackPanel { Width = BoxW };

        // header
        var header = new Border
        {
            Height = HeaderH,
            Background = AccentBrush,
            CornerRadius = new CornerRadius(7, 7, 0, 0),
            Padding = new Thickness(10, 0, 10, 0),
            Child = new TextBlock
            {
                Text = t.Name,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            }
        };
        stack.Children.Add(header);

        foreach (var c in t.Columns)
            stack.Children.Add(ColumnRow(c));

        var box = new Border
        {
            Width = BoxW,
            Background = new SolidColorBrush(BoxBg),
            BorderBrush = selected ? AccentBrush : new SolidColorBrush(Color.FromArgb(255, 70, 74, 84)),
            BorderThickness = new Thickness(selected ? 2.2 : 1),
            CornerRadius = new CornerRadius(8),
            Child = stack,
            Tag = t.Name
        };
        Canvas.SetLeft(box, t.X);
        Canvas.SetTop(box, t.Y);
        box.PointerPressed += Box_PointerPressed;
        box.PointerMoved += Box_PointerMoved;
        box.PointerReleased += Box_PointerReleased;
        _canvas.Children.Add(box);
    }

    private FrameworkElement ColumnRow(ErdColumn c)
    {
        var grid = new Grid { Height = RowH, Padding = new Thickness(8, 0, 8, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        string badge = c.IsPk ? "PK" : c.IsFk ? "FK" : "";
        var badgeTb = new TextBlock
        {
            Text = badge,
            FontSize = 8.5,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(c.IsPk ? Pk : Fk),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(badgeTb, 0);
        grid.Children.Add(badgeTb);

        var nameTb = new TextBlock
        {
            Text = c.Name,
            FontSize = 11.5,
            Foreground = TextBrush,
            FontWeight = c.IsPk ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(nameTb, 1);
        grid.Children.Add(nameTb);

        var typeTb = new TextBlock
        {
            Text = c.Type,
            FontSize = 9.5,
            Foreground = FaintBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0)
        };
        Grid.SetColumn(typeTb, 2);
        grid.Children.Add(typeTb);

        return grid;
    }

    private void DrawEdge(Rect from, Rect to, bool hot)
    {
        var fc = new Point(from.X + from.Width / 2, from.Y + from.Height / 2);
        var tc = new Point(to.X + to.Width / 2, to.Y + to.Height / 2);
        bool targetRight = tc.X >= fc.X;

        double x0 = targetRight ? from.X + from.Width : from.X;
        double y0 = Clamp(tc.Y, from.Y + 8, from.Y + from.Height - 8);
        double x1 = targetRight ? to.X : to.X + to.Width;
        double y1 = Clamp(fc.Y, to.Y + 8, to.Y + to.Height - 8);
        double midx = (x0 + x1) / 2;

        var color = hot ? ((SolidColorBrush)AccentBrush).Color : EdgeCol;
        var brush = new SolidColorBrush(color);
        double thick = hot ? 2.2 : 1.4;

        _canvas.Children.Add(new Polyline
        {
            Stroke = brush,
            StrokeThickness = thick,
            Points = new PointCollection { new(x0, y0), new(midx, y0), new(midx, y1), new(x1, y1) }
        });

        // FK dot at the child (many) end.
        var dot = new Ellipse { Width = 7, Height = 7, Fill = brush };
        Canvas.SetLeft(dot, x0 - 3.5);
        Canvas.SetTop(dot, y0 - 3.5);
        _canvas.Children.Add(dot);

        // Arrow into the parent (one) end.
        int dir = x1 >= midx ? 1 : -1;
        _canvas.Children.Add(new Polygon
        {
            Fill = brush,
            Points = new PointCollection
            {
                new(x1, y1), new(x1 - dir * 9, y1 - 5), new(x1 - dir * 9, y1 + 5)
            }
        });
    }

    private static double Clamp(double v, double lo, double hi) => Math.Min(hi, Math.Max(lo, v));

    // ── drag / select ──
    private void Box_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border b || b.Tag is not string id) return;
        _dragId = id;
        _moved = false;
        _dragPointerStart = e.GetCurrentPoint(_canvas).Position;
        _dragBoxStart = new Point(Canvas.GetLeft(b), Canvas.GetTop(b));
        b.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void Box_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragId is null || sender is not Border b) return;
        var p = e.GetCurrentPoint(_canvas).Position;
        double dx = p.X - _dragPointerStart.X, dy = p.Y - _dragPointerStart.Y;
        if (!_moved && Math.Abs(dx) + Math.Abs(dy) < 4) return;
        _moved = true;
        double nx = Math.Max(0, _dragBoxStart.X + dx);
        double ny = Math.Max(0, _dragBoxStart.Y + dy);
        Canvas.SetLeft(b, nx);
        Canvas.SetTop(b, ny);
        if (_schema?.Find(_dragId) is { } t)
        {
            t.X = nx; t.Y = ny;
            Render(_schema);
        }
    }

    private void Box_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border b) b.ReleasePointerCapture(e.Pointer);
        if (_dragId is null) return;
        string id = _dragId;
        _dragId = null;
        if (!_moved)
        {
            _selected = _selected == id ? null : id;
            SelectionChanged?.Invoke(_selected);
            if (_schema is not null) Render(_schema);
        }
    }
}
