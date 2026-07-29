// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace BBSApp.Services;

public sealed class GenTable
{
    [JsonPropertyName("headers")] public List<string> Headers { get; set; } = new();
    [JsonPropertyName("rows")] public List<List<string>> Rows { get; set; } = new();
}

public sealed class GenResult
{
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("bbs")] public GenTable Bbs { get; set; } = new();
    [JsonPropertyName("summary")] public GenTable Summary { get; set; } = new();
    [JsonPropertyName("checks")] public GenTable Checks { get; set; } = new();
}

public static class EngineClient
{
    private const string Dll = "bbs_engine.dll";

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void bbs_free(IntPtr p);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int bbs_generate(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string kind,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? settingsJson,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? rowsJson,
        out IntPtr outJson);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private static extern int bbs_load_project(string path, out IntPtr outJson);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private static extern int bbs_save_project(string path, [MarshalAs(UnmanagedType.LPUTF8Str)] string projectJson, out IntPtr outError);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private static extern int bbs_export_csv(string path,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string headersJson,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string rowsJson,
        out IntPtr outError);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private static extern int bbs_export_html(string path,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string projectJson,
        out IntPtr outError);

    private static string? PtrToUtf8(IntPtr p)
    {
        if (p == IntPtr.Zero) return null;
        try
        {
            var len = 0;
            while (Marshal.ReadByte(p, len) != 0) len++;
            var bytes = new byte[len];
            Marshal.Copy(p, bytes, 0, len);
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            bbs_free(p);
        }
    }

    public static GenResult Generate(string kind, JsonObject settings, IEnumerable<Dictionary<string, string>> rows)
    {
        var rowsArr = new JsonArray();
        foreach (var row in rows)
        {
            var o = new JsonObject();
            foreach (var kv in row) o[kv.Key] = kv.Value;
            rowsArr.Add(o);
        }
        var settingsJson = settings.ToJsonString();
        var rowsJson = rowsArr.ToJsonString();
        var rc = bbs_generate(kind, settingsJson, rowsJson, out var ptr);
        var text = PtrToUtf8(ptr) ?? "{\"ok\":false,\"error\":\"Empty response\"}";
        var result = JsonSerializer.Deserialize<GenResult>(text) ?? new GenResult { Ok = false, Error = "Bad JSON" };
        // Native API: 1 = success, 0 = failure. Prefer JSON ok/error; force fail if rc disagrees.
        if (rc == 0)
        {
            result.Ok = false;
            if (string.IsNullOrWhiteSpace(result.Error))
                result.Error = "Generate failed.";
        }
        return result;
    }

    public static JsonObject? LoadProject(string path, out string? error)
    {
        error = null;
        var rc = bbs_load_project(path, out var ptr);
        var text = PtrToUtf8(ptr);
        if (text is null) { error = "Empty response"; return null; }
        if (rc == 0)
        {
            try
            {
                var node = JsonNode.Parse(text) as JsonObject;
                error = node?["error"]?.GetValue<string>() ?? text;
            }
            catch { error = text; }
            return null;
        }
        return JsonNode.Parse(text) as JsonObject;
    }

    public static bool SaveProject(string path, JsonObject project, out string? error)
    {
        error = null;
        var rc = bbs_save_project(path, project.ToJsonString(), out var ptr);
        var err = PtrToUtf8(ptr);
        if (rc == 0) { error = err ?? "Save failed"; return false; }
        return true;
    }

    public static bool ExportCsv(string path, IList<string> headers, IList<IList<string>> rows, out string? error)
    {
        error = null;
        var hdr = JsonSerializer.Serialize(headers);
        var body = JsonSerializer.Serialize(rows);
        var rc = bbs_export_csv(path, hdr, body, out var ptr);
        var err = PtrToUtf8(ptr);
        if (rc == 0) { error = err ?? "Export failed"; return false; }
        return true;
    }

    public static bool ExportHtml(string path, JsonObject project, out string? error)
    {
        error = null;
        var rc = bbs_export_html(path, project.ToJsonString(), out var ptr);
        var err = PtrToUtf8(ptr);
        if (rc == 0) { error = err ?? "Export failed"; return false; }
        return true;
    }
}
