// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aorms.Bridge;

/// <summary>
/// Shared session file written by AORMS Connect after Activate/login.
/// Canon: esti docs/esti/AORMS-CONNECT.md (C2).
/// Default: %LocalAppData%\AORMS-Connect\session.json
/// CLI: --connect-session &lt;path&gt;
/// </summary>
public sealed class ConnectSessionFile
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("syncToken")]
    public string SyncToken { get; set; } = "";

    [JsonPropertyName("hubUrl")]
    public string HubUrl { get; set; } = "";

    [JsonPropertyName("licenseApiUrl")]
    public string? LicenseApiUrl { get; set; }

    [JsonPropertyName("licenseToken")]
    public string? LicenseToken { get; set; }

    [JsonPropertyName("deviceId")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("writtenAt")]
    public string WrittenAt { get; set; } = "";

    [JsonPropertyName("expiresAt")]
    public string? ExpiresAt { get; set; }
}

public static class ConnectSession
{
    public const string FlagConnectSession = "--connect-session";

    public static string DefaultDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AORMS-Connect");

    public static string DefaultPath() => Path.Combine(DefaultDirectory(), "session.json");

    public static string ResolvePath(string[]? args = null)
    {
        args ??= Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], FlagConnectSession, StringComparison.OrdinalIgnoreCase))
            {
                var p = args[i + 1].Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(p)) return p;
            }
        }
        return DefaultPath();
    }

    public static void Write(ConnectSessionFile session, string? path = null)
    {
        path ??= DefaultPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (string.IsNullOrWhiteSpace(session.WrittenAt))
            session.WrittenAt = DateTimeOffset.UtcNow.ToString("O");
        var json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public static ConnectSessionFile? TryRead(string? path = null)
    {
        path ??= DefaultPath();
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            var file = JsonSerializer.Deserialize<ConnectSessionFile>(json);
            if (file is null || string.IsNullOrWhiteSpace(file.SyncToken)) return null;
            if (!string.IsNullOrWhiteSpace(file.ExpiresAt) &&
                DateTimeOffset.TryParse(file.ExpiresAt, out var exp) &&
                exp < DateTimeOffset.UtcNow)
                return null;
            return file;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Import Connect session into this app's firm.db when a syncToken is present.
    /// Does not clear an existing newer local token unless overwrite is true.
    /// </summary>
    public static bool TryApplyToFirmDb(FirmDb db, string installId, string? path = null, bool overwrite = false)
    {
        var session = TryRead(path);
        if (session is null) return false;
        var (existing, _, _) = db.ReadAuth();
        if (!overwrite && !string.IsNullOrWhiteSpace(existing)) return false;

        db.UpsertSettings(
            string.IsNullOrWhiteSpace(session.DeviceId) ? installId : session.DeviceId!,
            session.LicenseToken,
            session.SyncToken,
            string.IsNullOrWhiteSpace(session.HubUrl) ? null : session.HubUrl.TrimEnd('/'),
            string.IsNullOrWhiteSpace(session.LicenseApiUrl) ? null : session.LicenseApiUrl.TrimEnd('/'),
            "ACTIVE");
        return true;
    }
}
