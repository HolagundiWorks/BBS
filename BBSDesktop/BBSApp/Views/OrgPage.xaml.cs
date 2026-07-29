// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using BBSApp.Services;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace BBSApp.Views;

public sealed partial class OrgPage : Page
{
    public enum OrgTab { Sites, Resources, Employees, Payroll }

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private readonly OrgBook _org = ProjectStore.Current.Org;
    private readonly ObservableCollection<SiteRowVm> _siteRows = new();
    private readonly ObservableCollection<ResourceRowVm> _resRows = new();
    private readonly ObservableCollection<EmployeeRowVm> _empRows = new();
    private readonly ObservableCollection<PayrollRowVm> _payRows = new();
    private readonly OrgTab _initialTab;
    private bool _loading;

    public OrgPage(OrgTab tab = OrgTab.Sites)
    {
        _initialTab = tab;
        InitializeComponent();
        _org.EnsureSeeded();
        _loading = true;
        foreach (var s in _org.Sites) _siteRows.Add(new SiteRowVm(s));
        SitesGrid.ItemsSource = _siteRows;
        foreach (var r in _org.Resources) _resRows.Add(new ResourceRowVm(r));
        ResourcesGrid.ItemsSource = _resRows;
        foreach (var e in _org.Employees) _empRows.Add(new EmployeeRowVm(e, _org));
        EmployeesGrid.ItemsSource = _empRows;
        PayrollGrid.ItemsSource = _payRows;
        MonthBox.Text = DateTime.Today.ToString("yyyy-MM", Inv);
        WorkingDaysBox.Value = _org.WorkingDays;
        _loading = false;

        Loaded += (_, _) =>
        {
            MainPivot.SelectedIndex = _initialTab switch
            { OrgTab.Resources => 1, OrgTab.Employees => 2, OrgTab.Payroll => 3, _ => 0 };
            LoadPayroll();
            UpdateSummary();
        };
    }

    private void UpdateSummary()
    {
        SummaryText.Text = $"{_org.Sites.Count} site(s) · {_org.Resources.Count} resource(s) · "
            + $"{_org.Employees.Count(e => e.Active)} active employee(s).";
    }

    private void Pivot_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (MainPivot.SelectedIndex == 3) LoadPayroll();
    }

    // ---- Sites ----
    private void AddSite_Click(object sender, RoutedEventArgs e)
    {
        var s = new Site { Name = "New site" };
        _org.Sites.Add(s); _siteRows.Add(new SiteRowVm(s));
        ProjectStore.Current.Notify(); UpdateSummary();
    }
    private void DeleteSite_Click(object sender, RoutedEventArgs e)
    {
        if (SitesGrid.SelectedItem is not SiteRowVm vm) return;
        _org.Sites.Remove(vm.S); _siteRows.Remove(vm);
        ProjectStore.Current.Notify(); UpdateSummary();
    }

    // ---- Resources ----
    private void AddResource_Click(object sender, RoutedEventArgs e)
    {
        var r = new Resource { Kind = "Labour", Name = "New resource", Unit = "day" };
        _org.Resources.Add(r); _resRows.Add(new ResourceRowVm(r));
        ProjectStore.Current.Notify(); UpdateSummary();
    }
    private void DeleteResource_Click(object sender, RoutedEventArgs e)
    {
        if (ResourcesGrid.SelectedItem is not ResourceRowVm vm) return;
        _org.Resources.Remove(vm.R); _resRows.Remove(vm);
        ProjectStore.Current.Notify(); UpdateSummary();
    }

    // ---- Employees ----
    private void AddEmployee_Click(object sender, RoutedEventArgs e)
    {
        var emp = new Employee { Code = $"E{_org.Employees.Count + 1:00}", Name = "New employee", WageType = "Monthly" };
        _org.Employees.Add(emp); _empRows.Add(new EmployeeRowVm(emp, _org));
        ProjectStore.Current.Notify(); UpdateSummary();
    }
    private void DeleteEmployee_Click(object sender, RoutedEventArgs e)
    {
        if (EmployeesGrid.SelectedItem is not EmployeeRowVm vm) return;
        _org.Employees.Remove(vm.E); _empRows.Remove(vm);
        ProjectStore.Current.Notify(); UpdateSummary();
    }

    // ---- Payroll ----
    private string CurrentMonth => string.IsNullOrWhiteSpace(MonthBox.Text) ? DateTime.Today.ToString("yyyy-MM", Inv) : MonthBox.Text.Trim();

    private void WorkingDays_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading) return;
        _org.WorkingDays = double.IsNaN(WorkingDaysBox.Value) ? 26 : WorkingDaysBox.Value;
        ProjectStore.Current.Notify();
        foreach (var r in _payRows) r.Recompute();
        UpdatePayrollSummary();
    }

    private void LoadPayroll_Click(object sender, RoutedEventArgs e) => LoadPayroll();

    private void LoadPayroll()
    {
        _payRows.Clear();
        string month = CurrentMonth;
        foreach (var emp in _org.Employees.Where(e => e.Active))
            _payRows.Add(new PayrollRowVm(emp, _org.GetPayroll(emp.Id, month), _org, UpdatePayrollSummary));
        UpdatePayrollSummary();
    }

    private void UpdatePayrollSummary()
    {
        double gross = _payRows.Sum(r => r.GrossValue);
        double adv = _payRows.Sum(r => r.AdvanceValue);
        PayrollSummary.Text = $"{CurrentMonth} · {_payRows.Count} employee(s) · gross {gross:N2} · advance {adv:N2} · net {gross - adv:N2}";
    }

    private void PayrollGrid_CellEditEnded(object sender, DataGridCellEditEndedEventArgs e)
    {
        ProjectStore.Current.Notify();
        UpdatePayrollSummary();
    }

    private async void ExportPayroll_Click(object sender, RoutedEventArgs e)
    {
        if (_payRows.Count == 0) { AppNotify.Info("Nothing to export", "Add employees and load a month first."); return; }
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow!));
        picker.SuggestedFileName = $"payroll_{CurrentMonth}";
        picker.FileTypeChoices.Add("PDF", new List<string> { ".pdf" });
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;
        if (PdfExport.ExportPayroll(file.Path, ProjectStore.Current, CurrentMonth, out var err))
            AppNotify.Success("Payroll exported", file.Name);
        else AppNotify.Error("Export failed", err ?? "Could not write PDF.");
    }
}

// ---------------- view-models ----------------
public sealed class SiteRowVm : INotifyPropertyChanged
{
    public Site S { get; }
    public SiteRowVm(Site s) { S = s; }
    public string Name { get => S.Name; set { S.Name = value ?? ""; OnP(); } }
    public string Location { get => S.Location; set { S.Location = value ?? ""; OnP(); } }
    public string Manager { get => S.Manager; set { S.Manager = value ?? ""; OnP(); } }
    public string Status { get => S.Status; set { S.Status = value ?? ""; OnP(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnP([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class ResourceRowVm : INotifyPropertyChanged
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public Resource R { get; }
    public ResourceRowVm(Resource r) { R = r; }
    public string Kind
    {
        get => R.Kind;
        set { R.Kind = Resource.Kinds.FirstOrDefault(k => k.Equals((value ?? "").Trim(), StringComparison.OrdinalIgnoreCase)) ?? R.Kind; OnP(); }
    }
    public string Name { get => R.Name; set { R.Name = value ?? ""; OnP(); } }
    public string Unit { get => R.Unit; set { R.Unit = value ?? ""; OnP(); } }
    public string RateText { get => R.Rate.ToString("0.##", Inv); set { if (double.TryParse(value, NumberStyles.Float, Inv, out var d)) R.Rate = d; OnP(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnP([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class EmployeeRowVm : INotifyPropertyChanged
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private readonly OrgBook _org;
    public Employee E { get; }
    public EmployeeRowVm(Employee e, OrgBook org) { E = e; _org = org; }
    public string Code { get => E.Code; set { E.Code = value ?? ""; OnP(); } }
    public string Name { get => E.Name; set { E.Name = value ?? ""; OnP(); } }
    public string Designation { get => E.Designation; set { E.Designation = value ?? ""; OnP(); } }
    public string Site
    {
        get => string.IsNullOrWhiteSpace(E.SiteId) ? "" : _org.SiteName(E.SiteId);
        set
        {
            var s = _org.Sites.FirstOrDefault(x => x.Name.Equals((value ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
            E.SiteId = s?.Id ?? "";
            OnP();
        }
    }
    public string WageType
    {
        get => E.WageType;
        set { E.WageType = (value ?? "").TrimStart().StartsWith("D", StringComparison.OrdinalIgnoreCase) ? "Daily" : "Monthly"; OnP(); }
    }
    public string RateText { get => E.Rate.ToString("0.##", Inv); set { if (double.TryParse(value, NumberStyles.Float, Inv, out var d)) E.Rate = d; OnP(); } }
    public string Phone { get => E.Phone; set { E.Phone = value ?? ""; OnP(); } }
    public string ActiveText
    {
        get => E.Active ? "Yes" : "No";
        set { E.Active = (value ?? "").TrimStart().StartsWith("Y", StringComparison.OrdinalIgnoreCase); OnP(); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnP([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class PayrollRowVm : INotifyPropertyChanged
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private readonly Employee _e;
    private readonly PayrollRecord _rec;
    private readonly OrgBook _org;
    private readonly Action _changed;
    public PayrollRowVm(Employee e, PayrollRecord rec, OrgBook org, Action changed) { _e = e; _rec = rec; _org = org; _changed = changed; }

    public string Code => _e.Code;
    public string Name => _e.Name;
    public string Designation => _e.Designation;
    public string Basis => _e.WageType.Equals("Daily", StringComparison.OrdinalIgnoreCase)
        ? $"Daily {_e.Rate:0.##}" : $"Monthly {_e.Rate:0.##}";

    public double GrossValue => _org.Gross(_e, _rec.DaysPresent);
    public double AdvanceValue => _rec.Advance;
    public double NetValue => GrossValue - _rec.Advance;

    public string DaysText
    {
        get => _rec.DaysPresent.ToString("0.#", Inv);
        set { if (double.TryParse(value, NumberStyles.Float, Inv, out var d)) _rec.DaysPresent = d; Recompute(); _changed(); }
    }
    public string AdvanceText
    {
        get => _rec.Advance.ToString("0.##", Inv);
        set { if (double.TryParse(value, NumberStyles.Float, Inv, out var d)) _rec.Advance = d; Recompute(); _changed(); }
    }
    public string GrossText => GrossValue.ToString("N2", Inv);
    public string NetText => NetValue.ToString("N2", Inv);

    public void Recompute()
    {
        OnP(nameof(DaysText)); OnP(nameof(AdvanceText)); OnP(nameof(GrossText)); OnP(nameof(NetText));
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnP([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
