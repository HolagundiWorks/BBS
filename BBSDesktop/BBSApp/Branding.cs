namespace BBSApp;

/// <summary>Product branding — AQC-Core by Human Centric Works, Hospet.</summary>
public static class Branding
{
    public const string AppName = "AQC-Core";
    public const string FullName = "Accelerated Quantity and Costing Core";
    public const string Company = "Human Centric Works";
    public const string Location = "Hospet";
    public const string CompanyLine = "Human Centric Works, Hospet";
    public const string Tagline = "Accelerated Quantity and Costing Core — RCC, masonry, finishes, estimate & cost";
    public const string DevelopedBy = "Developed by Human Centric Works, Hospet";
    public const string Copyright = "© Human Centric Works, Hospet";
    public const string LicenseName = "GNU AGPL v3 (Community) or Commercial";
    public const string LicenseUrl = "https://www.gnu.org/licenses/agpl-3.0.html";

    /// <summary>%LocalAppData% folder for app data (rate books, logos).</summary>
    public const string AppDataFolder = "AQCCore";
    /// <summary>Legacy folder name when migrating from BOQ Core.</summary>
    public const string LegacyAppDataFolder = "BOQCore";

    public static string WindowTitle(string? projectName = null, bool dirty = false)
    {
        string baseTitle = string.IsNullOrWhiteSpace(projectName)
            ? AppName
            : $"{AppName} — {projectName}";
        return dirty ? $"{baseTitle} •" : baseTitle;
    }

    public static string AppDataDirectory
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppDataFolder);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>Preferred path under AQCCore; falls back to legacy BOQCore if that file already exists.</summary>
    public static string ResolveAppDataFile(string fileName)
    {
        var primary = Path.Combine(AppDataDirectory, fileName);
        if (File.Exists(primary)) return primary;

        var legacy = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            LegacyAppDataFolder, fileName);
        return File.Exists(legacy) ? legacy : primary;
    }

    public static string ResolveAppDataSubdir(string subdir)
    {
        var primary = Path.Combine(AppDataDirectory, subdir);
        Directory.CreateDirectory(primary);
        return primary;
    }
}
