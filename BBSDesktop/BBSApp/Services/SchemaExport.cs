// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using System.Globalization;
using System.Text;

namespace BBSApp.Services;

/// <summary>
/// Generates the estimation <b>data model</b> for the two ways AQC-Core relates estimation items,
/// as a DBML schema (for <c>dbdiagram.io</c>) and an equivalent SQL script:
/// <list type="bullet">
///   <item><b>Item → material</b> composition — a work item decomposes into materials, either by a
///   ratio <i>recipe</i> (concrete grade M25 → cement + fine aggregate + coarse aggregate; cement
///   mortar 1:6 → cement + fine aggregate) or as a direct material (masonry → bricks, RCC → steel).</item>
///   <item><b>Item → item</b> derivation — one item's quantity follows another (brick wall → plaster
///   → paint; flooring → skirting), i.e. the <see cref="LinkRule"/> model.</item>
/// </list>
/// Reference content (items, materials, mix ratios, sample links) is pulled from the live registries
/// (<see cref="LinkTradeRegistry"/>, <see cref="MaterialsCalculator.MixFor"/>,
/// <see cref="LinkRuleBook.Defaults"/>) so the emitted model tracks the code instead of drifting.
/// </summary>
public static class SchemaExport
{
    // ── Material master ───────────────────────────────────────────────────────
    private sealed record Material(string Key, string Name, string Unit, string Category);

    private static readonly Material[] Materials =
    {
        new("CEMENT",     "Cement (OPC / PPC)",       "bags (50kg)", "Binder"),
        new("FINE_AGG",   "Fine aggregate (sand)",    "m³",          "Aggregate"),
        new("COARSE_AGG", "Coarse aggregate",         "m³",          "Aggregate"),
        new("WATER",      "Water",                    "litre",       "Binder"),
        new("BRICK",      "Bricks (modular)",         "nos",         "Unit"),
        new("ACC_BLOCK",  "AAC / ACC blocks",         "nos",         "Unit"),
        new("CEM_BLOCK",  "Cement / concrete blocks", "nos",         "Unit"),
        new("STEEL",      "Reinforcement steel",      "kg",          "Steel"),
        new("SIZE_STONE", "Size stone",               "m³",          "Stone"),
    };

    // ── Ratio recipes: a mix that decomposes into materials by parts ───────────
    // Concrete grades come from MaterialsCalculator.MixFor so the ratios track the code.
    private static readonly string[] ConcreteGrades = { "M15", "M20", "M25", "M30", "M35", "M40" };

    private sealed record MortarMix(string Key, string Item, string Name, double Cement, double FineAgg, double CoarseAgg, string Note);

    // Cement mortars (cement:sand) and PCC (cement:sand:aggregate). Mixes are user inputs on the
    // sheets; these are the representative defaults the calculators assume.
    private static readonly MortarMix[] MortarMixes =
    {
        new("CM_1_3",   "Plaster", "Cement mortar 1:3", 1, 3, 0, "Rich mortar — plaster / DPC"),
        new("CM_1_4",   "Masonry", "Cement mortar 1:4", 1, 4, 0, "Block / brick masonry"),
        new("CM_1_6",   "Masonry", "Cement mortar 1:6", 1, 6, 0, "Brick masonry / plaster (default)"),
        new("PCC_1_4_8","PCC",     "PCC 1:4:8",         1, 4, 8, "Plain cement concrete bed"),
    };

    // ── Direct item → material edges (not a ratio recipe) ──────────────────────
    private sealed record ItemMaterial(string Item, string Material, string Note);

    private static readonly ItemMaterial[] ItemMaterials =
    {
        new("Masonry",  "BRICK",      "Brick masonry — unit count incl. wastage"),
        new("Masonry",  "ACC_BLOCK",  "AAC/ACC block masonry — alternative unit"),
        new("Masonry",  "CEM_BLOCK",  "Concrete block masonry — alternative unit"),
        new("SSM",      "SIZE_STONE", "Size-stone masonry — stone volume"),
        new("Concrete", "STEEL",      "RCC reinforcement (from the BBS engine)"),
    };

    // ── Composition items (consumers) beyond the LinkTradeRegistry finish trades ─
    private sealed record ExtraItem(string Key, string Name, string Unit, string Category);

    private static readonly ExtraItem[] ExtraItems =
    {
        new("Concrete", "RCC concrete (columns, beams, slabs, footings, walls, stairs)", "m³", "Structure"),
    };

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>Write both <c>aqc-core-derivation.dbml</c> and <c>.sql</c> into <paramref name="dir"/>.
    /// Returns the two file paths.</summary>
    public static (string Dbml, string Sql) WriteFiles(string dir)
    {
        Directory.CreateDirectory(dir);
        string dbmlPath = Path.Combine(dir, "aqc-core-derivation.dbml");
        string sqlPath = Path.Combine(dir, "aqc-core-derivation.sql");
        File.WriteAllText(dbmlPath, BuildDbml());
        File.WriteAllText(sqlPath, BuildSql());
        return (dbmlPath, sqlPath);
    }

    /// <summary>The DBML schema — paste into dbdiagram.io (New Diagram → Import → DBML).</summary>
    public static string BuildDbml()
    {
        var b = new StringBuilder();
        b.AppendLine("// ─────────────────────────────────────────────────────────────────────────────");
        b.AppendLine("// AQC-Core — estimation item relationships (data model)");
        b.AppendLine("//");
        b.AppendLine("// Generated by SchemaExport.BuildDbml() from the live registries — do not hand-edit;");
        b.AppendLine("// regenerate from the app (Item links → Export data model).");
        b.AppendLine("//");
        b.AppendLine("// Two relationship families:");
        b.AppendLine("//   • item → material  (composition / BOM): concrete → cement + fine + coarse agg;");
        b.AppendLine("//                        masonry → bricks + mortar; RCC → steel.");
        b.AppendLine("//   • item → item      (derivation): brick wall → plaster → paint; floor → skirting.");
        b.AppendLine("//");
        b.AppendLine("// Open: dbdiagram.io → New Diagram → Import → paste (DBML).");
        b.AppendLine("// ─────────────────────────────────────────────────────────────────────────────");
        b.AppendLine();
        b.AppendLine("Project aqc_core_derivation {");
        b.AppendLine("  database_type: 'Generic'");
        b.AppendLine("  Note: 'AQC-Core (Human Centric Works) — how estimation items relate: composed of materials (recipe/BOM) and derived from other items (linked-item model).'");
        b.AppendLine("}");
        b.AppendLine();

        // item
        b.AppendLine("// ── Items — nodes shared by both relationship families ────────────────────────");
        b.AppendLine("Table item {");
        b.AppendLine("  key      varchar [pk, note: 'Item key = trade key / element, e.g. \"Masonry\", \"Concrete\"']");
        b.AppendLine("  name     varchar");
        b.AppendLine("  unit     varchar [note: 'm² | m³ | m | nos | kg']");
        b.AppendLine("  category varchar [note: 'Finish | Structure | Masonry | …']");
        b.AppendLine("  Note: '''");
        b.AppendLine("  Estimation items — both the consumers in the composition model and the nodes of the");
        b.AppendLine("  derivation graph. Seeded from LinkTradeRegistry plus RCC concrete:");
        foreach (var it in AllItems())
            b.AppendLine($"    • {it.Key} — {it.Name} ({it.Unit})");
        b.AppendLine("  '''");
        b.AppendLine("}");
        b.AppendLine();

        // material
        b.AppendLine("// ── Materials — leaves of the composition model ───────────────────────────────");
        b.AppendLine("Table material {");
        b.AppendLine("  key      varchar [pk, note: 'e.g. CEMENT, FINE_AGG, COARSE_AGG, BRICK, STEEL']");
        b.AppendLine("  name     varchar");
        b.AppendLine("  unit     varchar");
        b.AppendLine("  category varchar [note: 'Binder | Aggregate | Unit | Steel | Stone']");
        b.AppendLine("  Note: '''");
        b.AppendLine("  Purchasable materials a work item resolves to:");
        foreach (var m in Materials)
            b.AppendLine($"    • {m.Key} — {m.Name} ({m.Unit})");
        b.AppendLine("  '''");
        b.AppendLine("}");
        b.AppendLine();

        // recipe
        b.AppendLine("// ── Composition (item → material) ─────────────────────────────────────────────");
        b.AppendLine("Table recipe {");
        b.AppendLine("  key       varchar [pk, note: 'e.g. CONC_M25, CM_1_6, PCC_1_4_8']");
        b.AppendLine("  item_key  varchar [ref: > item.key, note: 'The item this recipe composes']");
        b.AppendLine("  name      varchar");
        b.AppendLine("  dry_factor double [note: 'Wet→dry volume factor (concrete 1.54)']");
        b.AppendLine("  note      varchar");
        b.AppendLine("  Note: 'A ratio recipe: the item decomposes into materials by parts (see recipe_component). target material qty = dry_factor × item volume × parts / Σparts.'");
        b.AppendLine("}");
        b.AppendLine();
        b.AppendLine("Table recipe_component {");
        b.AppendLine("  id           varchar [pk]");
        b.AppendLine("  recipe_key   varchar [ref: > recipe.key]");
        b.AppendLine("  material_key varchar [ref: > material.key]");
        b.AppendLine("  parts        double  [note: 'Proportion by volume, e.g. cement 1 : sand 1 : aggregate 2']");
        b.AppendLine("  Note: 'One material line of a recipe. e.g. CONC_M25 → CEMENT 1, FINE_AGG 1, COARSE_AGG 2.'");
        b.AppendLine("}");
        b.AppendLine();
        b.AppendLine("Table item_material {");
        b.AppendLine("  id           varchar [pk]");
        b.AppendLine("  item_key     varchar [ref: > item.key]");
        b.AppendLine("  material_key varchar [ref: > material.key]");
        b.AppendLine("  note         varchar");
        b.AppendLine("  Note: 'Direct item→material edge for materials that are not part of a ratio recipe — masonry → bricks, RCC → reinforcement steel.'");
        b.AppendLine("}");
        b.AppendLine();

        // link (item → item)
        b.AppendLine("// ── Derivation (item → item) — the linked-item model ──────────────────────────");
        b.AppendLine("Table link_rule {");
        b.AppendLine("  id           varchar [pk]");
        b.AppendLine("  name         varchar");
        b.AppendLine("  source_item  varchar [ref: > item.key, note: 'Producer item']");
        b.AppendLine("  target_item  varchar [ref: > item.key, note: 'Consumer item']");
        b.AppendLine("  basis        varchar [note: 'Area | Volume | Length | Perimeter | Count']");
        b.AppendLine("  factor       double  [note: 'target_qty = factor × source[basis]']");
        b.AppendLine("  rate_code    varchar [note: 'Optional pinned rate code']");
        b.AppendLine("  rate_override double [note: 'Optional manual unit rate']");
        b.AppendLine("  Note: '''");
        b.AppendLine("  A derivation edge — target quantity follows the source. Standard library:");
        foreach (var r in LinkRuleBook.Defaults())
            b.AppendLine($"    • {LinkTradeRegistry.Display(r.SourceTrade)} → {LinkTradeRegistry.Display(r.TargetTrade)}"
                       + $"  ({LinkBasisInfo.Label(r.Basis)} × {Num(r.Factor)})"
                       + (r.Enabled ? "" : " [off]"));
        b.AppendLine("  '''");
        b.AppendLine("}");
        b.AppendLine();
        b.AppendLine("Table derived_item {");
        b.AppendLine("  id          varchar [pk]");
        b.AppendLine("  rule_id     varchar [ref: > link_rule.id]");
        b.AppendLine("  source_item varchar [ref: > item.key]");
        b.AppendLine("  target_item varchar [ref: > item.key]");
        b.AppendLine("  source_qty  double");
        b.AppendLine("  factor      double");
        b.AppendLine("  target_qty  double  [note: 'factor × source_qty']");
        b.AppendLine("  target_unit varchar");
        b.AppendLine("  Note: 'Engine output (DerivationEngine.Preview) — a computed linked line; logically a view over item × link_rule.'");
        b.AppendLine("}");
        b.AppendLine();

        // Concrete example, as a TableGroup comment for readers.
        b.AppendLine("TableGroup composition {");
        b.AppendLine("  item");
        b.AppendLine("  recipe");
        b.AppendLine("  recipe_component");
        b.AppendLine("  item_material");
        b.AppendLine("  material");
        b.AppendLine("}");
        b.AppendLine();
        b.AppendLine("TableGroup derivation {");
        b.AppendLine("  link_rule");
        b.AppendLine("  derived_item");
        b.AppendLine("}");

        return b.ToString();
    }

    /// <summary>SQL DDL + seed rows equivalent to the DBML model.</summary>
    public static string BuildSql()
    {
        var b = new StringBuilder();
        b.AppendLine("-- AQC-Core — estimation item relationships (data model)");
        b.AppendLine("-- Generated by SchemaExport.BuildSql(); item→material composition + item→item derivation.");
        b.AppendLine();
        b.AppendLine("CREATE TABLE item (");
        b.AppendLine("  key      TEXT PRIMARY KEY,");
        b.AppendLine("  name     TEXT,");
        b.AppendLine("  unit     TEXT,");
        b.AppendLine("  category TEXT");
        b.AppendLine(");");
        b.AppendLine("CREATE TABLE material (");
        b.AppendLine("  key      TEXT PRIMARY KEY,");
        b.AppendLine("  name     TEXT,");
        b.AppendLine("  unit     TEXT,");
        b.AppendLine("  category TEXT");
        b.AppendLine(");");
        b.AppendLine("CREATE TABLE recipe (");
        b.AppendLine("  key        TEXT PRIMARY KEY,");
        b.AppendLine("  item_key   TEXT REFERENCES item(key),");
        b.AppendLine("  name       TEXT,");
        b.AppendLine("  dry_factor REAL,");
        b.AppendLine("  note       TEXT");
        b.AppendLine(");");
        b.AppendLine("CREATE TABLE recipe_component (");
        b.AppendLine("  id           TEXT PRIMARY KEY,");
        b.AppendLine("  recipe_key   TEXT REFERENCES recipe(key),");
        b.AppendLine("  material_key TEXT REFERENCES material(key),");
        b.AppendLine("  parts        REAL");
        b.AppendLine(");");
        b.AppendLine("CREATE TABLE item_material (");
        b.AppendLine("  id           TEXT PRIMARY KEY,");
        b.AppendLine("  item_key     TEXT REFERENCES item(key),");
        b.AppendLine("  material_key TEXT REFERENCES material(key),");
        b.AppendLine("  note         TEXT");
        b.AppendLine(");");
        b.AppendLine("CREATE TABLE link_rule (");
        b.AppendLine("  id            TEXT PRIMARY KEY,");
        b.AppendLine("  name          TEXT,");
        b.AppendLine("  source_item   TEXT REFERENCES item(key),");
        b.AppendLine("  target_item   TEXT REFERENCES item(key),");
        b.AppendLine("  basis         TEXT,");
        b.AppendLine("  factor        REAL,");
        b.AppendLine("  rate_code     TEXT,");
        b.AppendLine("  rate_override REAL");
        b.AppendLine(");");
        b.AppendLine("CREATE TABLE derived_item (");
        b.AppendLine("  id          TEXT PRIMARY KEY,");
        b.AppendLine("  rule_id     TEXT REFERENCES link_rule(id),");
        b.AppendLine("  source_item TEXT REFERENCES item(key),");
        b.AppendLine("  target_item TEXT REFERENCES item(key),");
        b.AppendLine("  source_qty  REAL,");
        b.AppendLine("  factor      REAL,");
        b.AppendLine("  target_qty  REAL,");
        b.AppendLine("  target_unit TEXT");
        b.AppendLine(");");
        b.AppendLine();

        // seed data
        b.AppendLine("-- Items");
        foreach (var it in AllItems())
            b.AppendLine($"INSERT INTO item(key,name,unit,category) VALUES ({Q(it.Key)},{Q(it.Name)},{Q(it.Unit)},{Q(it.Category)});");
        b.AppendLine();
        b.AppendLine("-- Materials");
        foreach (var m in Materials)
            b.AppendLine($"INSERT INTO material(key,name,unit,category) VALUES ({Q(m.Key)},{Q(m.Name)},{Q(m.Unit)},{Q(m.Category)});");
        b.AppendLine();
        b.AppendLine("-- Recipes + components (item → material)");
        int cid = 1;
        foreach (var g in ConcreteGrades)
        {
            var mix = MaterialsCalculator.MixFor(g);
            string rk = "CONC_" + g;
            b.AppendLine($"INSERT INTO recipe(key,item_key,name,dry_factor,note) VALUES ({Q(rk)},'Concrete',{Q(g + " concrete")},{Num(MaterialsCalculator.DryFactor)},{Q(mix.Note)});");
            b.AppendLine($"INSERT INTO recipe_component(id,recipe_key,material_key,parts) VALUES ('rc{cid++}',{Q(rk)},'CEMENT',{Num(mix.Cement)});");
            b.AppendLine($"INSERT INTO recipe_component(id,recipe_key,material_key,parts) VALUES ('rc{cid++}',{Q(rk)},'FINE_AGG',{Num(mix.Sand)});");
            b.AppendLine($"INSERT INTO recipe_component(id,recipe_key,material_key,parts) VALUES ('rc{cid++}',{Q(rk)},'COARSE_AGG',{Num(mix.Aggregate)});");
        }
        foreach (var mm in MortarMixes)
        {
            b.AppendLine($"INSERT INTO recipe(key,item_key,name,dry_factor,note) VALUES ({Q(mm.Key)},{Q(mm.Item)},{Q(mm.Name)},1.33,{Q(mm.Note)});");
            b.AppendLine($"INSERT INTO recipe_component(id,recipe_key,material_key,parts) VALUES ('rc{cid++}',{Q(mm.Key)},'CEMENT',{Num(mm.Cement)});");
            b.AppendLine($"INSERT INTO recipe_component(id,recipe_key,material_key,parts) VALUES ('rc{cid++}',{Q(mm.Key)},'FINE_AGG',{Num(mm.FineAgg)});");
            if (mm.CoarseAgg > 0)
                b.AppendLine($"INSERT INTO recipe_component(id,recipe_key,material_key,parts) VALUES ('rc{cid++}',{Q(mm.Key)},'COARSE_AGG',{Num(mm.CoarseAgg)});");
        }
        b.AppendLine();
        b.AppendLine("-- Direct item → material");
        int im = 1;
        foreach (var e in ItemMaterials)
            b.AppendLine($"INSERT INTO item_material(id,item_key,material_key,note) VALUES ('im{im++}',{Q(e.Item)},{Q(e.Material)},{Q(e.Note)});");
        b.AppendLine();
        b.AppendLine("-- Derivation (item → item) — standard library");
        foreach (var r in LinkRuleBook.Defaults())
            b.AppendLine($"INSERT INTO link_rule(id,name,source_item,target_item,basis,factor,rate_code,rate_override) VALUES "
                       + $"({Q(r.Id)},{Q(r.Name)},{Q(r.SourceTrade)},{Q(r.TargetTrade)},{Q(LinkBasisInfo.Label(r.Basis))},{Num(r.Factor)},{Q(r.RateCodeOverride)},{Num(r.RateOverride)});");

        return b.ToString();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static IEnumerable<(string Key, string Name, string Unit, string Category)> AllItems()
    {
        foreach (var e in ExtraItems) yield return (e.Key, e.Name, e.Unit, e.Category);
        foreach (var t in LinkTradeRegistry.All) yield return (t.Key, t.Display, t.Unit, "Trade");
    }

    private static string Num(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Q(string? s) => "'" + (s ?? "").Replace("'", "''") + "'";
}
