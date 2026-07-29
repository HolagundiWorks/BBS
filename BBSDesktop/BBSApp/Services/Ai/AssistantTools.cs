using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic.Models.Messages;
using BBSApp.Services;

namespace BBSApp.Services.Ai;

/// <summary>
/// The assistant's read-only tool surface (Phase 1): drive navigation, read a
/// project summary, and run the deterministic engine for an RCC member kind.
/// Every handler wraps an existing service — no new business logic lives here,
/// and nothing here mutates project data.
/// </summary>
public sealed class AssistantTools
{
    // Member kinds the C++ engine's generate path understands (bbs_c_api.h).
    private static readonly string[] EngineKinds =
        { "columns", "beams", "pedestals", "lintels", "slabs", "footings", "walls", "stairs" };

    private readonly IAppCommandBus _bus;
    private readonly IReadOnlyList<Tool> _definitions;

    public AssistantTools(IAppCommandBus bus)
    {
        _bus = bus;
        _definitions = BuildDefinitions();
    }

    public IReadOnlyList<Tool> Definitions => _definitions;

    /// <summary>Execute a tool by name. Must be called on the UI thread — touches ProjectStore and navigation.</summary>
    public string Execute(string name, IReadOnlyDictionary<string, JsonElement> input) => name switch
    {
        "navigate" => Navigate(input),
        "get_project_summary" => ProjectSummary(),
        "run_calc" => RunCalc(input),
        _ => $"Unknown tool: {name}"
    };

    private IReadOnlyList<Tool> BuildDefinitions() => new[]
    {
        new Tool
        {
            Name = "navigate",
            Description = "Open a page in the app by its ribbon tag (e.g. \"columns\", "
                        + "\"masonry\", \"estimate\", \"report\"). Returns whether navigation "
                        + "succeeded; on failure the reply lists the known tags.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["tag"] = Schema(new { type = "string", description = "Ribbon command tag to navigate to." })
                },
                Required = new List<string> { "tag" }
            }
        },
        new Tool
        {
            Name = "get_project_summary",
            Description = "Return a JSON summary of the current project: name, active persona, "
                        + "levels, per-module row counts, estimate markups, and whether an estimate "
                        + "has been calculated. Read-only.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>()
            }
        },
        new Tool
        {
            Name = "run_calc",
            Description = "Run the deterministic C++ engine for one RCC member kind, using the "
                        + "project's current rows and settings, and return its summary and "
                        + "design-check tables. This is the trustworthy source for quantities and "
                        + "checks — never compute them yourself. Read-only (does not modify the project).",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["kind"] = Schema(new { type = "string", @enum = EngineKinds, description = "RCC member kind to compute." })
                },
                Required = new List<string> { "kind" }
            }
        }
    };

    private string Navigate(IReadOnlyDictionary<string, JsonElement> input)
    {
        string tag = GetString(input, "tag");
        if (string.IsNullOrWhiteSpace(tag)) return "No tag supplied.";
        return _bus.Navigate(tag)
            ? $"Navigated to \"{tag}\"."
            : $"Unknown tag \"{tag}\". Known tags: {string.Join(", ", _bus.KnownTags)}.";
    }

    private static string ProjectSummary()
    {
        var s = ProjectStore.Current;

        var levels = new JsonArray();
        foreach (var l in s.Levels)
            levels.Add(new JsonObject { ["id"] = l.Id, ["name"] = l.Name, ["height_mm"] = l.HeightMm });

        var p = s.Parties.ActiveParty;
        string who = string.IsNullOrWhiteSpace(p.Company) ? p.Role.Display() : p.Company;

        var summary = new JsonObject
        {
            ["project"] = s.Name,
            ["persona"] = $"{p.Role.Display()} · {who}",
            ["levels"] = levels,
            ["rcc_members"] = new JsonObject
            {
                ["columns"] = s.Columns.Count,
                ["beams"] = s.Beams.Count,
                ["pedestals"] = s.Pedestals.Count,
                ["lintels"] = s.Lintels.Count,
                ["slabs"] = s.Slabs.Count,
                ["footings"] = s.Footings.Count,
                ["walls"] = s.Walls.Count,
                ["stairs"] = s.Stairs.Count
            },
            ["civil_items"] = new JsonObject
            {
                ["masonry"] = s.MasonryWalls.Count,
                ["plaster"] = s.Plaster.Count,
                ["pcc"] = s.PccBeds.Count,
                ["earthwork"] = s.Earthwork.Count,
                ["ssm"] = s.SizeStone.Count,
                ["flooring"] = s.Flooring.Count,
                ["painting"] = s.Painting.Count,
                ["doors"] = s.Doors.Count,
                ["windows"] = s.Windows.Count
            },
            ["estimate_markups"] = s.Markups.ToJson(),
            ["concrete_from_rmc"] = s.ConcreteFromRmc,
            ["has_estimate"] = s.LastEstimate != null
        };
        return summary.ToJsonString();
    }

    private static string RunCalc(IReadOnlyDictionary<string, JsonElement> input)
    {
        string kind = GetString(input, "kind").Trim().ToLowerInvariant();
        var col = CollectionFor(kind);
        if (col is null)
            return $"run_calc supports RCC kinds only: {string.Join(", ", EngineKinds)}.";
        if (col.Count == 0)
            return $"No {kind} rows in the project yet.";

        try
        {
            // Clone rows first — ExpandForGenerate/StampDefaults mutate their input,
            // and run_calc must not change the project.
            var clones = col
                .Select(r => new Dictionary<string, string>(r, StringComparer.OrdinalIgnoreCase))
                .ToList();
            var expanded = MemberSheetHelper.ExpandForGenerate(kind, clones);
            var res = EngineClient.Generate(
                MemberSheetHelper.EngineKind(kind), ProjectStore.Current.SettingsJson(), expanded);

            if (!res.Ok)
                return $"Engine error for {kind}: {res.Error}";

            var outp = new JsonObject
            {
                ["kind"] = kind,
                ["members"] = expanded.Count,
                ["summary"] = TableToJson(res.Summary),
                ["checks"] = TableToJson(res.Checks)
            };
            return outp.ToJsonString();
        }
        catch (Exception ex)
        {
            return $"Could not run the engine for {kind}: {ex.Message}";
        }
    }

    private static JsonObject TableToJson(GenTable? t)
    {
        var headers = new JsonArray();
        var rows = new JsonArray();
        if (t is not null)
        {
            foreach (var h in t.Headers) headers.Add(h);
            foreach (var r in t.Rows)
            {
                var row = new JsonArray();
                foreach (var cell in r) row.Add(cell);
                rows.Add(row);
            }
        }
        return new JsonObject { ["headers"] = headers, ["rows"] = rows };
    }

    private static ObservableCollection<Dictionary<string, string>>? CollectionFor(string kind)
    {
        var s = ProjectStore.Current;
        return kind switch
        {
            "columns" => s.Columns,
            "beams" => s.Beams,
            "pedestals" => s.Pedestals,
            "lintels" => s.Lintels,
            "slabs" => s.Slabs,
            "footings" => s.Footings,
            "walls" => s.Walls,
            "stairs" => s.Stairs,
            _ => null
        };
    }

    private static JsonElement Schema(object shape) => JsonSerializer.SerializeToElement(shape);

    private static string GetString(IReadOnlyDictionary<string, JsonElement> input, string key)
        => input.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? ""
            : "";
}
