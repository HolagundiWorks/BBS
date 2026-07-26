using System.Text.Json.Nodes;

namespace BBSApp.Services;

/// <summary>Per-project identity for reports, estimates, and title bar.</summary>
public sealed class ProjectInfo
{
    public string Name { get; set; } = "Untitled Project";
    public string Location { get; set; } = "";
    public string ClientName { get; set; } = "";
    /// <summary>Engineer, Architect, or PMC.</summary>
    public string PreparedByRole { get; set; } = "Engineer";
    public string PreparedByName { get; set; } = "";
    public string CompanyName { get; set; } = Branding.Company;
    public string ContactPhone { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public string Address { get; set; } = "";
    /// <summary>Absolute or relative path to project logo image (png/jpg).</summary>
    public string LogoPath { get; set; } = "";

    public static readonly string[] PreparedByRoles = { "Engineer", "Architect", "PMC" };

    public JsonObject ToJson() => new()
    {
        ["name"] = Name,
        ["location"] = Location,
        ["client_name"] = ClientName,
        ["prepared_by_role"] = PreparedByRole,
        ["prepared_by_name"] = PreparedByName,
        ["company_name"] = CompanyName,
        ["contact_phone"] = ContactPhone,
        ["contact_email"] = ContactEmail,
        ["address"] = Address,
        ["logo_path"] = LogoPath
    };

    public void LoadFrom(JsonObject? o)
    {
        if (o is null) return;
        Name = o["name"]?.GetValue<string>() ?? Name;
        Location = o["location"]?.GetValue<string>() ?? "";
        ClientName = o["client_name"]?.GetValue<string>() ?? "";
        PreparedByRole = o["prepared_by_role"]?.GetValue<string>() ?? "Engineer";
        PreparedByName = o["prepared_by_name"]?.GetValue<string>() ?? "";
        CompanyName = o["company_name"]?.GetValue<string>() ?? Branding.Company;
        ContactPhone = o["contact_phone"]?.GetValue<string>() ?? "";
        ContactEmail = o["contact_email"]?.GetValue<string>() ?? "";
        Address = o["address"]?.GetValue<string>() ?? "";
        LogoPath = o["logo_path"]?.GetValue<string>() ?? "";
    }

    public void Reset()
    {
        Name = "Untitled Project";
        Location = "";
        ClientName = "";
        PreparedByRole = "Engineer";
        PreparedByName = "";
        CompanyName = Branding.Company;
        ContactPhone = "";
        ContactEmail = "";
        Address = "";
        LogoPath = "";
    }

    public string PreparedByLine
    {
        get
        {
            string role = string.IsNullOrWhiteSpace(PreparedByRole) ? "Engineer" : PreparedByRole.Trim();
            if (string.IsNullOrWhiteSpace(PreparedByName)) return $"Prepared by {role}";
            return $"Prepared by {role}: {PreparedByName.Trim()}";
        }
    }

    public string CompanyDisplay =>
        string.IsNullOrWhiteSpace(CompanyName) ? Branding.CompanyLine : CompanyName.Trim();

    public static string LogosDirectory => Branding.ResolveAppDataSubdir("project-logos");

    /// <summary>Copy source image into app LocalAppData and return the stored path.</summary>
    public static async Task<string> ImportLogoAsync(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new FileNotFoundException("Logo file not found.", sourcePath);

        var ext = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".png";
        var destName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var dest = Path.Combine(LogosDirectory, destName);
        await Task.Run(() => File.Copy(sourcePath, dest, overwrite: true));
        return dest;
    }

    public static string? ResolveLogoFile(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return null;
        if (File.Exists(stored)) return stored;

        var proj = ProjectStore.Current.FilePath;
        if (!string.IsNullOrWhiteSpace(proj))
        {
            var projDir = Path.GetDirectoryName(proj);
            if (!string.IsNullOrEmpty(projDir))
            {
                var combined = Path.GetFullPath(Path.Combine(projDir, stored));
                if (File.Exists(combined)) return combined;
            }
        }

        var local = Path.Combine(LogosDirectory, Path.GetFileName(stored));
        if (File.Exists(local)) return local;

        var legacy = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Branding.LegacyAppDataFolder, "project-logos", Path.GetFileName(stored));
        return File.Exists(legacy) ? legacy : null;
    }

    public string? ResolvedLogoPath => ResolveLogoFile(LogoPath);
}
