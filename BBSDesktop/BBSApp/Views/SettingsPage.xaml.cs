using System.Globalization;
using BBSApp.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace BBSApp.Views;

public sealed partial class SettingsPage : Page
{
    public enum SettingsTab { Project, Engineering, CostPercent, Foundry }

    private string _pendingLogoPath = "";
    private readonly SettingsTab _initialTab;

    public SettingsPage(SettingsTab tab = SettingsTab.Project)
    {
        _initialTab = tab;
        InitializeComponent();
        LoadProjectFields();
        LoadEngineeringFields();
        LoadMarkupFields();
        LoadFoundryFields();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        SettingsPivot.SelectedIndex = _initialTab switch
        {
            SettingsTab.Engineering => 1,
            SettingsTab.CostPercent => 2,
            SettingsTab.Foundry => 3,
            _ => 0
        };
    }

    private void LoadFoundryFields()
    {
        var f = FoundrySettings.Load();
        FoundryEndpointBox.Text = f.Endpoint ?? "";
        FoundryModelBox.Text = string.IsNullOrWhiteSpace(f.ModelAlias) ? "qwen3-vl-2b-instruct" : f.ModelAlias;
        FoundryConfidenceBox.Value = f.ConfidenceThreshold;
        FoundryAutoLoadToggle.IsOn = f.AutoLoadModel;
        FoundryStatusText.Text = "Checking…";
        FoundryRunStateText.Text = "Checking…";
        _ = RefreshFoundryStatusAsync();
    }

    private async System.Threading.Tasks.Task RefreshFoundryStatusAsync()
    {
        SetFoundryBusy(true);
        try
        {
            var st = await FoundryLocalClient.GetDaemonStatusAsync();
            ApplyFoundryStatus(st);
        }
        catch (Exception ex)
        {
            FoundryRunStateText.Text = "Error";
            FoundryStatusText.Text = ex.Message;
            FoundryStatusDot.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 200, 60, 60));
        }
        finally
        {
            SetFoundryBusy(false);
        }
    }

    private void ApplyFoundryStatus(FoundryDaemonStatus st)
    {
        FoundryRunStateText.Text = st.StatusLabel;
        FoundryStatusText.Text = string.IsNullOrWhiteSpace(st.Error)
            ? st.Summary
            : $"{st.Summary} — {st.Error}";
        FoundryStatusDot.Background = new SolidColorBrush(
            st.Running
                ? Windows.UI.Color.FromArgb(255, 34, 160, 80)
                : string.IsNullOrEmpty(st.Error)
                    ? Windows.UI.Color.FromArgb(255, 176, 176, 176)
                    : Windows.UI.Color.FromArgb(255, 200, 60, 60));
        FoundryStartBtn.IsEnabled = !st.Running;
        FoundryStopBtn.IsEnabled = st.Running;
        FoundryRestartBtn.IsEnabled = true;
        if (!string.IsNullOrWhiteSpace(st.Endpoint) && string.IsNullOrWhiteSpace(FoundryEndpointBox.Text))
            FoundryEndpointBox.PlaceholderText = st.Endpoint;
    }

    private void SetFoundryBusy(bool busy)
    {
        if (busy)
        {
            FoundryStartBtn.IsEnabled = false;
            FoundryStopBtn.IsEnabled = false;
            FoundryRestartBtn.IsEnabled = false;
            return;
        }
        // Re-enabled by ApplyFoundryStatus after refresh
        FoundryRestartBtn.IsEnabled = true;
    }

    private async void FoundryRefresh_Click(object sender, RoutedEventArgs e) =>
        await RefreshFoundryStatusAsync();

    private async void FoundryStart_Click(object sender, RoutedEventArgs e)
    {
        FoundryRunStateText.Text = "Starting…";
        FoundryStatusText.Text = "Starting Foundry Local daemon…";
        SetFoundryBusy(true);
        try
        {
            var (ok, msg) = await FoundryLocalClient.StartDaemonAsync();
            FoundryStatusText.Text = msg;
            await RefreshFoundryStatusAsync();
            if (!ok) FoundryRunStateText.Text = "Start failed";
        }
        catch (Exception ex)
        {
            FoundryRunStateText.Text = "Error";
            FoundryStatusText.Text = ex.Message;
            SetFoundryBusy(false);
        }
    }

    private async void FoundryStop_Click(object sender, RoutedEventArgs e)
    {
        FoundryRunStateText.Text = "Stopping…";
        FoundryStatusText.Text = "Stopping Foundry Local daemon…";
        SetFoundryBusy(true);
        try
        {
            var (ok, msg) = await FoundryLocalClient.StopDaemonAsync();
            FoundryStatusText.Text = msg;
            await RefreshFoundryStatusAsync();
            if (!ok) FoundryRunStateText.Text = "Stop failed";
        }
        catch (Exception ex)
        {
            FoundryRunStateText.Text = "Error";
            FoundryStatusText.Text = ex.Message;
            SetFoundryBusy(false);
        }
    }

    private async void FoundryRestart_Click(object sender, RoutedEventArgs e)
    {
        FoundryRunStateText.Text = "Restarting…";
        FoundryStatusText.Text = "Restarting Foundry Local daemon…";
        SetFoundryBusy(true);
        try
        {
            var (ok, msg) = await FoundryLocalClient.RestartDaemonAsync();
            FoundryStatusText.Text = msg;
            await RefreshFoundryStatusAsync();
            if (!ok) FoundryRunStateText.Text = "Restart failed";
        }
        catch (Exception ex)
        {
            FoundryRunStateText.Text = "Error";
            FoundryStatusText.Text = ex.Message;
            SetFoundryBusy(false);
        }
    }

    private string FoundryModelAlias() =>
        string.IsNullOrWhiteSpace(FoundryModelBox.Text) ? "qwen3-vl-2b-instruct" : FoundryModelBox.Text.Trim();

    private async void FoundryLoadModel_Click(object sender, RoutedEventArgs e)
    {
        var alias = FoundryModelAlias();
        FoundryRunStateText.Text = "Loading model…";
        FoundryStatusText.Text = $"Loading {alias} (first time downloads it — this can take a few minutes)…";
        SetFoundryBusy(true);
        FoundryLoadBtn.IsEnabled = false;
        try
        {
            using var client = new FoundryLocalClient();
            if (!await client.ConnectAsync(FoundrySettings.Load()))
            {
                FoundryStatusText.Text = client.LastError ?? "AI service not reachable. Click Start first.";
                FoundryRunStateText.Text = "Unreachable";
                return;
            }
            var (ok, msg) = await client.LoadModelAsync(alias);
            FoundryStatusText.Text = ok ? $"{msg} Ready for AI auto-pick." : msg;
        }
        catch (Exception ex)
        {
            FoundryStatusText.Text = ex.Message;
        }
        finally
        {
            FoundryLoadBtn.IsEnabled = true;
            await RefreshFoundryStatusAsync();
        }
    }

    private void LoadMarkupFields()
    {
        var m = ProjectStore.Current.Markups;
        ElectricalPctBox.Value = m.ElectricalPct;
        PlumbingPctBox.Value = m.PlumbingPct;
        EscalationPctBox.Value = m.EscalationPct;
        ConsultingPctBox.Value = m.ConsultingFeePct;
    }

    private void LoadProjectFields()
    {
        var info = ProjectStore.Current.Info;
        ProjectNameBox.Text = info.Name;
        ProjectLocationBox.Text = info.Location;
        ClientNameBox.Text = info.ClientName;
        PreparedByRoleBox.SelectedItem = ProjectInfo.PreparedByRoles.Contains(info.PreparedByRole)
            ? info.PreparedByRole
            : "Engineer";
        PreparedByNameBox.Text = info.PreparedByName;
        CompanyNameBox.Text = info.CompanyName;
        ContactPhoneBox.Text = info.ContactPhone;
        ContactEmailBox.Text = info.ContactEmail;
        AddressBox.Text = info.Address;
        _pendingLogoPath = info.LogoPath ?? "";
        RefreshLogoPreview();
    }

    private void LoadEngineeringFields()
    {
        var s = ProjectStore.Current;
        DiaBox.Text = string.Join(", ", s.Diameters);
        HysdBondToggle.IsOn = s.HysdBond;
        HysdFactorBox.Value = s.HysdBondFactor;
        MinHookBox.Value = s.MinHookMm;
        HookBox.Text = FormatMap(s.HookAllowance);
        BendBox.Text = FormatMap(s.BendDeduction);
        var y = s.Yields;
        BricksM3Box.Value = y.BricksPerM3;
        BricksM2Box.Value = y.BricksPerM2Half;
        MortarFracBox.Value = y.MortarFraction;
        DryFactorBox.Value = y.MortarDryFactor;
        WastageBox.Value = y.Wastage;
        ShutterWasteBox.Value = y.ShutteringWastage;
        IgnoreOpenBox.Value = y.IgnoreOpeningBelowM2;
        BeamSlabDeductToggle.IsOn = y.BeamSlabInterfaceDeduct;
        WallFacesBox.Value = y.WallPlasterFaces;
        ColSidesBox.Value = y.DefaultColumnSidesExposed;
        PlasterCeilingToggle.IsOn = y.DefaultPlasterCeiling;
        BeamSoffitToggle.IsOn = y.DefaultBeamSoffit;
        CoverColBox.Value = s.CoverColumnMm;
        CoverBeamBox.Value = s.CoverBeamMm;
        CoverSlabBox.Value = s.CoverSlabMm;
        CoverFootBox.Value = s.CoverFootingMm;
        CoverPedBox.Value = s.CoverPedestalMm;
        CoverLintBox.Value = s.CoverLintelMm;
        ColLapBox.SelectedItem = s.DefaultColumnLap is "Yes" or "No" ? s.DefaultColumnLap : "No";
        BeamLapBox.SelectedItem = s.DefaultBeamLap is "None" or "Tension" ? s.DefaultBeamLap : "None";
    }

    private void RefreshLogoPreview()
    {
        var resolved = ProjectInfo.ResolveLogoFile(_pendingLogoPath);
        if (resolved is null)
        {
            LogoPreview.Source = null;
            LogoPathText.Text = string.IsNullOrWhiteSpace(_pendingLogoPath)
                ? "No logo selected"
                : "Logo file missing — browse again";
            return;
        }

        try
        {
            var bmp = new BitmapImage();
            bmp.UriSource = new Uri(resolved);
            LogoPreview.Source = bmp;
            LogoPathText.Text = Path.GetFileName(resolved);
        }
        catch
        {
            LogoPreview.Source = null;
            LogoPathText.Text = "Could not load logo preview";
        }
    }

    private async void BrowseLogo_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        var hwnd = WindowNative.GetWindowHandle(App.MainWindow!);
        InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".bmp");
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        try
        {
            _pendingLogoPath = await ProjectInfo.ImportLogoAsync(file.Path);
            RefreshLogoPreview();
        }
        catch (Exception ex)
        {
            AppNotify.Error("Logo import failed", ex.Message);
        }
    }

    private void ClearLogo_Click(object sender, RoutedEventArgs e)
    {
        _pendingLogoPath = "";
        RefreshLogoPreview();
    }

    private static string FormatMap(Dictionary<int, double> map) =>
        string.Join(", ", map.OrderBy(kv => kv.Key)
            .Select(kv => $"{kv.Key}:{kv.Value.ToString(CultureInfo.InvariantCulture)}"));

    private static Dictionary<int, double> ParseMap(string text, Dictionary<int, double> fallback)
    {
        var dest = new Dictionary<int, double>();
        foreach (var part in text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var bits = part.Split(':');
            if (bits.Length < 2) continue;
            if (int.TryParse(bits[0].Trim(), out var k) &&
                double.TryParse(bits[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                dest[k] = v;
        }
        if (dest.Count == 0)
            foreach (var kv in fallback) dest[kv.Key] = kv.Value;
        return dest;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var parts = DiaBox.Text.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
        var list = new List<int>();
        foreach (var p in parts)
            if (int.TryParse(p.Trim(), out var d) && d > 0) list.Add(d);
        if (list.Count == 0)
        {
            AppNotify.Error("Enter at least one diameter.");
            return;
        }

        var name = (ProjectNameBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            AppNotify.Error("Enter a project name.");
            return;
        }

        var store = ProjectStore.Current;
        var info = store.Info;
        info.Name = name;
        info.Location = (ProjectLocationBox.Text ?? "").Trim();
        info.ClientName = (ClientNameBox.Text ?? "").Trim();
        info.PreparedByRole = PreparedByRoleBox.SelectedItem?.ToString() ?? "Engineer";
        info.PreparedByName = (PreparedByNameBox.Text ?? "").Trim();
        info.CompanyName = string.IsNullOrWhiteSpace(CompanyNameBox.Text)
            ? Branding.Company
            : CompanyNameBox.Text.Trim();
        info.ContactPhone = (ContactPhoneBox.Text ?? "").Trim();
        info.ContactEmail = (ContactEmailBox.Text ?? "").Trim();
        info.Address = (AddressBox.Text ?? "").Trim();
        info.LogoPath = _pendingLogoPath ?? "";
        store.Name = info.Name;

        store.Diameters.Clear();
        foreach (var d in list.Distinct().OrderBy(x => x))
            store.Diameters.Add(d);

        store.HysdBond = HysdBondToggle.IsOn;
        store.HysdBondFactor = double.IsNaN(HysdFactorBox.Value) || HysdFactorBox.Value <= 0
            ? 1.6 : HysdFactorBox.Value;
        store.MinHookMm = double.IsNaN(MinHookBox.Value) ? 75 : Math.Max(0, MinHookBox.Value);

        store.HookAllowance.Clear();
        foreach (var kv in ParseMap(HookBox.Text, new Dictionary<int, double> { [90] = 9, [135] = 10, [180] = 16 }))
            store.HookAllowance[kv.Key] = kv.Value;

        store.BendDeduction.Clear();
        foreach (var kv in ParseMap(BendBox.Text, new Dictionary<int, double> { [45] = 1, [90] = 2, [135] = 3 }))
            store.BendDeduction[kv.Key] = kv.Value;

        var y = store.Yields;
        y.BricksPerM3 = Val(BricksM3Box.Value, 500);
        y.BricksPerM2Half = Val(BricksM2Box.Value, 55);
        y.MortarFraction = Val(MortarFracBox.Value, 0.30);
        y.MortarDryFactor = Val(DryFactorBox.Value, 1.33);
        y.Wastage = Val(WastageBox.Value, 1.05);
        y.ShutteringWastage = Val(ShutterWasteBox.Value, 1.05);
        y.IgnoreOpeningBelowM2 = Val(IgnoreOpenBox.Value, 0.1);
        y.BeamSlabInterfaceDeduct = BeamSlabDeductToggle.IsOn;
        y.WallPlasterFaces = (int)Math.Clamp(Val(WallFacesBox.Value, 2), 1, 2);
        y.DefaultColumnSidesExposed = (int)Math.Clamp(Val(ColSidesBox.Value, 3), 0, 4);
        y.DefaultPlasterCeiling = PlasterCeilingToggle.IsOn;
        y.DefaultBeamSoffit = BeamSoffitToggle.IsOn;

        store.CoverColumnMm = Val(CoverColBox.Value, 40);
        store.CoverBeamMm = Val(CoverBeamBox.Value, 25);
        store.CoverSlabMm = Val(CoverSlabBox.Value, 20);
        store.CoverFootingMm = Val(CoverFootBox.Value, 50);
        store.CoverPedestalMm = Val(CoverPedBox.Value, 50);
        store.CoverLintelMm = Val(CoverLintBox.Value, 25);
        store.DefaultColumnLap = ColLapBox.SelectedItem?.ToString() ?? "No";
        store.DefaultBeamLap = BeamLapBox.SelectedItem?.ToString() ?? "None";

        var m = store.Markups;
        m.ElectricalPct = Pct(ElectricalPctBox.Value, 8);
        m.PlumbingPct = Pct(PlumbingPctBox.Value, 6);
        m.EscalationPct = Pct(EscalationPctBox.Value, 5);
        m.ConsultingFeePct = Pct(ConsultingPctBox.Value, 3);

        var f = new FoundrySettings
        {
            Endpoint = (FoundryEndpointBox.Text ?? "").Trim(),
            ModelAlias = string.IsNullOrWhiteSpace(FoundryModelBox.Text)
                ? "qwen3-vl-2b-instruct"
                : FoundryModelBox.Text.Trim(),
            ConfidenceThreshold = double.IsNaN(FoundryConfidenceBox.Value)
                ? 0.72
                : Math.Clamp(FoundryConfidenceBox.Value, 0.4, 0.95),
            AutoLoadModel = FoundryAutoLoadToggle.IsOn
        };
        f.Save();

        store.Notify();
        AppNotify.Success("Settings saved", "Project, engineering, cost %, and Foundry AI.");
    }

    private async void TestFoundry_Click(object sender, RoutedEventArgs e)
    {
        FoundryStatusText.Text = "Connecting…";
        var f = new FoundrySettings
        {
            Endpoint = (FoundryEndpointBox.Text ?? "").Trim(),
            ModelAlias = string.IsNullOrWhiteSpace(FoundryModelBox.Text)
                ? "qwen3-vl-2b-instruct"
                : FoundryModelBox.Text.Trim(),
            AutoLoadModel = FoundryAutoLoadToggle.IsOn
        };
        try
        {
            using var client = new FoundryLocalClient();
            if (!await client.ConnectAsync(f))
            {
                FoundryStatusText.Text = client.LastError ?? "Failed.";
                FoundryRunStateText.Text = "Unreachable";
                FoundryStatusDot.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 200, 60, 60));
                return;
            }
            await client.EnsureModelLoadedAsync(f.ModelAlias, f.AutoLoadModel);
            var ids = await client.ListModelIdsAsync();
            string model = FoundryLocalClient.ResolveModelId(ids, f.ModelAlias);
            FoundryStatusText.Text = $"OK · {client.BaseUrl} · model `{model}` · {ids.Count} loaded id(s).";
            FoundryEndpointBox.PlaceholderText = client.BaseUrl;
            await RefreshFoundryStatusAsync();
        }
        catch (Exception ex)
        {
            FoundryStatusText.Text = ex.Message;
            FoundryRunStateText.Text = "Error";
        }
    }

    private static double Val(double v, double def) =>
        double.IsNaN(v) || v <= 0 ? def : v;

    private static double Pct(double v, double def) =>
        double.IsNaN(v) || v < 0 ? def : v;
}
