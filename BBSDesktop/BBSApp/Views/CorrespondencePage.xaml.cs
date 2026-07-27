using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BBSApp.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace BBSApp.Views;

public sealed partial class CorrespondencePage : Page
{
    private readonly OfficeRegister _reg = ProjectStore.Current.Office;
    private readonly ObservableCollection<OfficeDocRow> _rows = new();
    private OfficeDocument? _current;
    private bool _loading;

    public CorrespondencePage()
    {
        InitializeComponent();
        _loading = true;
        NewTypeBox.ItemsSource = DocTypeInfo.All;
        NewTypeBox.SelectedIndex = 0;
        TypeBox.ItemsSource = DocTypeInfo.All;
        PrefixBox.Text = _reg.Prefix;
        DocList.ItemsSource = _rows;
        RebuildRows();
        _loading = false;

        Loaded += (_, _) =>
        {
            if (_rows.Count > 0) DocList.SelectedIndex = 0;
            else LoadEditor(null);
            UpdateSummary();
        };
    }

    private string Company => ProjectStore.Current.Info.CompanyDisplay;

    private void RebuildRows()
    {
        _rows.Clear();
        foreach (var d in _reg.Documents) _rows.Add(new OfficeDocRow(_reg, d, Company));
    }

    private void UpdateSummary()
    {
        int fin = _reg.Documents.Count(d => d.Finalized);
        SummaryText.Text = _reg.Documents.Count == 0
            ? "No documents yet — pick a type and click New. Numbers are assigned on Finalize."
            : $"{_reg.Documents.Count} document(s) · {fin} finalized · {_reg.Documents.Count - fin} draft.";
    }

    private OfficeDocRow? RowFor(OfficeDocument d) => _rows.FirstOrDefault(r => r.Doc == d);

    // ---- selection / editor ----
    private void DocList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        SaveEditor();                       // persist edits to the doc we're leaving
        _current = (DocList.SelectedItem as OfficeDocRow)?.Doc;
        LoadEditor(_current);
    }

    private void LoadEditor(OfficeDocument? doc)
    {
        _loading = true;
        _current = doc;
        bool has = doc is not null;
        EditorPanel.Opacity = has ? 1 : 0.5;
        if (doc is null)
        {
            TypeBox.SelectedItem = DocTypeInfo.All[0];
            DatePicker.Date = DateTimeOffset.Now;
            ToNameBox.Text = ToAddressBox.Text = SubjectBox.Text = BodyBox.Text = "";
            SignNameBox.Text = SignRoleBox.Text = "";
            NumberText.Text = "—";
            SetLocked(true);
            _loading = false;
            return;
        }
        TypeBox.SelectedItem = DocTypeInfo.Find(doc.TypeCode);
        DatePicker.Date = new DateTimeOffset(doc.IssueDate);
        ToNameBox.Text = doc.ToName;
        ToAddressBox.Text = doc.ToAddress;
        SubjectBox.Text = doc.Subject;
        BodyBox.Text = doc.Body;
        SignNameBox.Text = doc.SignatoryName;
        SignRoleBox.Text = doc.SignatoryRole;
        UpdateNumberText();
        UpdateToVisibility();
        SetLocked(doc.Finalized);
        _loading = false;
    }

    private void SaveEditor()
    {
        if (_current is null || _current.Finalized) return;
        if (TypeBox.SelectedItem is DocTypeInfo t) _current.TypeCode = t.Code;
        if (DatePicker.Date is { } dto) _current.IssueDate = dto.DateTime.Date;
        _current.ToName = ToNameBox.Text ?? "";
        _current.ToAddress = ToAddressBox.Text ?? "";
        _current.Subject = SubjectBox.Text ?? "";
        _current.Body = BodyBox.Text ?? "";
        _current.SignatoryName = SignNameBox.Text ?? "";
        _current.SignatoryRole = SignRoleBox.Text ?? "";
        RowFor(_current)?.Refresh();
    }

    private void SetLocked(bool locked)
    {
        bool en = !locked;
        TypeBox.IsEnabled = en;
        DatePicker.IsEnabled = en;
        ToNameBox.IsEnabled = en;
        ToAddressBox.IsEnabled = en;
        SubjectBox.IsEnabled = en;
        BodyBox.IsReadOnly = locked;
        SignNameBox.IsEnabled = en;
        SignRoleBox.IsEnabled = en;
        LockBar.IsOpen = locked;
    }

    private void UpdateToVisibility()
    {
        string code = (TypeBox.SelectedItem as DocTypeInfo)?.Code ?? "LTR";
        ToPanel.Visibility = DocTypeInfo.HasRecipient(code) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateNumberText()
    {
        if (_current is null) { NumberText.Text = "—"; return; }
        NumberText.Text = _current.Finalized && !string.IsNullOrWhiteSpace(_current.Number)
            ? _current.Number
            : _reg.PreviewNumber(_current, Company) + " (draft)";
    }

    private void TypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _current is null) return;
        if (TypeBox.SelectedItem is DocTypeInfo t) _current.TypeCode = t.Code;
        UpdateToVisibility();
        UpdateNumberText();
    }

    // ---- toolbar ----
    private void New_Click(object sender, RoutedEventArgs e)
    {
        SaveEditor();
        string code = (NewTypeBox.SelectedItem as DocTypeInfo)?.Code ?? "LTR";
        var doc = new OfficeDocument
        {
            TypeCode = code,
            IssueDate = DateTime.Today,
            Body = DocTypeInfo.DefaultBody(code)
        };
        _reg.Documents.Add(doc);
        var row = new OfficeDocRow(_reg, doc, Company);
        _rows.Add(row);
        ProjectStore.Current.Notify();
        DocList.SelectedItem = row;
        UpdateSummary();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null) { AppNotify.Info("Nothing to save", "Create a document first."); return; }
        SaveEditor();
        UpdateNumberText();
        ProjectStore.Current.Notify();
        AppNotify.Success("Saved", DocTypeInfo.DisplayFor(_current.TypeCode));
    }

    private async void Finalize_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null) { AppNotify.Info("Nothing to finalize", "Create a document first."); return; }
        SaveEditor();
        if (_current.Finalized)
        {
            AppNotify.Info("Already finalized", _current.Number);
            return;
        }
        var dlg = new ContentDialog
        {
            Title = "Finalize & assign number",
            Content = $"Assign number {_reg.PreviewNumber(_current, Company)} and lock this "
                      + $"{DocTypeInfo.DisplayFor(_current.TypeCode)} for editing?",
            PrimaryButtonText = "Finalize",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        _reg.Finalize(_current, Company);
        RowFor(_current)?.Refresh();
        LoadEditor(_current);
        ProjectStore.Current.Notify();
        UpdateSummary();
        AppNotify.Success("Finalized", _current.Number);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null) return;
        var doc = _current;
        int idx = _rows.IndexOf(RowFor(doc)!);
        _reg.Documents.Remove(doc);
        var row = RowFor(doc);
        if (row is not null) _rows.Remove(row);
        ProjectStore.Current.Notify();
        UpdateSummary();
        if (_rows.Count > 0) DocList.SelectedIndex = Math.Clamp(idx, 0, _rows.Count - 1);
        else LoadEditor(null);
    }

    private void Template_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null || _current.Finalized) return;
        string code = (TypeBox.SelectedItem as DocTypeInfo)?.Code ?? "LTR";
        BodyBox.Text = DocTypeInfo.DefaultBody(code);
    }

    private void Prefix_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _reg.Prefix = (PrefixBox.Text ?? "").Trim();
        ProjectStore.Current.Notify();
        foreach (var r in _rows) r.Refresh();
        UpdateNumberText();
    }

    private async void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null) { AppNotify.Info("Nothing to export", "Create a document first."); return; }
        SaveEditor();
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow!));
        string baseName = string.IsNullOrWhiteSpace(_current.Number)
            ? $"{_current.TypeCode}_{_current.IssueDate:yyyyMMdd}"
            : _current.Number.Replace('/', '-');
        picker.SuggestedFileName = baseName;
        picker.FileTypeChoices.Add("PDF", new List<string> { ".pdf" });
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;
        if (PdfExport.ExportOfficeDocument(file.Path, ProjectStore.Current, _current, out var err))
            AppNotify.Success("Document exported", file.Name);
        else
            AppNotify.Error("Export failed", err ?? "Could not write PDF.");
    }
}

/// <summary>List-row wrapper for a document in the register.</summary>
public sealed class OfficeDocRow : INotifyPropertyChanged
{
    private readonly OfficeRegister _reg;
    private readonly string _company;
    public OfficeDocument Doc { get; }

    public OfficeDocRow(OfficeRegister reg, OfficeDocument doc, string company)
    {
        _reg = reg; Doc = doc; _company = company;
    }

    public string Header
    {
        get
        {
            string num = Doc.Finalized && !string.IsNullOrWhiteSpace(Doc.Number)
                ? Doc.Number
                : "(draft)";
            return $"{DocTypeInfo.DisplayFor(Doc.TypeCode)} · {num}";
        }
    }

    public string Sub
    {
        get
        {
            string subj = string.IsNullOrWhiteSpace(Doc.Subject) ? "(no subject)" : Doc.Subject;
            return $"{subj} · {Doc.IssueDate:dd MMM yyyy}";
        }
    }

    public void Refresh()
    {
        OnP(nameof(Header));
        OnP(nameof(Sub));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnP([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
