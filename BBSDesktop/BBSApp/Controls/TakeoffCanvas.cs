// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using BBSApp.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace BBSApp.Controls;

/// <summary>PDF page host with category-colored overlays (point / line / area).</summary>
public sealed class TakeoffCanvas : UserControl
{
    private readonly Grid _root = new();
    private readonly Image _pageImage = new() { Stretch = Stretch.None };
    private readonly Canvas _overlay = new();
    private readonly Canvas _draft = new();
    private readonly HashSet<string> _hidden = new(StringComparer.OrdinalIgnoreCase);

    public event Action<Point>? PointerPressedOnPage;
    public event Action<Point>? PointerMovedOnPage;

    public ImageSource? PageSource
    {
        get => _pageImage.Source;
        set
        {
            _pageImage.Source = value;
            if (value is Microsoft.UI.Xaml.Media.Imaging.BitmapSource bs)
            {
                _overlay.Width = bs.PixelWidth;
                _overlay.Height = bs.PixelHeight;
                _draft.Width = bs.PixelWidth;
                _draft.Height = bs.PixelHeight;
            }
        }
    }

    public TakeoffItem? SelectedItem { get; set; }
    public IList<TakeoffPoint>? DraftPoints { get; set; }
    /// <summary>Controls draft rubber-band: Line (no fill rect), Area (polyline + fill), Opening (rect).</summary>
    public string DraftMode { get; set; } = "Line";
    public TakeoffPoint? SnapHint { get; set; }
    /// <summary>Live measure label drawn near the last draft segment (e.g. "2450 mm").</summary>
    public string? DraftReadout { get; set; }
    public TakeoffPoint? DraftReadoutAt { get; set; }

    public TakeoffCanvas()
    {
        _root.Children.Add(_pageImage);
        _root.Children.Add(_overlay);
        _root.Children.Add(_draft);
        Content = _root;
        _overlay.IsHitTestVisible = false;
        _draft.IsHitTestVisible = false;
        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        Background = new SolidColorBrush(Color.FromArgb(255, 40, 40, 40));
    }

    public void SetCategoryVisible(string category, bool visible)
    {
        if (visible) _hidden.Remove(category);
        else _hidden.Add(category);
    }

    public bool IsCategoryVisible(string category) => !_hidden.Contains(category);

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(this).Position;
        PointerPressedOnPage?.Invoke(p);
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(this).Position;
        PointerMovedOnPage?.Invoke(p);
    }

    public void Redraw(IEnumerable<TakeoffItem> items)
    {
        _overlay.Children.Clear();
        foreach (var it in items)
        {
            if (_hidden.Contains(it.Category)) continue;
            if (it.Points.Count == 0) continue;
            var stroke = BrushFor(it.Category, it.Committed);
            bool selected = it == SelectedItem;

            if (it.Tool.Equals("Point", StringComparison.OrdinalIgnoreCase)
                || (it.Points.Count == 1 && !IsAreaTool(it.Tool)))
            {
                DrawPointMarker(it.Points[0], it.Mark, stroke, selected);
                continue;
            }

            if (it.Points.Count < 2) continue;

            if (IsAreaTool(it.Tool))
            {
                if (it.Points.Count >= 3
                    && it.Tool.Equals("Area", StringComparison.OrdinalIgnoreCase))
                {
                    var poly = new Polygon
                    {
                        Stroke = stroke,
                        StrokeThickness = selected ? 3 : 1.5,
                        Fill = new SolidColorBrush(Color.FromArgb(36, 0, 120, 215)),
                        Points = new PointCollection()
                    };
                    if (!it.Committed) poly.StrokeDashArray = new DoubleCollection { 4, 2 };
                    foreach (var pt in it.Points)
                        poly.Points.Add(new Point(pt.X, pt.Y));
                    _overlay.Children.Add(poly);
                }
                else
                {
                    var a = it.Points[0];
                    var b = it.Points[^1];
                    double x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
                    double w = Math.Abs(b.X - a.X), h = Math.Abs(b.Y - a.Y);
                    var rect = new Rectangle
                    {
                        Width = Math.Max(w, 1), Height = Math.Max(h, 1),
                        Stroke = stroke, StrokeThickness = selected ? 3 : 1.5,
                        Fill = new SolidColorBrush(Color.FromArgb(28, 0, 120, 215))
                    };
                    if (!it.Committed) rect.StrokeDashArray = new DoubleCollection { 4, 2 };
                    Canvas.SetLeft(rect, x);
                    Canvas.SetTop(rect, y);
                    _overlay.Children.Add(rect);
                }

                var mid = it.Points[it.Points.Count / 2];
                string areaNote = it.Fields.TryGetValue("area_m2", out var am) ? $" · {am} m²" : "";
                var label = new TextBlock
                {
                    Text = $"{it.Mark}{areaNote}",
                    Foreground = stroke,
                    FontSize = 11,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(label, mid.X + 4);
                Canvas.SetTop(label, mid.Y + 4);
                _overlay.Children.Add(label);
            }
            else
            {
                var poly = new Polyline
                {
                    Stroke = stroke,
                    StrokeThickness = selected ? 3 : 2,
                    Points = new PointCollection()
                };
                foreach (var pt in it.Points)
                    poly.Points.Add(new Point(pt.X, pt.Y));
                _overlay.Children.Add(poly);

                var mid = it.Points[it.Points.Count / 2];
                var label = new TextBlock
                {
                    Text = $"{it.Mark} · {it.LengthMm:0.#} mm",
                    Foreground = stroke,
                    FontSize = 11,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(label, mid.X + 4);
                Canvas.SetTop(label, mid.Y + 4);
                _overlay.Children.Add(label);
            }
        }
        RedrawDraft();
    }

    private void DrawPointMarker(TakeoffPoint pt, string mark, Brush stroke, bool selected)
    {
        const double r = 10;
        var disc = new Ellipse
        {
            Width = r * 2, Height = r * 2,
            Fill = stroke,
            Stroke = new SolidColorBrush(Colors.White),
            StrokeThickness = selected ? 2.5 : 1.5
        };
        Canvas.SetLeft(disc, pt.X - r);
        Canvas.SetTop(disc, pt.Y - r);
        _overlay.Children.Add(disc);

        // Short number from mark (last segment after '-')
        string shortMark = mark;
        int last = mark.LastIndexOf('-');
        if (last >= 0 && last < mark.Length - 1)
            shortMark = mark[(last + 1)..];

        var num = new TextBlock
        {
            Text = shortMark,
            Foreground = new SolidColorBrush(Colors.White),
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            IsHitTestVisible = false
        };
        num.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(num, pt.X - num.DesiredSize.Width / 2);
        Canvas.SetTop(num, pt.Y - num.DesiredSize.Height / 2);
        _overlay.Children.Add(num);

        var label = new TextBlock
        {
            Text = mark,
            Foreground = stroke,
            FontSize = 11,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(label, pt.X + r + 4);
        Canvas.SetTop(label, pt.Y - 8);
        _overlay.Children.Add(label);
    }

    private static bool IsAreaTool(string tool) =>
        tool.Equals("Area", StringComparison.OrdinalIgnoreCase)
        || tool.Equals("Rectangle", StringComparison.OrdinalIgnoreCase)
        || tool.Equals("Opening", StringComparison.OrdinalIgnoreCase);

    public void RedrawDraft()
    {
        _draft.Children.Clear();
        if (DraftPoints is null || DraftPoints.Count == 0)
        {
            DrawSnapHintOnly();
            return;
        }
        var brush = new SolidColorBrush(Colors.Orange);
        bool areaMode = DraftMode.Equals("Area", StringComparison.OrdinalIgnoreCase);
        bool openingMode = DraftMode.Equals("Opening", StringComparison.OrdinalIgnoreCase);

        if (DraftPoints.Count == 1 && !areaMode)
        {
            var el = new Ellipse { Width = 8, Height = 8, Fill = brush };
            Canvas.SetLeft(el, DraftPoints[0].X - 4);
            Canvas.SetTop(el, DraftPoints[0].Y - 4);
            _draft.Children.Add(el);
            DrawSnapHintOnly();
            return;
        }

        if (openingMode && DraftPoints.Count >= 2)
        {
            var a = DraftPoints[0];
            var b = DraftPoints[^1];
            double x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
            double w = Math.Abs(b.X - a.X), h = Math.Abs(b.Y - a.Y);
            var rect = new Rectangle
            {
                Width = Math.Max(w, 1), Height = Math.Max(h, 1),
                Stroke = brush, StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 3, 2 },
                Fill = new SolidColorBrush(Color.FromArgb(40, 255, 165, 0))
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            _draft.Children.Add(rect);
        }
        else
        {
            // Line or polyline — never a bounding box for Line mode
            var poly = new Polyline { Stroke = brush, StrokeThickness = 2, Points = new PointCollection() };
            foreach (var p in DraftPoints)
                poly.Points.Add(new Point(p.X, p.Y));
            _draft.Children.Add(poly);

            if (areaMode && DraftPoints.Count >= 3)
            {
                var fill = new Polygon
                {
                    Stroke = brush,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 2 },
                    Fill = new SolidColorBrush(Color.FromArgb(35, 255, 165, 0)),
                    Points = new PointCollection()
                };
                foreach (var p in DraftPoints)
                    fill.Points.Add(new Point(p.X, p.Y));
                _draft.Children.Add(fill);
            }

            foreach (var p in DraftPoints)
            {
                var el = new Ellipse { Width = 6, Height = 6, Fill = brush };
                Canvas.SetLeft(el, p.X - 3);
                Canvas.SetTop(el, p.Y - 3);
                _draft.Children.Add(el);
            }
        }

        if (!string.IsNullOrEmpty(DraftReadout) && DraftReadoutAt is not null)
        {
            var label = new TextBlock
            {
                Text = DraftReadout,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                IsHitTestVisible = false
            };
            var bg = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 20, 20, 20)),
                Padding = new Thickness(6, 2, 6, 2),
                CornerRadius = new CornerRadius(3),
                Child = label,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(bg, DraftReadoutAt.X + 10);
            Canvas.SetTop(bg, DraftReadoutAt.Y - 18);
            _draft.Children.Add(bg);
        }

        DrawSnapHintOnly();
    }

    private void DrawSnapHintOnly()
    {
        if (SnapHint is null) return;
        var ring = new Ellipse
        {
            Width = 14, Height = 14,
            Stroke = new SolidColorBrush(Colors.Cyan),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(60, 0, 255, 255))
        };
        Canvas.SetLeft(ring, SnapHint.X - 7);
        Canvas.SetTop(ring, SnapHint.Y - 7);
        _draft.Children.Add(ring);
    }

    public static Brush BrushFor(string category, bool committed)
    {
        byte a = committed ? (byte)255 : (byte)200;
        Color c = category.ToLowerInvariant() switch
        {
            "columns" or "column" or "rcc" => Color.FromArgb(a, 220, 80, 80),
            "beams" or "beam" => Color.FromArgb(a, 200, 120, 40),
            "slabs" or "slab" => Color.FromArgb(a, 80, 140, 220),
            "footings" or "footing" => Color.FromArgb(a, 140, 90, 60),
            "masonry" => Color.FromArgb(a, 180, 60, 60),
            "plaster" => Color.FromArgb(a, 100, 160, 100),
            "pcc" => Color.FromArgb(a, 120, 120, 120),
            "earthwork" or "earth" => Color.FromArgb(a, 160, 120, 60),
            "ssm" => Color.FromArgb(a, 100, 100, 140),
            "shuttering" => Color.FromArgb(a, 40, 160, 180),
            "flooring" => Color.FromArgb(a, 160, 80, 160),
            "painting" or "paint" => Color.FromArgb(a, 80, 80, 200),
            "scale" => Color.FromArgb(a, 0, 200, 200),
            _ => Color.FromArgb(a, 0, 120, 215)
        };
        return new SolidColorBrush(c);
    }
}
