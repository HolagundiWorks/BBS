using System.Text.Json;
using System.Text.Json.Nodes;

namespace BBSApp.Services;

/// <summary>App-level Foundry Local connection prefs (LocalAppData).</summary>
public sealed class FoundrySettings
{
    public string Endpoint { get; set; } = ""; // empty = auto-discover via `foundry status`
    public string ModelAlias { get; set; } = "qwen3-vl-2b-instruct";
    public double ConfidenceThreshold { get; set; } = 0.72;
    public bool AutoLoadModel { get; set; } = true;

    static string StorePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AQC-Core", "foundry-settings.json");

    public static FoundrySettings Load()
    {
        try
        {
            var path = StorePath;
            if (!File.Exists(path)) return new FoundrySettings();
            var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
            if (root is null) return new FoundrySettings();
            return new FoundrySettings
            {
                Endpoint = root["endpoint"]?.GetValue<string>() ?? "",
                ModelAlias = root["model"]?.GetValue<string>() ?? "qwen3-vl-2b-instruct",
                ConfidenceThreshold = root["confidence"]?.GetValue<double>() ?? 0.72,
                AutoLoadModel = root["auto_load"]?.GetValue<bool>() ?? true
            };
        }
        catch { return new FoundrySettings(); }
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(StorePath)!;
        Directory.CreateDirectory(dir);
        var o = new JsonObject
        {
            ["endpoint"] = Endpoint ?? "",
            ["model"] = ModelAlias ?? "qwen3-vl-2b-instruct",
            ["confidence"] = ConfidenceThreshold,
            ["auto_load"] = AutoLoadModel
        };
        File.WriteAllText(StorePath, o.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}