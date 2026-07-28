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

/// <summary>CPM/PERT activity-on-node editor: draggable nodes, dependency arrows, critical path,
/// and drag-from-handle to create FS links (like a schema modeller).</summary>
public sealed class NetworkCanvas : UserControl
{
    private readonly Canvas _canvas = new();
    private ProjectSchedule? _schedule;

    private const double NodeW = 158;
    private const double NodeH = 70;
    private const double ColGap = 210;
    private const double RowGap = 96;
    private const double Margin = 24;

    private static readonly Color Critical = Color.FromArgb(255, 214, 69, 69);
    private static readonly Color Normal = Color.FromArgb(255, 90, 140, 200);

    private readonly Dictionary<string, Rect> _rects = new();
    private string? _selectedId;

    // drag state
    private string? _dragId;
    private Point _dragPointerStart;
    private Point _dragNodeStart;
    private bool _moved;

    // link state
    private string? _linkFromId;
    private Line? _tempLink;

    public event Action<string, double, double>? NodeMoved;
    public event Action<string, string>? LinkRequested;
    public event Action<string?>? SelectionChanged;

    public string? SelectedId => _selectedId;

    public NetworkCanvas()
    {
        _canvas.Background = new SolidColorBrush(Color.FromArgb(255, 32, 32, 36));
        Content = _canvas;
    }

    private Brush TextBrush => Res("TextFillColorPrimaryBrush", Color.FromArgb(255, 235, 235, 235));
    private Brush FaintBrush => Res("TextFillColorSecondaryBrush", Color.FromArgb(255, 160, 160, 160));

    private static Brush Res(string key, Color fallback) =>
        Application.Current.Resources.TryGetValue(key, out var v) && v is Brush b ? b : new SolidColorBrush(fallback);

    public void Render(ProjectSchedule schedule)
    {
        _schedule = schedule;
        _canvas.Children.Clear();
        _rects.Clear();

        var result = ScheduleCalculator.Compute(schedule);
        AutoLayout(schedule);

        double maxX = Margin + NodeW, maxY = Margin + NodeH;
        foreach (var a in schedule.Activities)
        {
            _rects[a.Id] = new Rect(a.X, a.Y, NodeW, NodeH);
            maxX = Math.Max(maxX, a.X + NodeW);
            maxY = Math.Max(maxY, a.Y + NodeH);
        }
        _canvas.Width = maxX + Margin;
        _canvas.Height = maxY + Margin;

        // Arrows first (behind nodes).
        foreach (var a in schedule.Activities)
            foreach (var l in a.Links)
            {
                if (!_rects.TryGetValue(l.PredecessorId, out var pr) || !_rects.TryGetValue(a.Id, out var sr))
                    continue;
                var pred = schedule.Find(l.PredecessorId);
                bool crit = pred is { IsCritical: true } && a.IsCritical;
                DrawEdge(pr, sr, crit ? Critical : Color.FromArgb(180, 150, 150, 150), l);
            }

        foreach (var a in schedule.Activities)
            DrawNode(a);

        // Canvas-level handlers for link dragging.
        _canvas.PointerMoved -= Canvas_PointerMoved;
        _canvas.PointerMoved += Canvas_PointerMoved;
    }

    private void AutoLayout(ProjectSchedule schedule)
    {
        var ranks = ScheduleCalculator.Ranks(schedule);
        var perRank = new Dictionary<int, int>();
        foreach (var a in schedule.Activities)
        {
            if (!double.IsNaN(a.X) && !double.IsNaN(a.Y)) continue;
            int r = ranks.TryGetValue(a.Id, out var rr) ? rr : 0;
            int row = perRank.TryGetValue(r, out var c) ? c : 0;
            perRank[r] = row + 1;
            a.X = Margin + r * ColGap;
            a.Y = Margin + row * RowGap;
        }
    }

    private void DrawNode(ScheduleActivity a)
    {
        var accent = a.InCycle ? Color.FromArgb(255, 230, 120, 40)
                   : a.IsCritical ? Critical : Normal;
        bool selected = a.Id == _selectedId;

        var stack = new StackPanel { Spacing = 1 };
        stack.Children.Add(new TextBlock
        {
            Text = a.Name,
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = TextBrush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        });
        stack.Children.Add(new TextBlock
        {
            Text = a.IsMilestone ? "milestone" : $"dur {a.DurationDays:0.#}d  ·  float {a.TotalFloat:0.#}",
            FontSize = 9, Foreground = FaintBrush
        });
        stack.Children.Add(new TextBlock
        {
            Text = $"ES {a.EarlyStart:0.#}  EF {a.EarlyFinish:0.#}",
            FontSize = 9, Foreground = FaintBrush
        });
        stack.Children.Add(new TextBlock
        {
            Text = $"LS {a.LateStart:0.#}  LF {a.LateFinish:0.#}",
            FontSize = 9, Foreground = FaintBrush
        });

        var border = new Border
        {
            Width = NodeW,
            Height = NodeH,
            Background = new SolidColorBrush(Color.FromArgb(255, 44, 46, 52)),
            BorderBrush = new SolidColorBrush(accent),
            BorderThickness = new Thickness(selected ? 2.5 : 1.5),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 6, 8, 6),
            Child = stack,
            Tag = a.Id
        };
        Canvas.SetLeft(border, a.X);
        Canvas.SetTop(border, a.Y);
        border.PointerPressed += Node_PointerPressed;
        border.PointerMoved += Node_PointerMoved;
        border.PointerReleased += Node_PointerReleased;
        _canvas.Children.Add(border);

        // Connector handle (drag to create an FS link).
        var dot = new Ellipse
        {
            Width = 14, Height = 14,
            Fill = new SolidColorBrush(accent),
            Stroke = new SolidColorBrush(Colors.White),
            StrokeThickness = 1.5,
            Tag = a.Id
        };
        ToolTipService.SetToolTip(dot, "Drag to a successor to link (Finish→Start)");
        Canvas.SetLeft(dot, a.X + NodeW - 7);
        Canvas.SetTop(dot, a.Y + NodeH / 2 - 7);
        dot.PointerPressed += Handle_PointerPressed;
        dot.PointerReleased += Handle_PointerReleased;
        _canvas.Children.Add(dot);
    }

    private void DrawEdge(Rect pr, Rect sr, Color color, ActivityLink link)
    {
        double x0 = pr.X + pr.Width, y0 = pr.Y + pr.Height / 2;
        double x1 = sr.X, y1 = sr.Y + sr.Height / 2;
        double midx = x0 + Math.Max(16, (x1 - x0) / 2);
        var brush = new SolidColorBrush(color);
        _canvas.Children.Add(new Polyline
        {
            Stroke = brush,
            StrokeThickness = 1.6,
            Points = new PointCollection { new Point(x0, y0), new Point(midx, y0), new Point(midx, y1), new Point(x1, y1) }
        });
        _canvas.Children.Add(new Polygon
        {
            Fill = brush,
            Points = new PointCollection { new Point(x1, y1), new Point(x1 - 8, y1 - 4), new Point(x1 - 8, y1 + 4) }
        });
        if (link.Type != DependencyType.FS || Math.Abs(link.LagDays) > 1e-6)
        {
            var tag = new TextBlock
            {
                Text = link.Type + (Math.Abs(link.LagDays) > 1e-6 ? $"{(link.LagDays >= 0 ? "+" : "")}{link.LagDays:0.#}" : ""),
                FontSize = 8, Foreground = FaintBrush
            };
            Canvas.SetLeft(tag, midx + 2);
            Canvas.SetTop(tag, (y0 + y1) / 2 - 8);
            _canvas.Children.Add(tag);
        }
    }

    // ---- node move / select ----
    private void Node_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_linkFromId is not null) return;
        if (sender is not Border b || b.Tag is not string id) return;
        _dragId = id;
        _moved = false;
        _dragPointerStart = e.GetCurrentPoint(_canvas).Position;
        _dragNodeStart = new Point(Canvas.GetLeft(b), Canvas.GetTop(b));
        b.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void Node_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragId is null || sender is not Border b) return;
        var p = e.GetCurrentPoint(_canvas).Position;
        double dx = p.X - _dragPointerStart.X, dy = p.Y - _dragPointerStart.Y;
        if (!_moved && Math.Abs(dx) + Math.Abs(dy) < 4) return;
        _moved = true;
        double nx = Math.Max(0, _dragNodeStart.X + dx);
        double ny = Math.Max(0, _dragNodeStart.Y + dy);
        Canvas.SetLeft(b, nx);
        Canvas.SetTop(b, ny);
        if (_schedule?.Find(_dragId) is { } a)
        {
            a.X = nx; a.Y = ny;
            Render(_schedule); // redraw edges + handle follows
        }
    }

    private void Node_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border b) b.ReleasePointerCapture(e.Pointer);
        if (_dragId is null) return;
        var id = _dragId;
        _dragId = null;
        if (_moved && _schedule?.Find(id) is { } a)
            NodeMoved?.Invoke(id, a.X, a.Y);
        else
        {
            _selectedId = id;
            SelectionChanged?.Invoke(id);
            if (_schedule is not null) Render(_schedule);
        }
    }

    // ---- link drag ----
    private void Handle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Ellipse dot || dot.Tag is not string id) return;
        _linkFromId = id;
        var p = e.GetCurrentPoint(_canvas).Position;
        _tempLink = new Line
        {
            X1 = p.X, Y1 = p.Y, X2 = p.X, Y2 = p.Y,
            Stroke = new SolidColorBrush(Colors.Orange),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            IsHitTestVisible = false
        };
        _canvas.Children.Add(_tempLink);
        dot.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_linkFromId is null || _tempLink is null) return;
        var p = e.GetCurrentPoint(_canvas).Position;
        _tempLink.X2 = p.X;
        _tempLink.Y2 = p.Y;
    }

    private void Handle_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Ellipse dot) dot.ReleasePointerCapture(e.Pointer);
        if (_linkFromId is null) return;
        var from = _linkFromId;
        _linkFromId = null;
        if (_tempLink is not null) { _canvas.Children.Remove(_tempLink); _tempLink = null; }

        var p = e.GetCurrentPoint(_canvas).Position;
        string? target = null;
        foreach (var kv in _rects)
            if (kv.Key != from && kv.Value.Contains(p)) { target = kv.Key; break; }
        if (target is not null)
            LinkRequested?.Invoke(from, target);
    }

    public void SetSelected(string? id)
    {
        _selectedId = id;
        if (_schedule is not null) Render(_schedule);
    }
}
