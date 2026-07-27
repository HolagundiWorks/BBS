using BBSApp.Models;
using BBSApp.Services;
using BBSApp.Views;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.System;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace BBSApp;

public sealed partial class MainWindow : Window
{
    private sealed record NavCmd(string Tag, string Label, object? Icon);

    private sealed record RibbonTab(string Id, string Title, NavCmd[] Commands);

    private readonly List<RibbonTab> _tabs = new();
    private readonly Dictionary<string, ToggleButton> _tabButtons = new(StringComparer.OrdinalIgnoreCase);
    private string _activeTab = "project";
    private string _activeTag = "dashboard";
    private DispatcherTimer? _toastTimer;
    private int _toastGeneration;

    public MainWindow()
    {
        InitializeComponent();
        App.MainWindow = this;
        TrySetMica();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        TrySetAppIcon();
        ProjectStore.Current.SeedDefaults();
        BuildRibbonModel();
        BuildRibbonTabs();
        SelectTab("project");
        NavigateTo("dashboard");
        SetWindowTitle(Branding.WindowTitle(ProjectStore.Current.Name, ProjectStore.Current.IsDirty));
        AppNotify.Raised += OnAppNotify;
        ProjectStore.Current.Changed += OnStoreChanged;
        Closed += (_, _) =>
        {
            AppNotify.Raised -= OnAppNotify;
            ProjectStore.Current.Changed -= OnStoreChanged;
        };
        if (Content is FrameworkElement root)
            root.KeyDown += Window_KeyDown;
    }

    private void OnStoreChanged()
    {
        DispatcherQueue.TryEnqueue(() =>
            SetWindowTitle(Branding.WindowTitle(ProjectStore.Current.Name, ProjectStore.Current.IsDirty)));
    }

    private void Window_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (!ctrl) return;
        string? tab = e.Key switch
        {
            VirtualKey.Number1 or VirtualKey.NumberPad1 => "project",
            VirtualKey.Number2 or VirtualKey.NumberPad2 => "rcc",
            VirtualKey.Number3 or VirtualKey.NumberPad3 => "civil",
            VirtualKey.Number4 or VirtualKey.NumberPad4 => "schedule",
            VirtualKey.Number5 or VirtualKey.NumberPad5 => "office",
            VirtualKey.Number6 or VirtualKey.NumberPad6 => "contracts",
            VirtualKey.Number7 or VirtualKey.NumberPad7 => "accounts",
            VirtualKey.Number8 or VirtualKey.NumberPad8 => "outputs",
            _ => null
        };
        if (tab is null) return;
        SelectTab(tab);
        e.Handled = true;
    }

    private void BuildRibbonModel()
    {
        _tabs.Clear();
        _tabs.Add(new RibbonTab("project", "Project", new[]
        {
            Cmd("dashboard", "Dashboard", Glyph("\uE80F")),
            Cmd("levels", "Levels", Glyph("\uF158")),
            Cmd("takeoff", "Drawing takeoff", Glyph("\uE8A5")),
        }));
        _tabs.Add(new RibbonTab("rcc", "RCC steel", new[]
        {
            Cmd("columns", "Columns", NavIcon("columns")),
            Cmd("beams", "Beams", NavIcon("beams")),
            Cmd("pedestals", "Pedestals", NavIcon("pedestals")),
            Cmd("lintels", "Lintels", NavIcon("lintels")),
            Cmd("slabs", "Slabs", NavIcon("slabs")),
            Cmd("footings", "Footings", NavIcon("footings")),
            Cmd("walls", "Retaining walls", NavIcon("walls")),
            Cmd("stairs", "Stairs", NavIcon("stairs")),
        }));
        _tabs.Add(new RibbonTab("civil", "Civil BOQ", new[]
        {
            Cmd("masonry", "Masonry walls", NavIcon("masonry")),
            Cmd("plaster", "Plastering", NavIcon("plaster")),
            Cmd("pcc", "PCC bed", NavIcon("pcc")),
            Cmd("earthwork", "Earthwork", NavIcon("earthwork")),
            Cmd("ssm", "Size stone", NavIcon("ssm")),
            Cmd("shuttering", "Shuttering", NavIcon("shuttering")),
            Cmd("flooring", "Flooring", NavIcon("flooring")),
            Cmd("painting", "Painting", NavIcon("painting")),
            Cmd("waterproofing", "Waterproofing", NavIcon("waterproofing")),
            Cmd("dpc", "DPC", NavIcon("dpc")),
            Cmd("coping", "Coping", NavIcon("coping")),
            Cmd("screed", "Screed", NavIcon("screed")),
            Cmd("vdf", "VDF", NavIcon("vdf")),
            Cmd("skirting", "Skirting", NavIcon("skirting")),
            Cmd("parapet", "Parapet", NavIcon("parapet")),
            Cmd("plinth_protection", "Plinth protection", NavIcon("plinth_protection")),
            Cmd("doors", "Doors", NavIcon("doors")),
            Cmd("windows", "Windows", NavIcon("windows")),
        }));
        _tabs.Add(new RibbonTab("schedule", "Schedule", new[]
        {
            Cmd("schedule_activities", "Activities", Glyph("")),
            Cmd("schedule_network", "Network", Glyph("")),
            Cmd("schedule_gantt", "Gantt", Glyph("")),
        }));
        _tabs.Add(new RibbonTab("office", "Office", new[]
        {
            Cmd("correspondence", "Correspondence", Glyph("")),
        }));
        _tabs.Add(new RibbonTab("contracts", "Contracts", new[]
        {
            Cmd("contracts_list", "Contracts", Glyph("")),
            Cmd("contracts_rates", "Rate schedule", Glyph("")),
            Cmd("contracts_terms", "Terms", Glyph("")),
        }));
        _tabs.Add(new RibbonTab("accounts", "Accounts", new[]
        {
            Cmd("accounts_bills", "Running bills", Glyph("")),
            Cmd("accounts_cash", "Cash book", Glyph("")),
            Cmd("accounts_ledger", "Ledger", Glyph("")),
        }));
        _tabs.Add(new RibbonTab("outputs", "Outputs", new[]
        {
            Cmd("quantities", "Quantities", Glyph("\uE9D2")),
            Cmd("po", "Purchase orders", Glyph("\uE7BF")),
            Cmd("estimate", "Estimate", Glyph("\uE8EF")),
            Cmd("ratebook", "Rate book", Glyph("\uE8F1")),
            Cmd("report", "Report", Glyph("\uE8A5")),
            Cmd("settings_project", "Project", Glyph("\uE8B7")),
            Cmd("settings_engineering", "Engineering", Glyph("\uE90F")),
            Cmd("settings_cost", "Cost %", Glyph("\uE8EF")),
        }));
    }

    private static NavCmd Cmd(string tag, string label, object? icon) => new(tag, label, icon);

    /// <summary>SVG theme icon, or 1–2 letter badge when the asset is missing.</summary>
    private static FrameworkElement NavIcon(string tag)
    {
        string? key = tag.ToLowerInvariant() switch
        {
            "columns" or "column" => "NavIconColumn",
            "beams" or "beam" => "NavIconBeam",
            "pedestals" or "pedestal" => "NavIconPedestal",
            "lintels" or "lintel" => "NavIconLintel",
            "slabs" or "slab" => "NavIconSlab",
            "footings" or "footing" => "NavIconFooting",
            "walls" or "wall" => "NavIconWall",
            "stairs" or "stair" => "NavIconStair",
            "masonry" => "NavIconMasonry",
            "plaster" => "NavIconPlaster",
            "pcc" => "NavIconPcc",
            "earthwork" => "NavIconEarthwork",
            "ssm" => "NavIconSsm",
            "shuttering" => "NavIconShuttering",
            "flooring" => "NavIconFlooring",
            "painting" => "NavIconPainting",
            "waterproofing" => "NavIconWaterproofing",
            "dpc" => "NavIconDpc",
            "coping" => "NavIconCoping",
            "screed" => "NavIconScreed",
            "vdf" => "NavIconVdf",
            "skirting" => "NavIconSkirting",
            "parapet" => "NavIconParapet",
            "plinth_protection" or "plinth" => "NavIconPlinth",
            "doors" or "door" => null,
            "windows" or "window" => null,
            _ => null
        };
        if (key is not null && ThemeIcon(key) is { } img)
            return img;
        return LetterIcon(LetterForTag(tag));
    }

    private static string LetterForTag(string tag) => tag.ToLowerInvariant() switch
    {
        "columns" or "column" => "C",
        "beams" or "beam" => "B",
        "pedestals" or "pedestal" => "P",
        "lintels" or "lintel" => "L",
        "slabs" or "slab" => "S",
        "footings" or "footing" => "F",
        "walls" or "wall" => "W",
        "stairs" or "stair" => "ST",
        "masonry" => "MW",
        "plaster" => "PL",
        "pcc" => "PC",
        "earthwork" => "EW",
        "ssm" => "SS",
        "shuttering" => "SH",
        "flooring" => "FL",
        "painting" => "PT",
        "waterproofing" => "WP",
        "dpc" => "DP",
        "coping" => "CP",
        "screed" => "SC",
        "vdf" => "VD",
        "skirting" => "SK",
        "parapet" => "PR",
        "plinth_protection" or "plinth" => "PP",
        "doors" or "door" => "DR",
        "windows" or "window" => "WN",
        "estimate" => "ES",
        "ratebook" => "RB",
        _ => tag.Length >= 2 ? tag[..2].ToUpperInvariant() : tag.ToUpperInvariant()
    };

    private static FrameworkElement LetterIcon(string letters, double size = 16)
    {
        string text = letters.Length > 2 ? letters[..2] : letters;
        return new Border
        {
            Width = size + 6,
            Height = size + 6,
            CornerRadius = new CornerRadius(3),
            Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = text,
                FontSize = text.Length > 1 ? size * 0.55 : size * 0.7,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -1, 0, 0)
            }
        };
    }

    private static ImageIcon? ThemeIcon(string key)
    {
        if (!Application.Current.Resources.TryGetValue(key, out var src) || src is null)
            return null;
        return new ImageIcon { Source = (ImageSource)src, Width = 16, Height = 16 };
    }

    private void BuildRibbonTabs()
    {
        RibbonTabPanel.Children.Clear();
        _tabButtons.Clear();
        var tabStyle = (Style)Application.Current.Resources["RibbonTabStyle"];
        foreach (var tab in _tabs)
        {
            var btn = new ToggleButton
            {
                Content = tab.Title,
                Tag = tab.Id,
                Style = tabStyle
            };
            ToolTipService.SetToolTip(btn, $"{tab.Title} commands");
            btn.Click += RibbonTab_Click;
            _tabButtons[tab.Id] = btn;
            RibbonTabPanel.Children.Add(btn);
        }
    }

    private void RibbonTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string id) return;
        SelectTab(id);
    }

    private void SelectTab(string id)
    {
        _activeTab = id;
        var tabFill = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
        var tabStroke = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
        var transparent = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        foreach (var kv in _tabButtons)
        {
            bool on = string.Equals(kv.Key, id, StringComparison.OrdinalIgnoreCase);
            kv.Value.IsChecked = on;
            kv.Value.FontWeight = on
                ? Microsoft.UI.Text.FontWeights.SemiBold
                : Microsoft.UI.Text.FontWeights.Normal;
            // Active tab shares fill/border with RibbonBody so they read as one piece.
            kv.Value.Background = on ? tabFill : transparent;
            kv.Value.BorderBrush = on ? tabStroke : transparent;
            kv.Value.Foreground = (Brush)Application.Current.Resources[
                on ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush"];
        }

        var tab = _tabs.FirstOrDefault(t => t.Id == id) ?? _tabs[0];
        RibbonCommandPanel.Children.Clear();
        double captionSize = ResourceDouble("RibbonCommandCaptionSize", 11);
        foreach (var cmd in tab.Commands)
        {
            var stack = new StackPanel { Spacing = 4, Width = 76 };
            FrameworkElement iconEl = cmd.Icon switch
            {
                FontIcon fi => CloneFontIcon(fi, 20),
                ImageIcon ii => new ImageIcon { Source = ii.Source, Width = 20, Height = 20 },
                FrameworkElement fe => CloneFrameworkIcon(fe, 20),
                _ => LetterIcon(LetterForTag(cmd.Tag), 18)
            };
            iconEl.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(iconEl);
            stack.Children.Add(new TextBlock
            {
                Text = cmd.Label,
                FontSize = captionSize,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.WrapWholeWords,
                MaxLines = 2,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            bool active = string.Equals(cmd.Tag, _activeTag, StringComparison.OrdinalIgnoreCase);
            var btn = new Button
            {
                Content = stack,
                Tag = cmd.Tag,
                Padding = new Thickness(8, 6, 8, 6),
                MinWidth = 76,
                Background = active
                    ? (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"]
                    : transparent,
                BorderBrush = active
                    ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
                    : transparent,
                BorderThickness = active ? new Thickness(0, 0, 0, 2) : new Thickness(0),
                CornerRadius = new CornerRadius(4)
            };
            ToolTipService.SetToolTip(btn, cmd.Label);
            btn.Click += RibbonCommand_Click;
            RibbonCommandPanel.Children.Add(btn);
        }
    }

    private static FontIcon CloneFontIcon(FontIcon src, double size = 16) => new()
    {
        Glyph = src.Glyph,
        FontSize = size,
        FontFamily = src.FontFamily
    };

    private static FrameworkElement CloneFrameworkIcon(FrameworkElement src, double size = 20)
    {
        if (src is ImageIcon ii)
            return new ImageIcon { Source = ii.Source, Width = size, Height = size };
        if (src is Border)
            return LetterIcon(
                src is Border { Child: TextBlock tb } ? tb.Text : "?",
                size);
        return LetterIcon("?", size);
    }

    private static FontIcon Glyph(string g, double size = 16) => new()
    {
        Glyph = g,
        FontSize = size,
        FontFamily = (FontFamily)Application.Current.Resources["SymbolThemeFontFamily"]
    };

    private static double ResourceDouble(string key, double fallback)
    {
        if (Application.Current.Resources.TryGetValue(key, out var v) && v is double d)
            return d;
        return fallback;
    }

    private void RibbonCommand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string tag) return;
        NavigateTo(tag);
    }

    private void NavigateTo(string tag)
    {
        _activeTag = tag;
        // Keep matching tab selected for context; ribbon stays expanded.
        var owner = _tabs.FirstOrDefault(t => t.Commands.Any(c => c.Tag == tag));
        if (owner is not null && owner.Id != _activeTab)
            SelectTab(owner.Id);
        else
            RefreshCommandHighlight();

        ContentFrame.Content = tag switch
        {
            "dashboard" => new DashboardPage(),
            "levels" => new LevelsPage(),
            "columns" => new ElementPage(ElementSpecs.Columns(), ProjectStore.Current.Columns),
            "beams" => new ElementPage(ElementSpecs.Beams(), ProjectStore.Current.Beams),
            "pedestals" => new ElementPage(ElementSpecs.Pedestals(), ProjectStore.Current.Pedestals),
            "lintels" => new ElementPage(ElementSpecs.Lintels(), ProjectStore.Current.Lintels),
            "slabs" => new ElementPage(ElementSpecs.Slabs(), ProjectStore.Current.Slabs),
            "footings" => new ElementPage(ElementSpecs.Footings(), ProjectStore.Current.Footings),
            "walls" => new ElementPage(ElementSpecs.Walls(), ProjectStore.Current.Walls),
            "stairs" => new ElementPage(ElementSpecs.Stairs(), ProjectStore.Current.Stairs),
            "takeoff" => new TakeoffPage(),
            "masonry" => new ElementPage(ElementSpecs.MasonryWalls(), ProjectStore.Current.MasonryWalls),
            "plaster" => new ElementPage(ElementSpecs.Plaster(), ProjectStore.Current.FinishPropose),
            "pcc" => new ElementPage(ElementSpecs.PccBeds(), ProjectStore.Current.PccBeds),
            "earthwork" => new ElementPage(ElementSpecs.Earthwork(), ProjectStore.Current.Earthwork),
            "ssm" => new ElementPage(ElementSpecs.SizeStone(), ProjectStore.Current.SizeStone),
            "shuttering" => new ElementPage(ElementSpecs.Shuttering(), ProjectStore.Current.Shuttering),
            "flooring" => new ElementPage(ElementSpecs.Flooring(), ProjectStore.Current.Flooring),
            "painting" => new ElementPage(ElementSpecs.Painting(), ProjectStore.Current.Painting),
            "waterproofing" => new ElementPage(ElementSpecs.Waterproofing(), ProjectStore.Current.Waterproofing),
            "dpc" => new ElementPage(ElementSpecs.Dpc(), ProjectStore.Current.Dpc),
            "coping" => new ElementPage(ElementSpecs.Coping(), ProjectStore.Current.Coping),
            "screed" => new ElementPage(ElementSpecs.Screed(), ProjectStore.Current.Screed),
            "vdf" => new ElementPage(ElementSpecs.Vdf(), ProjectStore.Current.Vdf),
            "skirting" => new ElementPage(ElementSpecs.Skirting(), ProjectStore.Current.Skirting),
            "parapet" => new ElementPage(ElementSpecs.Parapet(), ProjectStore.Current.Parapet),
            "plinth_protection" => new ElementPage(ElementSpecs.PlinthProtection(), ProjectStore.Current.PlinthProtection),
            "doors" => new ElementPage(ElementSpecs.Doors(), ProjectStore.Current.Doors),
            "windows" => new ElementPage(ElementSpecs.Windows(), ProjectStore.Current.Windows),
            "schedule" or "schedule_activities" => new SchedulePage(SchedulePage.ScheduleTab.Activities),
            "schedule_network" => new SchedulePage(SchedulePage.ScheduleTab.Network),
            "schedule_gantt" => new SchedulePage(SchedulePage.ScheduleTab.Gantt),
            "office" or "correspondence" => new CorrespondencePage(),
            "contracts" or "contracts_list" => new ContractsPage(ContractsPage.ContractsTab.Contracts),
            "contracts_rates" => new ContractsPage(ContractsPage.ContractsTab.Rates),
            "contracts_terms" => new ContractsPage(ContractsPage.ContractsTab.Terms),
            "accounts" or "accounts_bills" => new AccountsPage(AccountsPage.AccountsTab.Bills),
            "accounts_cash" => new AccountsPage(AccountsPage.AccountsTab.Cash),
            "accounts_ledger" => new AccountsPage(AccountsPage.AccountsTab.Ledger),
            "quantities" => new QuantitiesPage(),
            "po" => new PurchaseOrderPage(),
            "estimate" => new EstimatePage(),
            "ratebook" => new RateBookPage(),
            "report" => new ReportPage(),
            "settings" or "settings_project" => new SettingsPage(SettingsPage.SettingsTab.Project),
            "settings_engineering" => new SettingsPage(SettingsPage.SettingsTab.Engineering),
            "settings_cost" => new SettingsPage(SettingsPage.SettingsTab.CostPercent),
            _ => new DashboardPage()
        };

        var label = owner?.Commands.FirstOrDefault(c => c.Tag == tag)?.Label ?? tag;
        RibbonPageLabel.Text = label;
    }

    private void RefreshCommandHighlight()
    {
        var accent = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        var selectedBg = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        var transparent = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        foreach (var child in RibbonCommandPanel.Children)
        {
            if (child is not Button btn || btn.Tag is not string tag) continue;
            bool on = string.Equals(tag, _activeTag, StringComparison.OrdinalIgnoreCase);
            btn.Background = on ? selectedBg : transparent;
            btn.BorderBrush = on ? accent : transparent;
            btn.BorderThickness = on ? new Thickness(0, 0, 0, 2) : new Thickness(0);
        }
    }

    private void OnAppNotify(NotifyRequest req)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _toastGeneration++;
            int gen = _toastGeneration;
            ToastBar.Title = req.Title;
            ToastBar.Message = req.Message;
            ToastBar.Severity = req.Severity;
            ToastHost.Visibility = Visibility.Visible;
            ToastBar.IsOpen = true;

            _toastTimer?.Stop();
            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(1200, req.DurationMs)) };
            _toastTimer.Tick += (_, _) =>
            {
                _toastTimer.Stop();
                if (gen == _toastGeneration)
                    CloseToast();
            };
            _toastTimer.Start();
        });
    }

    private void ToastBar_CloseClick(InfoBar sender, object args)
    {
        _toastGeneration++;
        _toastTimer?.Stop();
        CloseToast();
    }

    private void CloseToast()
    {
        ToastBar.IsOpen = false;
        ToastHost.Visibility = Visibility.Collapsed;
    }

    private void TrySetAppIcon()
    {
        try
        {
            var ico = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(ico))
                AppWindow.SetIcon(ico);
        }
        catch
        {
            // Icon is optional for unpackaged runs.
        }
    }

    private void TrySetMica()
    {
        if (MicaController.IsSupported())
        {
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
        }
        else if (DesktopAcrylicController.IsSupported())
        {
            SystemBackdrop = new DesktopAcrylicBackdrop();
        }
    }

    private void CalcFab_Click(object sender, RoutedEventArgs e)
    {
        var open = FloatingCalcPanel.Visibility != Visibility.Visible;
        FloatingCalcPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        if (open)
            CalcInput.Focus(FocusState.Programmatic);
    }

    private void CalcClose_Click(object sender, RoutedEventArgs e)
    {
        FloatingCalcPanel.Visibility = Visibility.Collapsed;
        CalcFab.Focus(FocusState.Programmatic);
    }

    private void CalcInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            EvaluateCalc(selectResult: true);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            if (!string.IsNullOrEmpty(CalcInput.Text) || !string.IsNullOrEmpty(CalcResult.Text))
            {
                CalcInput.Text = "";
                CalcResult.Text = "";
            }
            else
            {
                FloatingCalcPanel.Visibility = Visibility.Collapsed;
                CalcFab.Focus(FocusState.Programmatic);
            }
            e.Handled = true;
        }
    }

    private void CalcInput_TextChanged(object sender, TextChangedEventArgs e) =>
        EvaluateCalc(selectResult: false);

    private void EvaluateCalc(bool selectResult)
    {
        if (QuickCalc.TryEvaluate(CalcInput.Text, out var result))
        {
            CalcResult.Text = result;
            if (selectResult)
            {
                CalcResult.Focus(FocusState.Programmatic);
                CalcResult.SelectAll();
            }
        }
        else if (string.IsNullOrWhiteSpace(CalcInput.Text))
        {
            CalcResult.Text = "";
        }
        else if (!CalcInput.Text.Any(c => char.IsDigit(c)))
        {
            CalcResult.Text = "";
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void NavMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string tag) return;
        SelectNavTag(tag);
    }

    private void SelectNavTag(string tag)
    {
        NavigateTo(tag);
    }

    private void SetWindowTitle(string text)
    {
        Title = text;
        if (TitleBarText != null)
            TitleBarText.Text = Branding.AppName;
    }

    private void RefreshWindowTitle() =>
        SetWindowTitle(Branding.WindowTitle(ProjectStore.Current.Name, ProjectStore.Current.IsDirty));


    private async void About_Click(object sender, RoutedEventArgs e)
    {
        var logo = new Border
        {
            Width = 72,
            Height = 72,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Microsoft.UI.Colors.White),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/logo.png")),
                Stretch = Stretch.Uniform,
                Margin = new Thickness(8)
            }
        };

        var body = new StackPanel { Spacing = 8, MaxWidth = 360 };
        body.Children.Add(logo);
        body.Children.Add(new TextBlock
        {
            Text = Branding.AppName,
            Style = (Style)Application.Current.Resources["TitleTextBlockStyle"],
            HorizontalAlignment = HorizontalAlignment.Center
        });
        body.Children.Add(new TextBlock
        {
            Text = Branding.FullName,
            Style = (Style)Application.Current.Resources["BodyStrongStyle"],
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        });
        body.Children.Add(new TextBlock
        {
            Text = Branding.Tagline,
            Style = (Style)Application.Current.Resources["CaptionSecondaryStyle"],
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        });
        body.Children.Add(new TextBlock
        {
            Text = Branding.DevelopedBy,
            Style = (Style)Application.Current.Resources["BodyStrongStyle"],
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        });
        body.Children.Add(new TextBlock
        {
            Text = Branding.Copyright,
            Style = (Style)Application.Current.Resources["MetaTextStyle"],
            HorizontalAlignment = HorizontalAlignment.Center
        });
        body.Children.Add(new TextBlock
        {
            Text = $"Licensed under {Branding.LicenseName}",
            Style = (Style)Application.Current.Resources["MetaTextStyle"],
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var dlg = new ContentDialog
        {
            Title = $"About {Branding.AppName}",
            Content = body,
            CloseButtonText = "Close",
            XamlRoot = Content.XamlRoot,
            DefaultButton = ContentDialogButton.Close
        };
        await dlg.ShowAsync();
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        ProjectStore.Current.Reset();
        RefreshWindowTitle();
        SelectNavTag("dashboard");
    }

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.FileTypeFilter.Add(".bbsproj");
        picker.FileTypeFilter.Add(".json");
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;
        var root = EngineClient.LoadProject(file.Path, out var err);
        if (root is null)
        {
            AppNotify.Error("Open failed", err ?? "Could not open project.");
            return;
        }
        ProjectStore.Current.LoadFrom(root);
        ProjectStore.Current.FilePath = file.Path;
        RefreshWindowTitle();
        SelectNavTag("dashboard");
        AppNotify.Success("Project opened", ProjectStore.Current.Name);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ProjectStore.Current.FilePath))
        {
            SaveAs_Click(sender, e);
            return;
        }
        if (!EngineClient.SaveProject(ProjectStore.Current.FilePath, ProjectStore.Current.ToJson(), out var err))
            AppNotify.Error("Save failed", err ?? "Could not save.");
        else
        {
            ProjectStore.Current.IsDirty = false;
            RefreshWindowTitle();
            AppNotify.Success("Saved", Path.GetFileName(ProjectStore.Current.FilePath));
        }
    }

    private async void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.SuggestedFileName = ProjectStore.Current.Name.Replace(' ', '_');
        picker.FileTypeChoices.Add("AQC-Core Project", new List<string> { ".bbsproj" });
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;
        if (!EngineClient.SaveProject(file.Path, ProjectStore.Current.ToJson(), out var err))
        {
            AppNotify.Error("Save failed", err ?? "Could not save.");
            return;
        }
        ProjectStore.Current.FilePath = file.Path;
        ProjectStore.Current.IsDirty = false;
        RefreshWindowTitle();
        AppNotify.Success("Saved", file.Name);
    }
}
