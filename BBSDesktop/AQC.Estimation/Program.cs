using Aorms.Bridge;

// Suite product shell — AQC Estimation
// Shares bbs_engine (via AQC) + Aorms.Bridge. Full WinUI domain UI lands next.
Console.WriteLine("AQC Estimation · product=estimation");
var opt = new BridgeOptions
{
    DeviceId = Environment.GetEnvironmentVariable("INSTALL_ID") ?? $"aqc-estimation-dev",
    HubUrl = Environment.GetEnvironmentVariable("ESTI_HUB_URL") ?? "http://127.0.0.1:4000",
    LicenseApiUrl = Environment.GetEnvironmentVariable("ESTI_LICENSE_API_URL") ?? "",
    ProductApiKey = Environment.GetEnvironmentVariable("ESTI_PRODUCT_API_KEY") ?? "",
    DeviceName = "AQC Estimation",
};
using var bridge = new AormsBridge(opt);
var cfg = bridge.HubConfigured();
Console.WriteLine($"syncReady={cfg.SyncReady} hub={cfg.HubUrl}");
Console.WriteLine("OK shared Aorms.Bridge (suite three-app packaging).");