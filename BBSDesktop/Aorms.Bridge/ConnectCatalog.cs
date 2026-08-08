// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aorms.Bridge;

/// <summary>
/// Shared project catalog owned by AORMS Connect.
/// Path: %LocalAppData%\AORMS-Connect\catalog.json
/// Canon: esti docs/esti/AORMS-CONNECT.md (C2).
/// </summary>
public sealed class CatalogProject
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("ref")]
    public string Ref { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "ACTIVE";

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = "";
}

public static class ConnectCatalog
{
    public static string DefaultPath() =>
        Path.Combine(ConnectSession.DefaultDirectory(), "catalog.json");

    public static IReadOnlyList<CatalogProject> List(string? path = null)
    {
        path ??= DefaultPath();
        if (!File.Exists(path)) return Array.Empty<CatalogProject>();
        try
        {
            var rows = JsonSerializer.Deserialize<List<CatalogProject>>(File.ReadAllText(path));
            return rows ?? new List<CatalogProject>();
        }
        catch
        {
            return Array.Empty<CatalogProject>();
        }
    }

    public static CatalogProject? FindById(string projectId, string? path = null) =>
        List(path).FirstOrDefault(p =>
            string.Equals(p.Id, projectId, StringComparison.OrdinalIgnoreCase));

    public static CatalogProject? FindByRef(string projectRef, string? path = null) =>
        List(path).FirstOrDefault(p =>
            string.Equals(p.Ref, projectRef, StringComparison.OrdinalIgnoreCase));
}
