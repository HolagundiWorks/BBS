namespace BBSApp;

/// <summary>Product branding — BOQ Core by Human Centic Works, Hospet.</summary>
public static class Branding
{
    public const string AppName = "BOQ Core";
    public const string Company = "Human Centic Works";
    public const string Location = "Hospet";
    public const string CompanyLine = "Human Centic Works, Hospet";
    public const string Tagline = "BOQ quantity take-off — RCC, masonry, plaster, PCC, earthwork";
    public const string DevelopedBy = "Developed by Human Centic Works, Hospet";
    public const string Copyright = "© Human Centic Works, Hospet";

    public static string WindowTitle(string? projectName = null, bool dirty = false)
    {
        string baseTitle = string.IsNullOrWhiteSpace(projectName)
            ? AppName
            : $"{AppName} — {projectName}";
        return dirty ? $"{baseTitle} •" : baseTitle;
    }
}
