namespace BBSApp.Services.Ai;

/// <summary>
/// Persists the assistant's Anthropic API key under %LocalAppData%\AQCCore\
/// (the same per-user folder the app already uses for rate books and logos).
/// This is convenience storage scoped to the current Windows profile — not
/// encryption; treat the key like any other local credential.
/// </summary>
public static class AssistantSettings
{
    private static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AQCCore");

    private static string KeyFile => Path.Combine(Dir, "assistant.key");

    /// <summary>The stored API key, or null if none is set. Setting null/blank clears it.</summary>
    public static string? ApiKey
    {
        get
        {
            try
            {
                if (!File.Exists(KeyFile)) return null;
                var text = File.ReadAllText(KeyFile).Trim();
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
            catch
            {
                return null;
            }
        }
        set
        {
            try
            {
                Directory.CreateDirectory(Dir);
                if (string.IsNullOrWhiteSpace(value))
                {
                    if (File.Exists(KeyFile)) File.Delete(KeyFile);
                }
                else
                {
                    File.WriteAllText(KeyFile, value.Trim());
                }
            }
            catch
            {
                // Best effort — the assistant simply stays disabled if this fails.
            }
        }
    }
}
