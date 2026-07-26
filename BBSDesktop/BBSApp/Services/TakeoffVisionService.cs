using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace BBSApp.Services;

public sealed class VisionDetection
{
    public string Category { get; set; } = "columns";
    public string Tool { get; set; } = "Point";
    public double Confidence { get; set; }
    /// <summary>Points as percent of image width/height (0–100).</summary>
    public List<TakeoffPoint> PointsPct { get; } = new();
    public string? Note { get; set; }
    public bool Accepted { get; set; } = true;
}

public sealed class VisionDetectResult
{
    public bool Ok { get; init; }
    public string? Error { get; init; }
    public List<VisionDetection> HighConfidence { get; } = new();
    public List<VisionDetection> LowConfidence { get; } = new();
    public string? RawResponse { get; init; }
}

/// <summary>Qwen3-VL via Foundry Local → structural element proposals on the takeoff page.</summary>
public static class TakeoffVisionService
{
    public const string PendingField = "ai_pending";
    public const string ConfidenceField = "ai_confidence";

    public static async Task<VisionDetectResult> DetectAsync(
        ProjectStore store,
        FoundrySettings settings,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report("Resolving drawing…");
        var page = await RenderCurrentPageAsync(store, ct);
        if (page is null)
        {
            var pathHint = TakeoffAnnotatedPdf.ResolvePdfPath(store);
            return Fail(
                pathHint is null
                    ? "Import a PDF on Drawing takeoff first."
                    : $"Could not render PDF page for AI (path: {pathHint}). Re-import the PDF.");
        }

        progress?.Report($"Page image {page.Width:0}×{page.Height:0} ({page.Png.Length / 1024} KB)…");
        progress?.Report("Connecting to Foundry Local…");
        using var client = new FoundryLocalClient();
        if (!await client.ConnectAsync(settings, ct))
            return Fail(client.LastError ?? "Foundry connect failed. Use AI Start, then try again.");

        progress?.Report($"Resolving model {settings.ModelAlias}…");
        await client.EnsureModelLoadedAsync(settings.ModelAlias, settings.AutoLoadModel, ct);
        var ids = await client.ListModelIdsAsync(ct);
        string modelId = FoundryLocalClient.ResolveModelId(ids, settings.ModelAlias);
        if (ids.Count == 0)
            return Fail("No models loaded in Foundry. Run: foundry model load qwen3-vl-2b-instruct");
        if (!ids.Any(m => m.Contains("vl", StringComparison.OrdinalIgnoreCase)
                          || m.Contains(settings.ModelAlias, StringComparison.OrdinalIgnoreCase)))
        {
            return Fail(
                $"Vision model not loaded. Loaded: {string.Join(", ", ids)}. " +
                $"Run: foundry model load {settings.ModelAlias}");
        }

        progress?.Report($"Running {modelId} (may take 1–3 min on CPU)…");
        string? raw = await client.ChatVisionJsonAsync(modelId, BuildPrompt(), page.Png, ct);

        // The daemon drops all models when it restarts (Restart button, self-heal, or idle eviction),
        // so the model can vanish between load and request. Reload it once and retry.
        if (raw is null && FoundryLocalClient.IsModelNotLoaded(client.LastError))
        {
            progress?.Report($"Model was unloaded — reloading {settings.ModelAlias}…");
            var (loaded, loadMsg) = await client.LoadModelAsync(settings.ModelAlias, ct);
            if (!loaded)
                return Fail(loadMsg);
            var reIds = await client.ListModelIdsAsync(ct);
            modelId = FoundryLocalClient.ResolveModelId(reIds, settings.ModelAlias);
            progress?.Report($"Running {modelId} (may take 1–2 min on CPU)…");
            raw = await client.ChatVisionJsonAsync(modelId, BuildPrompt(), page.Png, ct);
        }

        // Out-of-memory (bad allocation) on CPU/low-RAM machines: re-render smaller and retry once.
        if (raw is null && client.LastErrorIsOom)
        {
            progress?.Report("Low memory — retrying at reduced resolution…");
            var smaller = await RenderCurrentPageAsync(store, ct, maxDim: 512);
            if (smaller is not null)
            {
                page = smaller;
                raw = await client.ChatVisionJsonAsync(modelId, BuildSimplePrompt(), page.Png, ct, maxTokens: 1024);
            }

            // Still OOM: the runtime's memory arena has likely grown and won't shrink. Restart the
            // Foundry service to reclaim it (this also unloads any extra models), reload the vision
            // model, and make one final small attempt on a fresh, minimal-footprint connection.
            if (raw is null && client.LastErrorIsOom)
            {
                progress?.Report("Restarting AI service to reclaim memory…");
                var (restarted, _) = await FoundryLocalClient.RestartDaemonAsync(ct);
                if (restarted)
                {
                    using var fresh = new FoundryLocalClient();
                    if (await fresh.ConnectAsync(settings, ct))
                    {
                        progress?.Report($"Reloading {settings.ModelAlias} on fresh service…");
                        await fresh.EnsureModelLoadedAsync(settings.ModelAlias, true, ct);
                        var freshIds = await fresh.ListModelIdsAsync(ct);
                        string freshId = FoundryLocalClient.ResolveModelId(freshIds, settings.ModelAlias);
                        var reRendered = await RenderCurrentPageAsync(store, ct, maxDim: 512);
                        if (reRendered is not null)
                        {
                            progress?.Report($"Running {freshId} (may take 1–2 min on CPU)…");
                            var raw2 = await fresh.ChatVisionJsonAsync(freshId, BuildSimplePrompt(), reRendered.Png, ct, maxTokens: 1024);
                            if (raw2 is not null)
                            {
                                WriteLastDiag(freshId, reRendered.Png.Length, raw2, "post-restart");
                                return BuildResult(ParseDetections(raw2), settings, raw2);
                            }
                        }
                    }
                }
                return Fail(
                    "Not enough memory even after restarting the AI service. The 4B vision model is tight on a " +
                    "16 GB, CPU-only machine. In Settings → AI / Foundry, switch the model to qwen3-vl-2b-instruct " +
                    "(about half the memory), or close other apps, then try again.");
            }
        }

        if (raw is null)
            return Fail(client.LastError ?? "No response from model.");

        WriteLastDiag(modelId, page.Png.Length, raw, null);

        var parsed = ParseDetections(raw);
        if (parsed.Count == 0)
        {
            // One retry with a simpler prompt
            progress?.Report("No elements parsed — retrying with simpler prompt…");
            raw = await client.ChatVisionJsonAsync(modelId, BuildSimplePrompt(), page.Png, ct, maxTokens: 1024);
            if (raw is null)
                return Fail(client.LastError ?? "Retry failed — no response.");
            WriteLastDiag(modelId, page.Png.Length, raw, "retry");
            parsed = ParseDetections(raw);
        }

        return BuildResult(parsed, settings, raw);
    }

    private static VisionDetectResult BuildResult(List<VisionDetection> parsed, FoundrySettings settings, string raw)
    {
        if (parsed.Count == 0)
        {
            string snippet = raw.Length > 280 ? raw[..280] + "…" : raw;
            return new VisionDetectResult
            {
                Ok = false,
                Error = "Model replied but no usable marks were parsed. Reply snippet: " + snippet.Replace('\n', ' '),
                RawResponse = raw
            };
        }

        double thr = Math.Clamp(settings.ConfidenceThreshold, 0.4, 0.95);
        var result = new VisionDetectResult { Ok = true, RawResponse = raw };
        foreach (var d in parsed)
        {
            if (d.Confidence >= thr) result.HighConfidence.Add(d);
            else result.LowConfidence.Add(d);
        }
        return result;
    }

    private static VisionDetectResult Fail(string error)
    {
        WriteLastDiag(null, 0, null, error);
        return new VisionDetectResult { Ok = false, Error = error };
    }

    private static void WriteLastDiag(string? model, int pngBytes, string? raw, string? note)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AQC-Core");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "ai-last-error.txt");
            File.WriteAllText(path,
                $"utc={DateTime.UtcNow:o}\nmodel={model}\npng_bytes={pngBytes}\nnote={note}\n---\n{raw ?? ""}");
        }
        catch { }
    }

    public static TakeoffItem ToTakeoffItem(VisionDetection d, ProjectStore store, string level, double canvasW, double canvasH)
    {
        var profile = ElementPickProfile.Find(d.Category.Equals("openings", StringComparison.OrdinalIgnoreCase) ? "masonry" : d.Category) ?? ElementPickProfile.All[0];
        string tool = string.IsNullOrWhiteSpace(d.Tool)
            ? profile.PickMode switch
            {
                TakeoffPickMode.Point => "Point",
                TakeoffPickMode.Area => "Area",
                _ => "Line"
            }
            : d.Tool;

        var pixelPts = d.PointsPct.Select(p => new TakeoffPoint
        {
            X = p.X / 100.0 * canvasW,
            Y = p.Y / 100.0 * canvasH
        }).ToList();

        double lengthMm = 0;
        if (tool.Equals("Line", StringComparison.OrdinalIgnoreCase) && pixelPts.Count >= 2 && store.Takeoff.MmPerPx > 0)
        {
            for (int i = 1; i < pixelPts.Count; i++)
            {
                var a = pixelPts[i - 1];
                var b = pixelPts[i];
                lengthMm += Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y)) * store.Takeoff.MmPerPx;
            }
        }

        bool isOpening = d.Category.Equals("openings", StringComparison.OrdinalIgnoreCase)
                         || tool.Equals("Opening", StringComparison.OrdinalIgnoreCase);
        string category = isOpening ? "masonry" : profile.Category;
        string mark = TakeoffState.NextMark(store, category, level);
        if (isOpening)
        {
            tool = "Opening";
            mark += "-OP";
        }

        var item = new TakeoffItem
        {
            Category = category,
            Level = level,
            Mark = mark,
            Tool = tool,
            LengthMm = lengthMm,
            MappedField = profile.PrimaryField,
            Committed = false
        };
        item.Points.AddRange(pixelPts);
        item.Fields[PendingField] = "1";
        item.Fields[ConfidenceField] = d.Confidence.ToString("0.###", CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(d.Note))
            item.Fields["ai_note"] = d.Note!;
        if (lengthMm > 0)
            item.Fields[profile.PrimaryField] = lengthMm.ToString("0.#", CultureInfo.InvariantCulture);
        return item;
    }

    public static void FinalizeItem(TakeoffItem item) => item.Fields.Remove(PendingField);

    private static string BuildPrompt() =>
        """
This image is a building plan. Detect structural / masonry marks you can see.

Reply with ONLY one JSON object (no markdown fences, no explanation):
{"elements":[{"category":"columns","tool":"Point","confidence":0.85,"points_pct":[{"x":12.5,"y":40.0}],"note":""}]}

Allowed category: columns, beams, pedestals, lintels, slabs, footings, masonry, openings
Allowed tool: Point (columns/pedestals), Line (beams/lintels/masonry), Area (slabs/footings), Opening (doors/windows)
points_pct x,y are percentages of image width/height (0-100), origin top-left.
confidence 0-1. Prefer fewer accurate marks. Skip title blocks and dimensions text.
If unsure, still return best-guess columns and walls with lower confidence.
""";

    private static string BuildSimplePrompt() =>
        """
List column centres and wall lines on this plan as JSON only:
{"elements":[{"category":"columns","tool":"Point","confidence":0.7,"points_pct":[{"x":10,"y":20}]}]}
Use category columns (Point) or masonry (Line with 2 points_pct). Coordinates are % of width/height 0-100.
""";

    private sealed record PageBitmap(byte[] Png, float Width, float Height);

    private static async Task<PageBitmap?> RenderCurrentPageAsync(ProjectStore store, CancellationToken ct, int maxDim = 768)
    {
        var path = TakeoffAnnotatedPdf.ResolvePdfPath(store);
        if (path is null || !File.Exists(path)) return null;
        try
        {
            PdfDocument pdf;
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(path);
                pdf = await PdfDocument.LoadFromFileAsync(file);
            }
            catch
            {
                using var ras = await FileRandomAccessStream.OpenAsync(path, FileAccessMode.Read);
                pdf = await PdfDocument.LoadFromStreamAsync(ras);
            }

            if (pdf.PageCount == 0) return null;
            uint pageIndex = (uint)Math.Clamp(store.Takeoff.Page, 0, (int)pdf.PageCount - 1);
            using var pdfPage = pdf.GetPage(pageIndex);
            // Keep VL payload small — large pages time out / run out of memory on CPU.
            // Vision-encoder memory scales ~with pixel area, so maxDim is the main OOM lever.
            double scale = Math.Min(1.5, maxDim / Math.Max(pdfPage.Size.Width, pdfPage.Size.Height));
            float destW = (float)Math.Max(64, pdfPage.Size.Width * scale);
            float destH = (float)Math.Max(64, pdfPage.Size.Height * scale);
            var opts = new PdfPageRenderOptions
            {
                DestinationWidth = (uint)Math.Round(destW),
                DestinationHeight = (uint)Math.Round(destH)
            };
            using var stream = new InMemoryRandomAccessStream();
            await pdfPage.RenderToStreamAsync(stream, opts).AsTask(ct);
            stream.Seek(0);
            var reader = new DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync((uint)stream.Size);
            var bytes = new byte[stream.Size];
            reader.ReadBytes(bytes);
            reader.Dispose();
            return new PageBitmap(bytes, destW, destH);
        }
        catch (Exception ex)
        {
            WriteLastDiag(null, 0, null, "render: " + ex.Message);
            return null;
        }
    }

    private static List<VisionDetection> ParseDetections(string raw)
    {
        var list = new List<VisionDetection>();
        string json = ExtractJsonObject(raw);
        if (string.IsNullOrWhiteSpace(json)) return list;
        try
        {
            var root = JsonNode.Parse(json) as JsonObject;
            var arr = root?["elements"] as JsonArray
                      ?? root?["detections"] as JsonArray
                      ?? root?["items"] as JsonArray;
            if (arr is null) return list;
            foreach (var node in arr)
            {
                if (node is not JsonObject o) continue;
                string cat = NormalizeCategory(o["category"]?.GetValue<string>() ?? o["type"]?.GetValue<string>() ?? "");
                if (string.IsNullOrEmpty(cat)) continue;
                string tool = o["tool"]?.GetValue<string>() ?? "";
                double conf = 0.55;
                if (o["confidence"] is JsonValue jv)
                {
                    try { conf = jv.GetValue<double>(); } catch { }
                }
                conf = Math.Clamp(conf, 0, 1);

                var det = new VisionDetection
                {
                    Category = cat,
                    Tool = string.IsNullOrWhiteSpace(tool) ? DefaultTool(cat) : tool,
                    Confidence = conf,
                    Note = o["note"]?.GetValue<string>() ?? o["label"]?.GetValue<string>()
                };

                var pts = o["points_pct"] as JsonArray
                          ?? o["points"] as JsonArray
                          ?? o["bbox_pct"] as JsonArray;
                if (pts is not null)
                {
                    foreach (var p in pts)
                    {
                        if (p is not JsonObject po) continue;
                        double x = Num(po, "x");
                        double y = Num(po, "y");
                        if (Math.Abs(x) <= 1.5 && Math.Abs(y) <= 1.5)
                        {
                            x *= 100;
                            y *= 100;
                        }
                        det.PointsPct.Add(new TakeoffPoint { X = x, Y = y });
                    }
                }

                if (det.PointsPct.Count == 0 && o["box_pct"] is JsonObject box)
                {
                    double x = Num(box, "x"), y = Num(box, "y"), w = Num(box, "w"), h = Num(box, "h");
                    det.PointsPct.Add(new TakeoffPoint { X = x, Y = y });
                    det.PointsPct.Add(new TakeoffPoint { X = x + w, Y = y + h });
                    if (string.IsNullOrWhiteSpace(tool))
                        det.Tool = cat is "slabs" or "footings" ? "Area" : "Opening";
                }

                // cx,cy style
                if (det.PointsPct.Count == 0 && (o.ContainsKey("cx") || o.ContainsKey("x")))
                {
                    double x = Num(o, "cx"); if (x == 0) x = Num(o, "x");
                    double y = Num(o, "cy"); if (y == 0) y = Num(o, "y");
                    if (Math.Abs(x) <= 1.5 && Math.Abs(y) <= 1.5) { x *= 100; y *= 100; }
                    if (x != 0 || y != 0)
                        det.PointsPct.Add(new TakeoffPoint { X = x, Y = y });
                }

                if (det.PointsPct.Count == 0) continue;
                if (!IsGeometryOk(det)) continue;
                list.Add(det);
            }
        }
        catch { }
        return list;
    }

    private static bool IsGeometryOk(VisionDetection d)
    {
        string t = d.Tool;
        if (t.Equals("Point", StringComparison.OrdinalIgnoreCase)) return d.PointsPct.Count >= 1;
        if (t.Equals("Line", StringComparison.OrdinalIgnoreCase)) return d.PointsPct.Count >= 2;
        if (t.Equals("Area", StringComparison.OrdinalIgnoreCase)) return d.PointsPct.Count >= 3 || d.PointsPct.Count == 2;
        if (t.Equals("Opening", StringComparison.OrdinalIgnoreCase)) return d.PointsPct.Count >= 2;
        return d.PointsPct.Count >= 1;
    }

    private static string DefaultTool(string cat) => cat switch
    {
        "columns" or "pedestals" => "Point",
        "slabs" or "footings" => "Area",
        "openings" => "Opening",
        _ => "Line"
    };

    private static string NormalizeCategory(string raw)
    {
        var s = raw.Trim().ToLowerInvariant();
        return s switch
        {
            "column" or "columns" or "rcc column" or "c" or "col" => "columns",
            "beam" or "beams" or "rb" or "pb" => "beams",
            "pedestal" or "pedestals" => "pedestals",
            "lintel" or "lintels" => "lintels",
            "slab" or "slabs" => "slabs",
            "footing" or "footings" or "foundation" => "footings",
            "wall" or "walls" or "masonry" or "brick" or "brickwork" => "masonry",
            "opening" or "openings" or "door" or "window" or "doors" or "windows" => "openings",
            _ => ElementPickProfile.Find(s)?.Category ?? ""
        };
    }

    private static string ExtractJsonObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        // Strip Qwen think / reasoning blocks
        raw = Regex.Replace(raw, @"<think>[\s\S]*?</think>", "", RegexOptions.IgnoreCase);
        raw = Regex.Replace(raw, @"<reasoning>[\s\S]*?</reasoning>", "", RegexOptions.IgnoreCase);
        var fence = Regex.Match(raw, @"```(?:json)?\s*([\s\S]*?)```", RegexOptions.IgnoreCase);
        if (fence.Success) raw = fence.Groups[1].Value;
        int start = raw.IndexOf('{');
        int end = raw.LastIndexOf('}');
        if (start < 0 || end <= start) return "";
        return raw[start..(end + 1)];
    }

    private static double Num(JsonObject o, string key)
    {
        try { return o[key]?.GetValue<double>() ?? 0; }
        catch
        {
            if (double.TryParse(o[key]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                return d;
            return 0;
        }
    }
}