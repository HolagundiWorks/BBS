// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using System.Text.RegularExpressions;

namespace BBSApp.Services;

/// <summary>One column of an ERD table.</summary>
public sealed class ErdColumn
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool IsPk { get; set; }
    public bool IsFk => RefTable is not null;
    public string? RefTable { get; set; }
    public string? RefColumn { get; set; }
}

/// <summary>One ERD entity (table) with its columns and canvas position.</summary>
public sealed class ErdTable
{
    public string Name { get; set; } = "";
    public List<ErdColumn> Columns { get; } = new();
    public double X { get; set; } = double.NaN;
    public double Y { get; set; } = double.NaN;
}

/// <summary>A foreign-key relationship: <c>From.FromColumn → To.ToColumn</c> (child → parent).</summary>
public sealed class ErdRelation
{
    public string FromTable { get; set; } = "";
    public string FromColumn { get; set; } = "";
    public string ToTable { get; set; } = "";
    public string ToColumn { get; set; } = "";
}

public sealed class ErdSchema
{
    public List<ErdTable> Tables { get; } = new();
    public List<ErdRelation> Relations { get; } = new();

    public ErdTable? Find(string name) =>
        Tables.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Builds the in-app ERD model by parsing the DBML that <see cref="SchemaExport"/> generates, so the
/// diagram, the DBML export and the SQL export all track one source of truth (the live registries).
/// A focused DBML reader: Table blocks, columns (name / type / [pk] / [ref: &gt; table.col]),
/// skipping Note '''…''' blocks, indexes and the Project header.
/// </summary>
public static class SchemaModel
{
    public static ErdSchema FromExport() => Parse(SchemaExport.BuildDbml());

    /// <summary>Entities in the item→sub-item (derivation) mapping: trade → link_rule → derived_item.</summary>
    public static readonly string[] DerivationTables = { "trade", "link_rule", "derived_item" };

    /// <summary>Entities in the item→material (composition) mapping: trade/mix_design → material.</summary>
    public static readonly string[] CompositionTables =
        { "trade", "trade_material", "material", "mix_design", "mix_component" };

    /// <summary>Drop every table not in <paramref name="keep"/> and any relation touching a dropped table.</summary>
    public static void RetainTables(ErdSchema schema, IEnumerable<string> keep)
    {
        var set = new HashSet<string>(keep, StringComparer.OrdinalIgnoreCase);
        schema.Tables.RemoveAll(t => !set.Contains(t.Name));
        schema.Relations.RemoveAll(r => !set.Contains(r.FromTable) || !set.Contains(r.ToTable));
    }

    private static readonly Regex TableRx = new(@"^Table\s+(?<n>\w+)\s*\{", RegexOptions.Compiled);
    private static readonly Regex ProjectRx = new(@"^Project\s+\w+\s*\{", RegexOptions.Compiled);
    private static readonly Regex ColRx =
        new(@"^(?<name>[A-Za-z_]\w*)\s+(?<type>[A-Za-z_]\w*)(?:\s+\[(?<attrs>.*)\])?\s*$", RegexOptions.Compiled);
    private static readonly Regex RefRx =
        new(@"ref:\s*[<>\-]+\s*(?<t>\w+)\.(?<c>\w+)", RegexOptions.Compiled);

    public static ErdSchema Parse(string dbml)
    {
        var schema = new ErdSchema();
        ErdTable? current = null;
        bool inNote = false, inSkip = false, inIndexes = false;

        foreach (var raw in dbml.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//")) continue;

            // Multi-line Note '''…''' block.
            if (inNote)
            {
                if (line == "'''") inNote = false;
                continue;
            }
            // indexes { … } block.
            if (inIndexes)
            {
                if (line == "}") inIndexes = false;
                continue;
            }
            // Project { … } header — skip wholesale.
            if (inSkip)
            {
                if (line == "}") inSkip = false;
                continue;
            }
            if (ProjectRx.IsMatch(line)) { inSkip = !line.EndsWith("}"); continue; }

            if (current is null)
            {
                var tm = TableRx.Match(line);
                if (tm.Success)
                {
                    current = new ErdTable { Name = tm.Groups["n"].Value };
                    schema.Tables.Add(current);
                }
                continue;
            }

            // Inside a table.
            if (line == "}") { current = null; continue; }
            if (line.StartsWith("Note:"))
            {
                if (line.Contains("'''")) inNote = true; // opening of a block note
                continue;                                 // single-line notes ignored too
            }
            if (line.StartsWith("indexes")) { inIndexes = !line.EndsWith("}"); continue; }

            var cm = ColRx.Match(line);
            if (!cm.Success) continue;

            var col = new ErdColumn { Name = cm.Groups["name"].Value, Type = cm.Groups["type"].Value };
            string attrs = cm.Groups["attrs"].Success ? cm.Groups["attrs"].Value : "";
            if (Regex.IsMatch(attrs, @"\bpk\b")) col.IsPk = true;
            var rm = RefRx.Match(attrs);
            if (rm.Success)
            {
                col.RefTable = rm.Groups["t"].Value;
                col.RefColumn = rm.Groups["c"].Value;
            }
            current.Columns.Add(col);
        }

        foreach (var t in schema.Tables)
            foreach (var c in t.Columns)
                if (c.RefTable is not null)
                    schema.Relations.Add(new ErdRelation
                    {
                        FromTable = t.Name,
                        FromColumn = c.Name,
                        ToTable = c.RefTable,
                        ToColumn = c.RefColumn ?? "id"
                    });

        return schema;
    }
}
