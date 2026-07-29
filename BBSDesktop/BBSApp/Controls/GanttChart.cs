// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using BBSApp.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace BBSApp.Controls;

/// <summary>Working-day Gantt: bars per activity, FS dependency arrows, critical path in red,
/// %-complete overlay, float whiskers, a dated axis and a "today" marker.</summary>
public sealed class GanttChart : UserControl
{
    private readonly Canvas _canvas = new();

    public double PxPerDay { get; set; } = 24;
    public double RowHeight { get; set; } = 28;
    public double LabelWidth { get; set; } = 220;
    private const double HeaderH = 44;

    private static readonly Color Critical = Color.FromArgb(255, 214, 69, 69);
    private static readonly Color Normal = Color.FromArgb(255, 66, 133, 210);

    public GanttChart()
    {
        _canvas.Background = new SolidColorBrush(Colors.Transparent);
        Content = _canvas;
    }

    private Brush TextBrush => Res("TextFillColorPrimaryBrush", Color.FromArgb(255, 230, 230, 230));
    private Brush FaintBrush => Res("TextFillColorSecondaryBrush", Color.FromArgb(255, 150, 150, 150));

    private static Brush Res(string key, Color fallback) =>
        Application.Current.Resources.TryGetValue(key, out var v) && v is Brush b
            ? b : new SolidColorBrush(fallback);

    public void Render(ProjectSchedule schedule)
    {
        _canvas.Children.Clear();
        var result = ScheduleCalculator.Compute(schedule);
        var acts = schedule.Activities;

        int totalDays = Math.Max(1, (int)Math.Ceiling(result.ProjectDurationDays) + 1);
        double chartLeft = LabelWidth;
        double width = chartLeft + (totalDays + 1) * PxPerDay + 20;
        double height = HeaderH + acts.Count * RowHeight + 24;
        _canvas.Width = width;
        _canvas.Height = height;

        var gridColor = Color.FromArgb(60, 130, 130, 130);
        var weekendColor = Color.FromArgb(28, 130, 130, 130);

        // Column shading + date axis (one column per working day).
        for (int d = 0; d <= totalDays; d++)
        {
            double x = chartLeft + d * PxPerDay;
            var date = schedule.DateForOffset(d);
            var line = new Line
            {
                X1 = x, Y1 = HeaderH - 6, X2 = x, Y2 = height - 6,
                Stroke = new SolidColorBrush(gridColor), StrokeThickness = 1
            };
            _canvas.Children.Add(line);
            if (d % 5 == 0 || d == 0)
            {
                var lbl = new TextBlock
                {
                    Text = date.ToString("dd MMM"),
                    FontSize = 10,
                    Foreground = FaintBrush
                };
                Canvas.SetLeft(lbl, x + 2);
                Canvas.SetTop(lbl, 6);
                _canvas.Children.Add(lbl);
            }
        }

        // Today marker.
        double todayOffset = WorkingDaysBetween(schedule, schedule.StartDate, DateTime.Today);
        if (todayOffset >= 0 && todayOffset <= totalDays)
        {
            double tx = chartLeft + todayOffset * PxPerDay;
            _canvas.Children.Add(new Line
            {
                X1 = tx, Y1 = HeaderH - 10, X2 = tx, Y2 = height - 6,
                Stroke = new SolidColorBrush(Color.FromArgb(200, 40, 170, 90)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 3, 3 }
            });
        }

        // Row geometry cache for arrows.
        var barGeom = new Dictionary<string, (double x0, double x1, double yMid)>();

        for (int i = 0; i < acts.Count; i++)
        {
            var a = acts[i];
            double rowTop = HeaderH + i * RowHeight;
            double yMid = rowTop + RowHeight / 2;

            // Name label
            var name = new TextBlock
            {
                Text = $"{i + 1}. {a.Name}",
                FontSize = 12,
                Foreground = a.InCycle ? new SolidColorBrush(Critical) : TextBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Width = LabelWidth - 10,
                MaxLines = 1
            };
            Canvas.SetLeft(name, 4);
            Canvas.SetTop(name, rowTop + 5);
            _canvas.Children.Add(name);

            double x0 = chartLeft + a.EarlyStart * PxPerDay;
            double x1 = chartLeft + a.EarlyFinish * PxPerDay;
            double barTop = rowTop + 6;
            double barH = RowHeight - 12;
            barGeom[a.Id] = (x0, x1, yMid);

            var color = a.IsCritical ? Critical : Normal;

            if (a.IsMilestone)
            {
                double s = barH;
                var dia = new Polygon
                {
                    Fill = new SolidColorBrush(color),
                    Points = new PointCollection
                    {
                        new Point(x0, barTop), new Point(x0 + s / 2, barTop + s / 2),
                        new Point(x0, barTop + s), new Point(x0 - s / 2, barTop + s / 2)
                    }
                };
                _canvas.Children.Add(dia);
            }
            else
            {
                double w = Math.Max(2, x1 - x0);
                var bar = new Rectangle
                {
                    Width = w, Height = barH, RadiusX = 3, RadiusY = 3,
                    Fill = new SolidColorBrush(Color.FromArgb(150, color.R, color.G, color.B)),
                    Stroke = new SolidColorBrush(color), StrokeThickness = 1
                };
                Canvas.SetLeft(bar, x0);
                Canvas.SetTop(bar, barTop);
                _canvas.Children.Add(bar);

                if (a.PercentComplete > 0)
                {
                    var done = new Rectangle
                    {
                        Width = Math.Max(1, w * Math.Clamp(a.PercentComplete, 0, 100) / 100.0),
                        Height = barH, RadiusX = 3, RadiusY = 3,
                        Fill = new SolidColorBrush(color)
                    };
                    Canvas.SetLeft(done, x0);
                    Canvas.SetTop(done, barTop);
                    _canvas.Children.Add(done);
                }

                // Total-float whisker
                if (a.TotalFloat > 1e-3 && !a.InCycle)
                {
                    double fx = chartLeft + a.LateFinish * PxPerDay;
                    _canvas.Children.Add(new Line
                    {
                        X1 = x1, Y1 = yMid, X2 = fx, Y2 = yMid,
                        Stroke = FaintBrush, StrokeThickness = 1,
                        StrokeDashArray = new DoubleCollection { 2, 2 }
                    });
                }

                var meta = new TextBlock
                {
                    Text = a.InCycle ? "cycle!" : $"{a.DurationDays:0.#}d" + (a.TotalFloat > 1e-3 ? $"  fl {a.TotalFloat:0.#}" : ""),
                    FontSize = 9,
                    Foreground = FaintBrush
                };
                Canvas.SetLeft(meta, x1 + 6);
                Canvas.SetTop(meta, barTop);
                _canvas.Children.Add(meta);
            }
        }

        // FS dependency arrows (predecessor finish → successor start).
        foreach (var a in acts)
        {
            if (!barGeom.TryGetValue(a.Id, out var sg)) continue;
            foreach (var l in a.Links)
            {
                if (!barGeom.TryGetValue(l.PredecessorId, out var pg)) continue;
                var col = (a.IsCritical && schedule.Find(l.PredecessorId)?.IsCritical == true)
                    ? Critical : Color.FromArgb(160, 150, 150, 150);
                DrawArrow(pg.x1, pg.yMid, sg.x0, sg.yMid, col);
            }
        }
    }

    private void DrawArrow(double x0, double y0, double x1, double y1, Color c)
    {
        var brush = new SolidColorBrush(c);
        double midx = x0 + 8;
        var poly = new Polyline
        {
            Stroke = brush, StrokeThickness = 1.4,
            Points = new PointCollection { new Point(x0, y0), new Point(midx, y0), new Point(midx, y1), new Point(x1, y1) }
        };
        _canvas.Children.Add(poly);
        // arrowhead at (x1,y1)
        var head = new Polygon
        {
            Fill = brush,
            Points = new PointCollection
            {
                new Point(x1, y1), new Point(x1 - 6, y1 - 3), new Point(x1 - 6, y1 + 3)
            }
        };
        _canvas.Children.Add(head);
    }

    private static int WorkingDaysBetween(ProjectSchedule s, DateTime from, DateTime to)
    {
        if (to < from) return -1;
        int n = 0;
        var d = from;
        int guard = 0;
        while (d.Date < to.Date && guard++ < 100000)
        {
            d = d.AddDays(1);
            if (s.IsWorkingDay(d)) n++;
        }
        return n;
    }
}
