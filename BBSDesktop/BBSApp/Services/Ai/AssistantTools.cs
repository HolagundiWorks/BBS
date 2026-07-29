using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic.Models.Messages;
using BBSApp.Services;

namespace BBSApp.Services.Ai;

/// <summary>
/// The assistant's tool surface. Read tools (navigate, get_project_summary,
/// run_calc) wrap existing services directly. Write tools (add_element_row,
/// update_setting) apply in-memory changes only after the user approves them
/// through <see cref="IAssistantConfirm"/>. Nothing here saves to disk, and the
/// C++ engine remains the source of truth for all numbers.
/// </summary>
public sealed class AssistantTools
{
    // Member kinds the C++ engine's generate path understands (bbs_c_api.h).
    private static readonly string[] EngineKinds =
        { "columns", "beams", "pedestals", "lintels", "slabs", "footings", "walls", "stairs" };

    private static readonly string[] SettingPaths =
    {
        "cover.column", "cover.beam", "cover.slab", "cover.footing", "cover.pedestal", "cover.lintel",
        "markup.electrical", "markup.plumbing", "markup.escalation", "markup.consulting",
        "concrete_from_rmc"
    };

    private readonly IAppCommandBus _bus;
    private readonly IAssistantConfirm _confirm;
    private readonly IReadOnlyList<Tool> _definitions;

    public AssistantTools(IAppCommandBus bus, IAssistantConfirm confirm)
    {
        _bus = bus;
        _confirm = confirm;
        _definitions = BuildDefinitions();
    }

    public IReadOnlyList<Tool> Definitions => _definitions;

    /// <summary>Execute a tool by name. Must run on the UI thread — touches ProjectStore, navigation, and dialogs.</summary>
    public Task<string> ExecuteAsync(string name, IReadOnlyDictionary<string, JsonElement> input) => name switch
    {
        "navigate" => Task.FromResult(Navigate(input)),
        "get_project_summary" => Task.FromResult(ProjectSummary()),
        "run_calc" => Task.FromResult(RunCalc(input)),
        "add_element_row" => AddElementRowAsync(input),
        "update_setting" => UpdateSettingAsync(input),
        _ => Task.FromResult($"Unknown tool: {name}")
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
        },
        new Tool
        {
            Name = "add_element_row",
            Description = "Add one RCC member row (columns, beams, pedestals, lintels, slabs, "
                        + "footings, walls, stairs) to the project. Put only the fields you want to "
                        + "set in `fields`; the rest are inherited from the last row of that kind "
                        + "(or defaults), and the mark is auto-assigned. The user must approve the "
                        + "change in a dialog before it is applied — do not ask in chat first.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["kind"] = Schema(new { type = "string", @enum = EngineKinds, description = "RCC member kind." }),
                    ["fields"] = Schema(new
                    {
                        type = "object",
                        description = "Field key/value overrides for the new row (values as strings).",
                        additionalProperties = new { type = "string" }
                    })
                },
                Required = new List<string> { "kind" }
            }
        },
        new Tool
        {
            Name = "update_setting",
            Description = "Change one project setting: RCC nominal covers in mm (cover.column …), "
                        + "estimate markup percentages (markup.electrical …), or the "
                        + "concrete-from-RMC flag. The user must approve the change in a dialog "
                        + "before it is applied — do not ask in chat first.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["path"] = Schema(new { type = "string", @enum = SettingPaths, description = "Setting to change." }),
                    ["value"] = Schema(new { type = "string", description = "New value: a number for covers/markups, or true/false for concrete_from_rmc." })
                },
                Required = new List<string> { "path", "value" }
            }
        }
    };

    // ——— Read tools ———

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
        if (col is null || !EngineKinds.Contains(kind))
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

    // ——— Write tools (confirmed) ———

    private async Task<string> AddElementRowAsync(IReadOnlyDictionary<string, JsonElement> input)
    {
        string kind = GetString(input, "kind").Trim().ToLowerInvariant();
        var col = CollectionFor(kind);
        if (col is null || !EngineKinds.Contains(kind))
            return $"add_element_row supports RCC kinds only: {string.Join(", ", EngineKinds)}.";

        // Seed from the last existing row so grades/section/level carry over.
        var row = col.Count > 0
            ? new Dictionary<string, string>(col[^1], StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var setKeys = new List<string>();
        if (input.TryGetValue("fields", out var fieldsEl) && fieldsEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in fieldsEl.EnumerateObject())
            {
                row[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString() ?? ""
                    : prop.Value.GetRawText();
                setKeys.Add(prop.Name);
            }
        }

        row["nos"] = row.TryGetValue("nos", out var nos) && !string.IsNullOrWhiteSpace(nos) ? nos : "1";
        MemberSheetHelper.StampDefaults(kind, row);

        bool callerMark = row.TryGetValue("mark", out var mk) && !string.IsNullOrWhiteSpace(mk);
        bool dupMark = callerMark && col.Any(r =>
            r.TryGetValue("mark", out var em) && em.Equals(mk, StringComparison.OrdinalIgnoreCase));
        if (!callerMark || dupMark)
            row["mark"] = MemberSheetHelper.SuggestNextMark(kind, col, row);

        if (!await _confirm.ConfirmAsync($"Add {Singular(kind)} to the project?", DescribeRow(kind, row, setKeys)))
            return $"Cancelled — no {Singular(kind)} added.";

        col.Add(row);
        ProjectStore.Current.Notify();
        AppNotify.Success($"{Singular(kind)} added", row.GetValueOrDefault("mark", kind));
        _bus.Navigate(kind);
        return $"Added a {Singular(kind)} (mark {row.GetValueOrDefault("mark", "?")}). "
             + $"The project now has {col.Count} {kind}.";
    }

    private async Task<string> UpdateSettingAsync(IReadOnlyDictionary<string, JsonElement> input)
    {
        string path = GetString(input, "path").Trim().ToLowerInvariant();
        string value = GetString(input, "value").Trim();
        if (string.IsNullOrWhiteSpace(path)) return "No setting path supplied.";

        var s = ProjectStore.Current;

        var doubles = new Dictionary<string, (Func<double> Get, Action<double> Set)>(StringComparer.OrdinalIgnoreCase)
        {
            ["cover.column"] = (() => s.CoverColumnMm, v => s.CoverColumnMm = v),
            ["cover.beam"] = (() => s.CoverBeamMm, v => s.CoverBeamMm = v),
            ["cover.slab"] = (() => s.CoverSlabMm, v => s.CoverSlabMm = v),
            ["cover.footing"] = (() => s.CoverFootingMm, v => s.CoverFootingMm = v),
            ["cover.pedestal"] = (() => s.CoverPedestalMm, v => s.CoverPedestalMm = v),
            ["cover.lintel"] = (() => s.CoverLintelMm, v => s.CoverLintelMm = v),
            ["markup.electrical"] = (() => s.Markups.ElectricalPct, v => s.Markups.ElectricalPct = v),
            ["markup.plumbing"] = (() => s.Markups.PlumbingPct, v => s.Markups.PlumbingPct = v),
            ["markup.escalation"] = (() => s.Markups.EscalationPct, v => s.Markups.EscalationPct = v),
            ["markup.consulting"] = (() => s.Markups.ConsultingFeePct, v => s.Markups.ConsultingFeePct = v),
        };

        if (doubles.TryGetValue(path, out var acc))
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) || d < 0)
                return $"Could not read a non-negative number from \"{value}\".";
            string cur = acc.Get().ToString("0.###", CultureInfo.InvariantCulture);
            string nw = d.ToString("0.###", CultureInfo.InvariantCulture);
            if (!await _confirm.ConfirmAsync("Change setting?", $"{path}: {cur} → {nw}"))
                return "Cancelled — setting unchanged.";
            acc.Set(d);
            ProjectStore.Current.Notify();
            AppNotify.Success("Setting updated", $"{path} = {nw}");
            return $"Set {path} to {nw} (was {cur}).";
        }

        if (path == "concrete_from_rmc")
        {
            if (!TryParseBool(value, out var b))
                return $"Could not read true/false from \"{value}\".";
            string cur = s.ConcreteFromRmc ? "true" : "false";
            string nw = b ? "true" : "false";
            if (!await _confirm.ConfirmAsync("Change setting?", $"concrete_from_rmc: {cur} → {nw}"))
                return "Cancelled — setting unchanged.";
            s.ConcreteFromRmc = b;
            ProjectStore.Current.Notify();
            AppNotify.Success("Setting updated", $"concrete_from_rmc = {nw}");
            return $"Set concrete_from_rmc to {nw} (was {cur}).";
        }

        return $"Unknown setting \"{path}\". Supported: {string.Join(", ", SettingPaths)}.";
    }

    // ——— Helpers ———

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

    private static string DescribeRow(string kind, Dictionary<string, string> row, List<string> setKeys)
    {
        string head = $"{kind} · mark {row.GetValueOrDefault("mark", "?")}"
                    + (row.TryGetValue("level", out var lv) ? $", level {lv}" : "");
        string body = setKeys.Count > 0
            ? "You set: " + string.Join(", ", setKeys.Select(k => $"{k}={row.GetValueOrDefault(k, "")}"))
            : "Using inherited values / defaults.";
        return head + "\n" + body;
    }

    private static string Singular(string kind) =>
        kind.EndsWith("s", StringComparison.OrdinalIgnoreCase) ? kind[..^1] : kind;

    private static bool TryParseBool(string v, out bool b)
    {
        switch (v.Trim().ToLowerInvariant())
        {
            case "true": case "yes": case "1": case "on": b = true; return true;
            case "false": case "no": case "0": case "off": b = false; return true;
            default: b = false; return false;
        }
    }

    private static JsonElement Schema(object shape) => JsonSerializer.SerializeToElement(shape);

    private static string GetString(IReadOnlyDictionary<string, JsonElement> input, string key)
        => input.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? ""
            : "";
}
