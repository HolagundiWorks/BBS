using Aorms.Bridge;

var dbPath = Path.Combine(Path.GetTempPath(), "aorms-firm-smoke.db");
if (File.Exists(dbPath)) File.Delete(dbPath);

var opt = new BridgeOptions
{
    DeviceId = Environment.GetEnvironmentVariable("INSTALL_ID") ?? "smoke-device-1",
    HubUrl = Environment.GetEnvironmentVariable("ESTI_HUB_URL") ?? "http://127.0.0.1:4000",
    LicenseApiUrl = Environment.GetEnvironmentVariable("ESTI_LICENSE_API_URL") ?? "",
    ProductApiKey = Environment.GetEnvironmentVariable("ESTI_PRODUCT_API_KEY") ?? "",
};
using var bridge = new AormsBridge(opt, dbPath);

var licenseKey = Environment.GetEnvironmentVariable("ESTI_LICENSE_KEY")?.Trim();
if (!string.IsNullOrEmpty(licenseKey))
{
    if (string.IsNullOrWhiteSpace(opt.LicenseApiUrl) || string.IsNullOrWhiteSpace(opt.ProductApiKey))
    {
        Console.Error.WriteLine("ESTI_LICENSE_KEY set but ESTI_LICENSE_API_URL / ESTI_PRODUCT_API_KEY missing.");
        Environment.Exit(2);
    }
    Console.WriteLine($"activate device={opt.DeviceId} hub={opt.HubUrl}");
    var grant = await bridge.ActivateAsync(licenseKey);
    Console.WriteLine($"activate OK hasSyncToken={!string.IsNullOrWhiteSpace(grant.SyncToken)}");
}

bridge.EnqueueMeta("phaseProgress", "smoke-phase-1", new Dictionary<string, object?>
{
    ["projectId"] = "00000000-0000-0000-0000-000000000001",
    ["phaseId"] = "smoke-phase-1",
    ["pctComplete"] = 42,
    ["status"] = "IN_PROGRESS",
});
var cfg = bridge.HubConfigured();
Console.WriteLine($"hasSyncToken={cfg.HasSyncToken} syncReady={cfg.SyncReady} hub={cfg.HubUrl}");
var flush = await bridge.FlushAsync();
Console.WriteLine($"flush skipped={flush.SkippedReason ?? "(none)"} metaSent={flush.MetaSent} artSent={flush.ArtifactsSent}");

if (!string.IsNullOrEmpty(licenseKey))
{
    if (flush.SkippedReason is not null || flush.MetaSent < 1)
    {
        Console.Error.WriteLine("FAIL hub activate→Flush expected metaSent>=1");
        Environment.Exit(1);
    }
    Console.WriteLine("OK hub activate → meta Flush");
    Environment.Exit(0);
}

if (flush.SkippedReason == "missing_sync_token")
{
    Console.WriteLine("OK local outbox + skip without token (expected until activate).");
    Environment.Exit(0);
}
Console.WriteLine(flush.MetaSent > 0 ? "OK hub meta flush" : "WARN unexpected flush result");
