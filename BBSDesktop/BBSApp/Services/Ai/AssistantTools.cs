// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic;
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

    // Correspondence type codes (LTR, MEMO, …), derived so they can't drift from the register.
    private static readonly string[] DocCodes = DocTypeInfo.All.Select(t => t.Code).ToArray();

    // Linkable trade keys and derivation bases, derived from the registry so the
    // tool schema can never drift from LinkTradeRegistry / LinkBasisInfo.
    private static readonly string[] LinkTradeKeys = LinkTradeRegistry.All.Select(t => t.Key).ToArray();
    private static readonly string[] LinkBases = LinkBasisInfo.All.Select(LinkBasisInfo.Label).ToArray();

    private const long MaxPdfBytes = 20 * 1024 * 1024;

    private readonly IAppCommandBus _bus;
    private readonly IAssistantConfirm _confirm;
    private readonly AnthropicClient? _client;
    private readonly IReadOnlyList<Tool> _definitions;

    public AssistantTools(IAppCommandBus bus, IAssistantConfirm confirm, AnthropicClient? client)
    {
        _bus = bus;
        _confirm = confirm;
        _client = client;
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
        "create_correspondence" => CreateCorrespondenceAsync(input),
        "list_takeoff" => Task.FromResult(ListTakeoff()),
        "commit_opening" => CommitOpeningAsync(input),
        "read_drawing" => ReadDrawingAsync(input),
        "list_link_rules" => Task.FromResult(ListLinkRules()),
        "add_link_rule" => AddLinkRuleAsync(input),
        "apply_links" => ApplyLinksAsync(input),
        "list_levels" => Task.FromResult(ListLevels()),
        "add_level" => AddLevelAsync(input),
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
        },
        new Tool
        {
            Name = "create_correspondence",
            Description = "Add a drafted office document to the correspondence register as an "
                        + "editable draft. Types: LTR (letter), MEMO, NOTICE, CIRC (circular), "
                        + "CERT (certificate), DECL (declaration), SI (site instruction), WON "
                        + "(work-order note), IPC (interim payment certificate). Write the full "
                        + "body yourself and pass it in `body`. It is issued under the current "
                        + "persona and stays unnumbered until the user finalizes it. The user must "
                        + "approve in a dialog before it is added — do not ask in chat first.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["type"] = Schema(new { type = "string", @enum = DocCodes, description = "Document type code." }),
                    ["subject"] = Schema(new { type = "string", description = "Short subject line." }),
                    ["body"] = Schema(new { type = "string", description = "The full drafted body text." }),
                    ["to_name"] = Schema(new { type = "string", description = "Recipient name (for addressed types)." }),
                    ["to_address"] = Schema(new { type = "string", description = "Recipient address (optional)." })
                },
                Required = new List<string> { "type", "body" }
            }
        },
        new Tool
        {
            Name = "list_takeoff",
            Description = "Return a JSON view of the drawing takeoff: whether a PDF and scale are "
                        + "set, the measured masonry walls, the openings (with sizes, committed "
                        + "state, and a suggested wall/kind), and the BOQ masonry wall marks. "
                        + "Read-only.",
            InputSchema = new() { Properties = new Dictionary<string, JsonElement>() }
        },
        new Tool
        {
            Name = "commit_opening",
            Description = "Commit a takeoff opening to the BOQ: create the door/window schedule "
                        + "entry (or deduct-only) plus the masonry wall deduction, on the wall you "
                        + "choose. Get opening ids and candidate walls from list_takeoff, and pick "
                        + "the wall from the masonry walls on that opening's level. The user must "
                        + "approve in a dialog before it is applied — do not ask in chat first.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["opening_id"] = Schema(new { type = "string", description = "Takeoff opening id (from list_takeoff)." }),
                    ["kind"] = Schema(new { type = "string", @enum = new[] { "door", "window", "deduct", "auto" }, description = "Schedule kind; auto = infer from proportions." }),
                    ["wall_mark"] = Schema(new { type = "string", description = "Masonry wall mark to deduct from; omit to use the nearest-wall default." })
                },
                Required = new List<string> { "opening_id" }
            }
        },
        new Tool
        {
            Name = "read_drawing",
            Description = "Visually analyze the currently loaded takeoff PDF drawing and return a "
                        + "description (rooms, walls, columns, doors/windows, visible dimensions "
                        + "and notes). Use it to understand a drawing before helping with takeoff. "
                        + "Read-only; requires a drawing to be loaded on the takeoff page.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["question"] = Schema(new { type = "string", description = "Optional specific question about the drawing." })
                }
            }
        },
        new Tool
        {
            Name = "list_link_rules",
            Description = "Return the linked-item derivation rules and a preview of what they "
                        + "currently derive. Link rules compute one trade's quantity from another "
                        + "(e.g. plaster = 2 × masonry face area, painting = 1 × plaster area, "
                        + "skirting = flooring perimeter). Includes each rule's source/target/basis/"
                        + "factor, the derived totals per target trade from the current take-off, and "
                        + "the derived cost priced against the active rate book (with any unpriced "
                        + "codes). Read-only.",
            InputSchema = new() { Properties = new Dictionary<string, JsonElement>() }
        },
        new Tool
        {
            Name = "add_link_rule",
            Description = "Add a linked-item derivation rule: target quantity = factor × the chosen "
                        + "basis measure of the source trade. Source and target are trade keys (see "
                        + "list_link_rules). Basis is Area, Volume, Length, Perimeter or Count. Rules "
                        + "chain (a target can be another rule's source). The user must approve the "
                        + "change in a dialog before it is added — do not ask in chat first.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["source"] = Schema(new { type = "string", @enum = LinkTradeKeys, description = "Source (producer) trade key." }),
                    ["target"] = Schema(new { type = "string", @enum = LinkTradeKeys, description = "Target (consumer) trade key." }),
                    ["basis"] = Schema(new { type = "string", @enum = LinkBases, description = "Which source measure drives the quantity." }),
                    ["factor"] = Schema(new { type = "number", description = "Multiplier applied to the basis measure (default 1)." }),
                    ["name"] = Schema(new { type = "string", description = "Optional rule name; a source→target label is used if omitted." }),
                    ["target_unit"] = Schema(new { type = "string", description = "Optional unit for the derived quantity; defaults to the target trade's unit." }),
                    ["rate_code"] = Schema(new { type = "string", description = "Optional rate code the derived lines price on; defaults to the target trade's canonical code." }),
                    ["per_item"] = Schema(new { type = "boolean", description = "True (default): one derived line per source item, keeping mark/level. False: a single aggregate line." })
                },
                Required = new List<string> { "source", "target", "basis" }
            }
        },
        new Tool
        {
            Name = "apply_links",
            Description = "Materialise the enabled link rules' derived quantities into the target "
                        + "take-off sheets as tagged rows, so they flow into the BOQ and estimate. "
                        + "Idempotent — previously applied link rows are replaced. Preview first with "
                        + "list_link_rules. The user must approve in a dialog before it is applied — "
                        + "do not ask in chat first.",
            InputSchema = new() { Properties = new Dictionary<string, JsonElement>() }
        },
        new Tool
        {
            Name = "list_levels",
            Description = "Return the project's storeys/levels: each level's id (e.g. Lvl0), name, "
                        + "storey height, slab thickness, beam depth and the derived column clear "
                        + "height, all in mm. Use the ids as the `level` field when adding rows. "
                        + "Read-only.",
            InputSchema = new() { Properties = new Dictionary<string, JsonElement>() }
        },
        new Tool
        {
            Name = "add_level",
            Description = "Add a storey/level to the project (e.g. an upper floor). Levels are ids "
                        + "Lvl0, Lvl1, … in order; the new level is appended and ids are renumbered. "
                        + "The user must approve in a dialog before it is added — do not ask in chat "
                        + "first.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["name"] = Schema(new { type = "string", description = "Level name, e.g. \"Second floor\". Defaults to \"Level N\"." }),
                    ["height_mm"] = Schema(new { type = "number", description = "Storey (floor-to-floor) height in mm. Default 3000." }),
                    ["slab_thickness_mm"] = Schema(new { type = "number", description = "Slab thickness in mm. Default 150." }),
                    ["beam_depth_mm"] = Schema(new { type = "number", description = "Beam depth in mm. Default 450." })
                }
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

    private async Task<string> CreateCorrespondenceAsync(IReadOnlyDictionary<string, JsonElement> input)
    {
        string code = GetString(input, "type").Trim().ToUpperInvariant();
        if (!DocCodes.Contains(code))
            return $"Unknown correspondence type \"{code}\". Supported: {string.Join(", ", DocCodes)}.";

        string subject = GetString(input, "subject").Trim();
        string body = GetString(input, "body");
        if (string.IsNullOrWhiteSpace(body))
            return "Provide the drafted body text in `body`.";

        var s = ProjectStore.Current;
        var party = s.Parties.ActiveParty;
        var doc = new OfficeDocument
        {
            TypeCode = code,
            IssuedByRole = party.Role,
            IssueDate = DateTime.Today,
            ToName = GetString(input, "to_name").Trim(),
            ToAddress = GetString(input, "to_address").Trim(),
            Subject = subject,
            Body = body
        };

        string preview;
        try { preview = s.Office.PreviewNumber(doc, party.Company); }
        catch { preview = "assigned on finalize"; }

        string display = DocTypeInfo.DisplayFor(code);
        string details =
            $"{display} — added as a draft (number {preview}, assigned when you finalize it).\n"
          + (DocTypeInfo.HasRecipient(code) && !string.IsNullOrWhiteSpace(doc.ToName) ? $"To: {doc.ToName}\n" : "")
          + (string.IsNullOrWhiteSpace(subject) ? "" : $"Subject: {subject}\n")
          + "\n" + Preview(body, 400);

        if (!await _confirm.ConfirmAsync($"Add {display} draft?", details))
            return $"Cancelled — no {display.ToLowerInvariant()} added.";

        s.Office.Documents.Add(doc);
        ProjectStore.Current.Notify();
        AppNotify.Success($"{display} draft added", string.IsNullOrWhiteSpace(subject) ? display : subject);
        _bus.Navigate("correspondence");
        return $"Added a {display.ToLowerInvariant()} draft"
             + (string.IsNullOrWhiteSpace(subject) ? "" : $" \"{subject}\"")
             + ". It is unnumbered until you finalize it on the Correspondence page.";
    }

    // ——— Takeoff tools (3b / 3c) ———

    private static string ListTakeoff()
    {
        var t = ProjectStore.Current.Takeoff;
        var walls = new JsonArray();
        var openings = new JsonArray();
        int other = 0;

        foreach (var it in t.Items)
        {
            bool isOpening = it.Tool.Equals("Opening", StringComparison.OrdinalIgnoreCase)
                             || it.Fields.ContainsKey("opening_l");
            if (isOpening)
            {
                openings.Add(new JsonObject
                {
                    ["id"] = it.Id,
                    ["level"] = it.Level,
                    ["opening_l"] = it.Fields.GetValueOrDefault("opening_l", ""),
                    ["opening_h"] = it.Fields.GetValueOrDefault("opening_h", ""),
                    ["nos"] = it.Fields.GetValueOrDefault("opening_nos", "1"),
                    ["committed"] = it.Committed,
                    ["suggested_wall"] = OpeningScheduleLinker.SuggestWallMark(ProjectStore.Current, it),
                    ["suggested_kind"] = OpeningScheduleLinker.SuggestKind(it).ToString()
                });
            }
            else if (it.Category.Equals("masonry", StringComparison.OrdinalIgnoreCase))
            {
                walls.Add(new JsonObject { ["mark"] = it.Mark, ["level"] = it.Level, ["length_mm"] = it.LengthMm });
            }
            else other++;
        }

        var boqWalls = new JsonArray();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in ProjectStore.Current.MasonryWalls)
            if (w.TryGetValue("mark", out var m) && !string.IsNullOrWhiteSpace(m) && seen.Add(m.Trim()))
                boqWalls.Add(m.Trim());

        return new JsonObject
        {
            ["pdf_loaded"] = t.PdfPath is not null,
            ["scale_set"] = t.MmPerPx > 0,
            ["mm_per_px"] = t.MmPerPx,
            ["measured_masonry_walls"] = walls,
            ["openings"] = openings,
            ["other_items"] = other,
            ["boq_masonry_wall_marks"] = boqWalls
        }.ToJsonString();
    }

    private async Task<string> CommitOpeningAsync(IReadOnlyDictionary<string, JsonElement> input)
    {
        string id = GetString(input, "opening_id").Trim();
        if (string.IsNullOrWhiteSpace(id)) return "Provide opening_id (see list_takeoff).";

        var store = ProjectStore.Current;
        var item = store.Takeoff.Items.FirstOrDefault(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (item is null) return $"No takeoff item with id {id}.";
        if (!item.Fields.ContainsKey("opening_l")) return "That takeoff item is not an opening.";
        if (item.Committed) return "That opening is already committed.";

        var kind = ParseKind(GetString(input, "kind"), item);
        string wallMark = GetString(input, "wall_mark").Trim();
        if (string.IsNullOrWhiteSpace(wallMark))
            wallMark = OpeningScheduleLinker.SuggestWallMark(store, item);

        string dims = $"{item.Fields.GetValueOrDefault("opening_l", "?")}×{item.Fields.GetValueOrDefault("opening_h", "?")} mm";
        string details = $"Opening {dims} on level {item.Level}\n→ {KindLabel(kind)} + masonry deduct on wall {wallMark}";
        if (!await _confirm.ConfirmAsync("Commit opening to BOQ?", details))
            return "Cancelled — opening not committed.";

        try
        {
            var res = OpeningScheduleLinker.Commit(store, item, kind, wallMark);
            store.Notify();
            AppNotify.Success("Opening committed", res.Message);
            _bus.Navigate("takeoff");
            return res.Message;
        }
        catch (Exception ex)
        {
            return $"Could not commit opening: {ex.Message}";
        }
    }

    private async Task<string> ReadDrawingAsync(IReadOnlyDictionary<string, JsonElement> input)
    {
        if (_client is null) return "The drawing reader is unavailable (no API key).";

        var path = ProjectStore.Current.Takeoff.PdfPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return "No takeoff drawing is loaded. Load a PDF on the Drawing takeoff page first.";

        byte[] bytes;
        try { bytes = await File.ReadAllBytesAsync(path); }
        catch (Exception ex) { return $"Could not read the drawing file: {ex.Message}"; }
        if (bytes.LongLength > MaxPdfBytes)
            return "The drawing PDF is too large to analyze (over 20 MB).";

        string b64 = await Task.Run(() => Convert.ToBase64String(bytes));
        string question = GetString(input, "question").Trim();
        string prompt = string.IsNullOrWhiteSpace(question)
            ? "This is a construction drawing. Describe what it shows: rooms/spaces, walls, columns, "
              + "doors and windows with approximate sizes if dimensioned, and any visible dimensions, "
              + "grid references, levels, or notes. Be concise and organized."
            : question;

        try
        {
            var resp = await _client.Messages.Create(new MessageCreateParams
            {
                Model = "claude-opus-5",
                MaxTokens = 8192,
                Messages =
                [
                    new MessageParam
                    {
                        Role = Role.User,
                        Content = new List<ContentBlockParam>
                        {
                            new DocumentBlockParam { Source = new Base64PdfSource { Data = b64 } },
                            new TextBlockParam { Text = prompt }
                        }
                    }
                ]
            });

            var parts = new List<string>();
            foreach (var block in resp.Content)
                if (block.TryPickText(out TextBlock? text))
                    parts.Add(text.Text);
            string joined = string.Join("\n", parts).Trim();
            return string.IsNullOrEmpty(joined) ? "The drawing reader returned no text." : joined;
        }
        catch (Exception ex)
        {
            return $"Could not analyze the drawing: {ex.Message}";
        }
    }

    // ——— Linked-item derivation tools ———

    private static string ListLinkRules()
    {
        var book = ProjectStore.Current.LinkRules;

        var rules = new JsonArray();
        foreach (var r in book.Rules)
            rules.Add(new JsonObject
            {
                ["id"] = r.Id,
                ["name"] = string.IsNullOrWhiteSpace(r.Name)
                    ? $"{LinkTradeRegistry.Display(r.SourceTrade)} → {LinkTradeRegistry.Display(r.TargetTrade)}"
                    : r.Name,
                ["enabled"] = r.Enabled,
                ["source"] = r.SourceTrade,
                ["target"] = r.TargetTrade,
                ["basis"] = LinkBasisInfo.Label(r.Basis),
                ["factor"] = r.Factor,
                ["target_unit"] = r.TargetUnit,
                ["rate_code"] = string.IsNullOrWhiteSpace(r.RateCodeOverride) ? null : r.RateCodeOverride,
                ["per_item"] = r.PerItem
            });

        var items = DerivationEngine.Preview(ProjectStore.Current, book);

        RateBookStore.Current.EnsureLoaded();
        var version = RateBookStore.Current.ActiveOrFirst();
        var (costTotal, missing) = DerivationEngine.Price(items, version);

        var totals = new JsonArray();
        foreach (var t in DerivationEngine.Totals(items))
            totals.Add(new JsonObject
            {
                ["trade"] = t.Trade,
                ["qty"] = Math.Round(t.Qty, 3),
                ["unit"] = t.Unit,
                ["lines"] = t.Lines
            });

        var missingArr = new JsonArray();
        foreach (var m in missing) missingArr.Add(m);

        return new JsonObject
        {
            ["rules"] = rules,
            ["derived_totals"] = totals,
            ["rate_book"] = version?.Name,
            ["derived_cost"] = version is null ? null : (JsonNode)Math.Round(costTotal, 2),
            ["unpriced_codes"] = missingArr,
            ["trade_keys"] = new JsonArray(LinkTradeKeys.Select(k => (JsonNode)k).ToArray())
        }.ToJsonString();
    }

    private async Task<string> AddLinkRuleAsync(IReadOnlyDictionary<string, JsonElement> input)
    {
        string source = GetString(input, "source").Trim();
        string target = GetString(input, "target").Trim();
        var srcDef = LinkTradeRegistry.ByKey(source);
        var tgtDef = LinkTradeRegistry.ByKey(target);
        if (srcDef is null) return $"Unknown source trade \"{source}\". Supported: {string.Join(", ", LinkTradeKeys)}.";
        if (tgtDef is null) return $"Unknown target trade \"{target}\". Supported: {string.Join(", ", LinkTradeKeys)}.";

        var basis = LinkBasisInfo.Parse(GetString(input, "basis"));

        double factor = 1.0;
        if (input.TryGetValue("factor", out var fEl))
        {
            if (fEl.ValueKind == JsonValueKind.Number) factor = fEl.GetDouble();
            else if (fEl.ValueKind == JsonValueKind.String
                     && double.TryParse(fEl.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var fd))
                factor = fd;
        }
        if (factor <= 0) return $"Factor must be greater than zero (got {factor.ToString("0.###", CultureInfo.InvariantCulture)}).";

        bool perItem = true;
        if (input.TryGetValue("per_item", out var pEl))
        {
            if (pEl.ValueKind == JsonValueKind.False) perItem = false;
            else if (pEl.ValueKind == JsonValueKind.String && TryParseBool(pEl.GetString() ?? "", out var pb)) perItem = pb;
        }

        string unit = GetString(input, "target_unit").Trim();
        if (string.IsNullOrWhiteSpace(unit)) unit = tgtDef.Unit;

        string name = GetString(input, "name").Trim();
        if (string.IsNullOrWhiteSpace(name)) name = $"{srcDef.Display} → {tgtDef.Display}";

        string rateCode = GetString(input, "rate_code").Trim();

        var rule = new LinkRule
        {
            Name = name,
            SourceTrade = srcDef.Key,
            TargetTrade = tgtDef.Key,
            Basis = basis,
            Factor = factor,
            TargetUnit = unit,
            RateCodeOverride = rateCode,
            PerItem = perItem,
            Enabled = true
        };

        string equation = $"{tgtDef.Display} = {factor.ToString("0.###", CultureInfo.InvariantCulture)} × "
                        + $"{srcDef.Display} {LinkBasisInfo.Label(basis).ToLowerInvariant()} ({unit})";
        string codeLine = string.IsNullOrWhiteSpace(rateCode) ? "" : $"\nprices on rate code {rateCode}";
        string details = $"{name}\n{equation}\n{(perItem ? "one line per source item" : "single aggregate line")}{codeLine}";
        if (!await _confirm.ConfirmAsync("Add link rule?", details))
            return "Cancelled — no link rule added.";

        ProjectStore.Current.LinkRules.Rules.Add(rule);
        ProjectStore.Current.Notify();
        AppNotify.Success("Link rule added", equation);
        _bus.Navigate("links");
        return $"Added link rule \"{name}\": {equation}. Preview it with list_link_rules, "
             + "then apply_links to write the derived quantities into the take-off.";
    }

    private async Task<string> ApplyLinksAsync(IReadOnlyDictionary<string, JsonElement> input)
    {
        var store = ProjectStore.Current;
        var book = store.LinkRules;

        var items = DerivationEngine.Preview(store, book);
        var totals = DerivationEngine.Totals(items);
        if (totals.Count == 0)
            return "No linked quantities to apply — add take-off for the source trades (masonry, "
                 + "flooring …) and enable rules, then try again.";

        RateBookStore.Current.EnsureLoaded();
        var version = RateBookStore.Current.ActiveOrFirst();
        var (costTotal, missing) = DerivationEngine.Price(items, version);

        string list = string.Join("\n", totals.Select(t =>
            $"  • {t.Trade}  {t.Qty.ToString("0.##", CultureInfo.InvariantCulture)} {t.Unit}  ({t.Lines} line(s))"));
        string costLine = version is null
            ? ""
            : $"\n\nDerived cost ≈ ₹{costTotal.ToString("N2", CultureInfo.InvariantCulture)} at {version.Name} rates"
              + (missing.Count > 0 ? $" ({missing.Count} code(s) unpriced)" : "");
        string details = "These linked quantities will be written into the take-off sheets so they "
                       + "flow into the BOQ and estimate. Previously applied link rows are replaced:\n\n"
                       + list + costLine;
        if (!await _confirm.ConfirmAsync("Apply linked items?", details))
            return "Cancelled — no linked quantities applied.";

        int n = DerivationEngine.Apply(store, book);
        AppNotify.Success("Linked items applied", $"{n} row(s) written to take-off sheets");
        _bus.Navigate("links");
        string costMsg = version is null ? "" : $" Derived cost ≈ ₹{costTotal.ToString("N2", CultureInfo.InvariantCulture)} at {version.Name} rates.";
        return $"Applied {n} linked row(s) to the take-off sheets — they now flow into Quantities "
             + $"and the Estimate.{costMsg} They can be undone from the Item links page (Clear applied).";
    }

    // ——— Level tools ———

    private static string ListLevels()
    {
        var levels = new JsonArray();
        foreach (var l in ProjectStore.Current.Levels)
            levels.Add(new JsonObject
            {
                ["id"] = l.Id,
                ["name"] = l.Name,
                ["height_mm"] = l.HeightMm,
                ["slab_thickness_mm"] = l.SlabThicknessMm,
                ["beam_depth_mm"] = l.BeamDepthMm,
                ["column_clear_height_mm"] = l.ColumnHeightMm
            });
        return new JsonObject { ["levels"] = levels }.ToJsonString();
    }

    private async Task<string> AddLevelAsync(IReadOnlyDictionary<string, JsonElement> input)
    {
        var store = ProjectStore.Current;
        int n = store.Levels.Count;

        string name = GetString(input, "name").Trim();
        if (string.IsNullOrWhiteSpace(name)) name = n == 0 ? "Plinth" : $"Level {n}";

        double height = GetNumber(input, "height_mm", 3000);
        double slab = GetNumber(input, "slab_thickness_mm", 150);
        double beam = GetNumber(input, "beam_depth_mm", 450);
        if (height <= 0) return "Storey height must be greater than zero.";
        if (slab < 0 || beam < 0) return "Slab thickness and beam depth cannot be negative.";

        double clear = Math.Max(0, height - slab - beam);
        string details = $"{name} (Lvl{n})\n"
                       + $"height {height:0} mm · slab {slab:0} mm · beam {beam:0} mm\n"
                       + $"column clear height {clear:0} mm";
        if (!await _confirm.ConfirmAsync("Add level?", details))
            return "Cancelled — no level added.";

        var level = new LevelDef
        {
            Id = "Lvl" + n,
            Name = name,
            HeightMm = height,
            SlabThicknessMm = slab,
            BeamDepthMm = beam
        };
        store.Levels.Add(level);
        store.RenumberLevels();
        store.Notify();
        AppNotify.Success("Level added", $"{level.Id} · {level.Name}");
        _bus.Navigate("levels");
        return $"Added level {level.Id} \"{level.Name}\" (height {height:0} mm, column clear "
             + $"height {clear:0} mm). The project now has {store.Levels.Count} level(s). "
             + $"Use \"{level.Id}\" as the level field when adding rows to this storey.";
    }

    private static OpeningScheduleLinker.ScheduleKind ParseKind(string k, TakeoffItem item) =>
        k.Trim().ToLowerInvariant() switch
        {
            "door" => OpeningScheduleLinker.ScheduleKind.Door,
            "window" => OpeningScheduleLinker.ScheduleKind.Window,
            "deduct" or "deduct_only" or "deductonly" => OpeningScheduleLinker.ScheduleKind.DeductOnly,
            _ => OpeningScheduleLinker.SuggestKind(item)
        };

    private static string KindLabel(OpeningScheduleLinker.ScheduleKind k) => k switch
    {
        OpeningScheduleLinker.ScheduleKind.Door => "Door schedule",
        OpeningScheduleLinker.ScheduleKind.Window => "Window schedule",
        _ => "Deduct only"
    };

    // ——— Helpers ———

    private static string Preview(string text, int max) =>
        text.Length <= max ? text : text[..max].TrimEnd() + " …";

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

    /// <summary>Read a numeric input, accepting a JSON number or a numeric string; else the default.</summary>
    private static double GetNumber(IReadOnlyDictionary<string, JsonElement> input, string key, double def)
    {
        if (!input.TryGetValue(key, out var v)) return def;
        if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
        if (v.ValueKind == JsonValueKind.String
            && double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return d;
        return def;
    }
}
