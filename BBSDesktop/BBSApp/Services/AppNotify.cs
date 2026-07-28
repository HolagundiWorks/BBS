using Microsoft.UI.Xaml.Controls;

namespace BBSApp.Services;

public readonly record struct NotifyRequest(
    string Title,
    string Message,
    InfoBarSeverity Severity,
    int DurationMs);

/// <summary>App-wide toast notifications that auto-hide.</summary>
public static class AppNotify
{
    public static event Action<NotifyRequest>? Raised;

    public static void Show(
        string title,
        string message = "",
        InfoBarSeverity severity = InfoBarSeverity.Informational,
        int durationMs = 3800)
    {
        Raised?.Invoke(new NotifyRequest(title, message ?? "", severity, durationMs));
    }

    public static void Info(string title, string message = "", int durationMs = 3500) =>
        Show(title, message, InfoBarSeverity.Informational, durationMs);

    public static void Success(string title, string message = "", int durationMs = 3200) =>
        Show(title, message, InfoBarSeverity.Success, durationMs);

    public static void Warning(string title, string message = "", int durationMs = 4500) =>
        Show(title, message, InfoBarSeverity.Warning, durationMs);

    public static void Error(string title, string message = "", int durationMs = 6000) =>
        Show(title, message, InfoBarSeverity.Error, durationMs);
}
