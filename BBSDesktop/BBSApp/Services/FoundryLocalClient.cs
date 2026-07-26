using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace BBSApp.Services;

public sealed class FoundryDaemonStatus
{
    public bool Running { get; init; }
    public string State { get; init; } = "unknown";
    public int? Pid { get; init; }
    public string? Endpoint { get; init; }
    public string? Uptime { get; init; }
    public int LoadedModels { get; init; }
    public string Summary { get; init; } = "";
    public string? Error { get; init; }

    public string StatusLabel => !string.IsNullOrEmpty(Error)
        ? "Error"
        : Running
            ? (State.Equals("ready", StringComparison.OrdinalIgnoreCase) ? "Running" : State)
            : "Stopped";
}

public sealed class FoundryLocalClient : IDisposable
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(8) };
    private string _baseUrl = "";

    public string BaseUrl => _baseUrl;
    public string? LastError { get; private set; }

    /// <summary>True when the last call failed with an out-of-memory / bad-allocation error from the runtime.</summary>
    public bool LastErrorIsOom { get; private set; }

    /// <summary>Heuristic: does this Foundry error text indicate the CPU/GPU runtime ran out of memory?</summary>
    public static bool IsOutOfMemory(string? text) =>
        !string.IsNullOrEmpty(text)
        && (text.Contains("bad allocation", StringComparison.OrdinalIgnoreCase)
            || text.Contains("bad_alloc", StringComparison.OrdinalIgnoreCase)
            || text.Contains("out of memory", StringComparison.OrdinalIgnoreCase)
            || text.Contains("insufficient memory", StringComparison.OrdinalIgnoreCase)
            || text.Contains("OOM", StringComparison.Ordinal)
            || text.Contains("allocate", StringComparison.OrdinalIgnoreCase));

    public static async Task<FoundryDaemonStatus> GetDaemonStatusAsync(CancellationToken ct = default)
    {
        try
        {
            var (code, stdout, stderr) = await RunFoundryAsync("server status -o json", ct);
            if (code != 0 && string.IsNullOrWhiteSpace(stdout))
            {
                return new FoundryDaemonStatus
                {
                    Running = false,
                    State = "stopped",
                    Summary = "AI service stopped",
                    Error = string.IsNullOrWhiteSpace(stderr) ? null : Trim(stderr, 200)
                };
            }

            var root = JsonNode.Parse(stdout) as JsonObject;
            bool running = root?["running"]?.GetValue<bool>() ?? false;
            string state = root?["state"]?.GetValue<string>() ?? (running ? "ready" : "stopped");
            int? pid = null;
            try { pid = root?["pid"]?.GetValue<int>(); } catch { }
            string? uptime = root?["uptime"]?.GetValue<string>();
            string? endpoint = (root?["webUrls"] as JsonArray)?.FirstOrDefault()?.GetValue<string>();

            int loaded = 0;
            if (running && !string.IsNullOrWhiteSpace(endpoint))
            {
                try
                {
                    using var resp = await Http.GetAsync($"{endpoint.TrimEnd('/')}/v1/models", ct);
                    if (resp.IsSuccessStatusCode)
                    {
                        var body = await resp.Content.ReadAsStringAsync(ct);
                        var models = JsonNode.Parse(body) as JsonObject;
                        loaded = (models?["data"] as JsonArray)?.Count ?? 0;
                    }
                }
                catch { }
            }

            string summary = running
                ? $"AI {state} · PID {pid?.ToString() ?? "—"} · {endpoint ?? "no URL"} · {loaded} model(s) · up {uptime ?? "—"}"
                : "AI service stopped";

            return new FoundryDaemonStatus
            {
                Running = running,
                State = state,
                Pid = pid,
                Endpoint = endpoint?.TrimEnd('/'),
                Uptime = uptime,
                LoadedModels = loaded,
                Summary = summary
            };
        }
        catch (Exception ex)
        {
            return new FoundryDaemonStatus
            {
                Running = false,
                State = "error",
                Summary = "Could not query Foundry",
                Error = ex.Message
            };
        }
    }

    public static async Task<(bool Ok, string Message)> StartDaemonAsync(CancellationToken ct = default)
    {
        var (code, stdout, stderr) = await RunFoundryAsync("server start", ct, timeoutMs: 120_000);
        if (code == 0)
        {
            await Task.Delay(800, ct);
            var st = await GetDaemonStatusAsync(ct);
            return (st.Running, st.Running ? $"Started · {st.Summary}" : $"Start finished but not ready yet. {stdout} {stderr}".Trim());
        }
        return (false, string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
    }

    public static async Task<(bool Ok, string Message)> StopDaemonAsync(CancellationToken ct = default)
    {
        var (code, stdout, stderr) = await RunFoundryAsync("server stop", ct, timeoutMs: 60_000);
        if (code == 0)
        {
            await Task.Delay(400, ct);
            return (true, "AI service stopped.");
        }
        return (false, string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
    }

    public static async Task<(bool Ok, string Message)> RestartDaemonAsync(CancellationToken ct = default)
    {
        var (code, stdout, stderr) = await RunFoundryAsync("server restart", ct, timeoutMs: 120_000);
        if (code == 0)
        {
            await Task.Delay(1000, ct);
            var st = await GetDaemonStatusAsync(ct);
            return (st.Running, st.Running ? $"Restarted · {st.Summary}" : $"Restart finished but not ready. {stdout} {stderr}".Trim());
        }
        return (false, string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
    }

    public async Task<bool> ConnectAsync(FoundrySettings settings, CancellationToken ct = default)
    {
        LastError = null;
        string? url = string.IsNullOrWhiteSpace(settings.Endpoint) ? null : settings.Endpoint.Trim().TrimEnd('/');
        if (url is null)
            url = await DiscoverEndpointAsync(ct);
        if (string.IsNullOrWhiteSpace(url))
        {
            LastError = "Foundry Local not reachable. Use Start on AI / Foundry settings.";
            return false;
        }
        _baseUrl = url.TrimEnd('/');
        try
        {
            using var resp = await Http.GetAsync($"{_baseUrl}/v1/models", ct);
            if (!resp.IsSuccessStatusCode)
            {
                LastError = $"Foundry /v1/models failed ({(int)resp.StatusCode}).";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    public async Task EnsureModelLoadedAsync(string alias, bool autoLoad, CancellationToken ct = default)
    {
        if (!autoLoad) return;
        if (await IsModelLoadedAsync(alias, ct)) return;
        await LoadModelAsync(alias, ct);
    }

    public async Task<bool> IsModelLoadedAsync(string alias, CancellationToken ct = default)
    {
        var models = await ListModelIdsAsync(ct);
        return models.Any(m => m.Contains(alias, StringComparison.OrdinalIgnoreCase)
                               || alias.Contains(m, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Loads a model via the Foundry CLI and confirms it registered with the running service.
    /// Verifies against /v1/models regardless of CLI exit code (CLI/daemon wording can disagree).</summary>
    public async Task<(bool Ok, string Message)> LoadModelAsync(string alias, CancellationToken ct = default)
    {
        var (_, stdout, stderr) = await RunFoundryAsync($"model load \"{alias}\"", ct, timeoutMs: 300_000);
        for (int i = 0; i < 4; i++)
        {
            if (await IsModelLoadedAsync(alias, ct)) return (true, $"Loaded {alias}.");
            await Task.Delay(700, ct);
        }
        string msg = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        LastError = $"Could not load model {alias}. {Trim(msg, 200)}";
        return (false, LastError);
    }

    /// <summary>True when a chat error means the daemon has no such model loaded (e.g. after a restart).</summary>
    public static bool IsModelNotLoaded(string? error) =>
        !string.IsNullOrEmpty(error) && error.Contains("not loaded", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<string>> ListModelIdsAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await Http.GetAsync($"{_baseUrl}/v1/models", ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            var root = JsonNode.Parse(json) as JsonObject;
            var data = root?["data"] as JsonArray;
            if (data is null) return Array.Empty<string>();
            return data.Select(n => n?["id"]?.GetValue<string>() ?? "")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }
        catch { return Array.Empty<string>(); }
    }

    public async Task<string?> ChatVisionJsonAsync(
        string model,
        string prompt,
        byte[] pngBytes,
        CancellationToken ct = default,
        int maxTokens = 2048)
    {
        LastError = null;
        LastErrorIsOom = false;
        string b64 = Convert.ToBase64String(pngBytes);
        // Keep instructions in the user turn — some VL builds ignore/mis-handle system + image.
        string userText = "Reply with JSON only.\n\n" + prompt;
        var payload = new JsonObject
        {
            ["model"] = model,
            ["temperature"] = 0.1,
            // KV-cache is preallocated from this budget; keep it modest — the JSON reply is small,
            // and a smaller budget lowers peak memory on CPU-only / low-RAM machines.
            ["max_tokens"] = Math.Clamp(maxTokens, 256, 4096),
            ["stream"] = false,
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = new JsonArray
                    {
                        new JsonObject { ["type"] = "text", ["text"] = userText },
                        new JsonObject
                        {
                            ["type"] = "image_url",
                            ["image_url"] = new JsonObject
                            {
                                ["url"] = $"data:image/png;base64,{b64}"
                            }
                        }
                    }
                }
            }
        };

        var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/chat/completions")
            {
                Content = content
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-needed");
            using var resp = await Http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                LastErrorIsOom = IsOutOfMemory(body);
                LastError = $"Chat failed ({(int)resp.StatusCode}): {Trim(body, 240)}";
                return null;
            }
            var root = JsonNode.Parse(body) as JsonObject;
            var text = root?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                LastError = "Empty model response.";
                return null;
            }
            return text;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return null;
        }
    }

    public static async Task<string?> DiscoverEndpointAsync(CancellationToken ct = default)
    {
        var st = await GetDaemonStatusAsync(ct);
        if (!string.IsNullOrWhiteSpace(st.Endpoint)) return st.Endpoint;

        try
        {
            var (code, stdout, _) = await RunFoundryAsync("status -o json", ct);
            if (code == 0)
            {
                var root = JsonNode.Parse(stdout) as JsonObject;
                var urls = root?["service"]?["webUrls"] as JsonArray;
                var first = urls?.FirstOrDefault()?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(first)) return first.TrimEnd('/');
            }
        }
        catch { }

        foreach (var port in new[] { 59273, 5272, 3928, 1234 })
        {
            try
            {
                using var resp = await Http.GetAsync($"http://127.0.0.1:{port}/v1/models", ct);
                if (resp.IsSuccessStatusCode) return $"http://127.0.0.1:{port}";
            }
            catch { }
        }
        return null;
    }

    public static string ResolveModelId(IReadOnlyList<string> loaded, string alias)
    {
        if (loaded.Count == 0) return alias;
        var hit = loaded.FirstOrDefault(m => m.Equals(alias, StringComparison.OrdinalIgnoreCase));
        if (hit is not null) return hit;
        hit = loaded.FirstOrDefault(m => m.StartsWith(alias, StringComparison.OrdinalIgnoreCase));
        if (hit is not null) return hit;
        hit = loaded.FirstOrDefault(m => m.Contains("qwen3-vl-2b", StringComparison.OrdinalIgnoreCase))
              ?? loaded.FirstOrDefault(m => m.Contains("qwen3-vl", StringComparison.OrdinalIgnoreCase));
        return hit ?? alias;
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunFoundryAsync(
        string args,
        CancellationToken ct,
        int timeoutMs = 30_000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "foundry",
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Could not start `foundry` CLI. Is Foundry Local installed?");
        var stdoutTask = p.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = p.StandardError.ReadToEndAsync(ct);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);
        try
        {
            await p.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            return (-1, await stdoutTask, "Timed out waiting for Foundry CLI.");
        }
        return (p.ExitCode, await stdoutTask, await stderrTask);
    }

    private static string Trim(string s, int n) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= n ? s : s[..n] + "…";

    public void Dispose() { }
}