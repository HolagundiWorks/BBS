// Bridge.cpp — RawRow -> engine glue (UTF-8 JSON friendly).
#include "Bridge.h"

#include "../core/Engine.h"
#include "../core/Json.h"
#include "../core/Parse.h"
#include "../core/Project.h"

#include <cstdlib>

namespace bbs {

static std::vector<ExtraFixed> collect_extra_fixed(const RawRow& r) {
    auto out = parse_extra_fixed(to_str(r, "extra_fixed"));
    for (int i = 1; i <= 3; ++i) {
        std::string n = std::to_string(i);
        ExtraFixed e;
        e.dia = to_float(r, "ex" + n + "_dia");
        e.nos = to_int(r, "ex" + n + "_nos");
        e.length_mm = to_float(r, "ex" + n + "_len");
        if (e.dia > 0 && e.nos > 0 && e.length_mm > 0) out.push_back(e);
    }
    return out;
}

static std::vector<ExtraSpan> collect_extra_span(const RawRow& r) {
    auto out = parse_extra_span(to_str(r, "extra_span"));
    for (int i = 1; i <= 3; ++i) {
        std::string n = std::to_string(i);
        ExtraSpan e;
        e.dia = to_float(r, "esp" + n + "_dia");
        e.nos = to_int(r, "esp" + n + "_nos");
        e.frac = to_float(r, "esp" + n + "_frac");
        if (e.dia > 0 && e.nos > 0 && e.frac > 0) out.push_back(e);
    }
    return out;
}

static std::vector<ExtraMesh> collect_extra_mesh(const RawRow& r) {
    auto out = parse_extra_mesh(to_str(r, "extra_mesh"));
    for (int i = 1; i <= 3; ++i) {
        std::string n = std::to_string(i);
        ExtraMesh e;
        e.dia = to_float(r, "em" + n + "_dia");
        e.length_mm = to_float(r, "em" + n + "_len");
        e.spacing = to_float(r, "em" + n + "_sp");
        if (e.dia > 0 && e.length_mm > 0 && e.spacing > 0) out.push_back(e);
    }
    return out;
}

static std::vector<std::string> bar_row(const BarEntry& e) {
    return {e.mark, e.bar_role, format_dia(e.dia), format_num(e.length_mm), std::to_string(e.nos)};
}
static std::vector<std::string> summ_row(const SummaryRow& s) {
    return {s.dia, std::to_string(s.nos), format_num(s.total_length_m), format_num(s.weight_kg)};
}

static JsonValue settings_to_json(const Settings& s) {
    JsonValue o = JsonValue::Object();
    JsonValue dias = JsonValue::Array();
    for (int d : s.diameters) dias.arr.push_back(JsonValue::Num(d));
    o.set("diameters", dias);
    JsonValue hooks = JsonValue::Object();
    for (const auto& kv : s.hook_allowance) hooks.set(std::to_string(kv.first), JsonValue::Num(kv.second));
    o.set("hook_allowance", hooks);
    JsonValue bends = JsonValue::Object();
    for (const auto& kv : s.bend_deduction) bends.set(std::to_string(kv.first), JsonValue::Num(kv.second));
    o.set("bend_deduction", bends);
    JsonValue tau = JsonValue::Object();
    for (const auto& kv : s.tau_bd) tau.set(kv.first, JsonValue::Num(kv.second));
    o.set("tau_bd", tau);
    JsonValue fy = JsonValue::Object();
    for (const auto& kv : s.fy) fy.set(kv.first, JsonValue::Num(kv.second));
    o.set("fy", fy);
    o.set("hysd_bond", JsonValue::Num(s.hysd_bond ? 1 : 0));
    o.set("hysd_bond_factor", JsonValue::Num(s.hysd_bond_factor));
    o.set("min_hook_mm", JsonValue::Num(s.min_hook_mm));
    return o;
}

static void settings_from_json_obj(const JsonValue* o, Settings& s) {
    if (!o || !o->isObject()) return;
    if (const JsonValue* d = o->find("diameters"); d && d->isArray()) {
        s.diameters.clear();
        for (const auto& v : d->arr) s.diameters.push_back(static_cast<int>(v.asNumber()));
    }
    if (const JsonValue* h = o->find("hook_allowance"); h && h->isObject()) {
        s.hook_allowance.clear();
        for (const auto& kv : h->obj) s.hook_allowance[std::atoi(kv.first.c_str())] = kv.second.asNumber();
    }
    if (const JsonValue* b = o->find("bend_deduction"); b && b->isObject()) {
        s.bend_deduction.clear();
        for (const auto& kv : b->obj) s.bend_deduction[std::atoi(kv.first.c_str())] = kv.second.asNumber();
    }
    if (const JsonValue* t = o->find("tau_bd"); t && t->isObject()) {
        s.tau_bd.clear();
        for (const auto& kv : t->obj) s.tau_bd[kv.first] = kv.second.asNumber();
    }
    if (const JsonValue* f = o->find("fy"); f && f->isObject()) {
        s.fy.clear();
        for (const auto& kv : f->obj) s.fy[kv.first] = kv.second.asNumber();
    }
    if (const JsonValue* hb = o->find("hysd_bond")) s.hysd_bond = hb->asNumber() != 0;
    if (const JsonValue* hf = o->find("hysd_bond_factor")) s.hysd_bond_factor = hf->asNumber();
    if (const JsonValue* mh = o->find("min_hook_mm")) s.min_hook_mm = mh->asNumber();
}

static JsonValue rows_to_json(const std::vector<RawRow>& rows) {
    JsonValue arr = JsonValue::Array();
    for (const auto& row : rows) {
        JsonValue o = JsonValue::Object();
        for (const auto& kv : row) o.set(kv.first, JsonValue::Str(kv.second));
        arr.arr.push_back(o);
    }
    return arr;
}

static std::vector<RawRow> rows_from_json_arr(const JsonValue* arr) {
    std::vector<RawRow> rows;
    if (!arr || !arr->isArray()) return rows;
    for (const auto& item : arr->arr) {
        if (!item.isObject()) continue;
        RawRow row;
        for (const auto& kv : item.obj) {
            if (kv.second.type == JsonValue::Type::String) row[kv.first] = kv.second.str;
            else if (kv.second.type == JsonValue::Type::Number) row[kv.first] = format_num(kv.second.num, 6);
        }
        rows.push_back(row);
    }
    return rows;
}

Settings settings_from_json_text(const std::string& json, std::string& err) {
    Settings s;
    if (json.empty()) return s;
    JsonValue root;
    if (!json_parse(json, root, err)) return s;
    settings_from_json_obj(&root, s);
    return s;
}

std::vector<RawRow> rows_from_json_text(const std::string& json, std::string& err) {
    JsonValue root;
    if (!json_parse(json, root, err)) return {};
    return rows_from_json_arr(&root);
}

std::string project_to_json_text(const ProjectData& data) {
    JsonValue root = JsonValue::Object();
    root.set("format", JsonValue::Str("bbsproj"));
    root.set("version", JsonValue::Num(3));
    root.set("name", JsonValue::Str(data.name));
    root.set("settings", settings_to_json(data.settings));
    JsonValue levels = JsonValue::Array();
    for (const auto& lv : data.levels) {
        JsonValue o = JsonValue::Object();
        o.set("id", JsonValue::Str(lv.id));
        o.set("name", JsonValue::Str(lv.name));
        o.set("height_mm", JsonValue::Num(lv.height_mm));
        o.set("slab_thickness_mm", JsonValue::Num(lv.slab_thickness_mm));
        o.set("beam_depth_mm", JsonValue::Num(lv.beam_depth_mm));
        levels.arr.push_back(o);
    }
    root.set("levels", levels);
    root.set("columns", rows_to_json(data.columns));
    root.set("beams", rows_to_json(data.beams));
    root.set("slabs", rows_to_json(data.slabs));
    root.set("footings", rows_to_json(data.footings));
    root.set("walls", rows_to_json(data.walls));
    root.set("stairs", rows_to_json(data.stairs));
    return json_dump(root);
}

bool project_from_json_text(const std::string& json, ProjectData& out, std::string& err) {
    JsonValue root;
    if (!json_parse(json, root, err)) return false;
    if (!root.isObject() || !root.find("settings")) {
        err = "This file doesn't look like a BBS project (.bbsproj).";
        return false;
    }
    if (const JsonValue* n = root.find("name"); n && n->type == JsonValue::Type::String)
        out.name = n->str;
    settings_from_json_obj(root.find("settings"), out.settings);
    out.levels.clear();
    if (const JsonValue* la = root.find("levels"); la && la->isArray()) {
        for (const auto& item : la->arr) {
            if (!item.isObject()) continue;
            ProjectData::Level lv;
            if (const JsonValue* id = item.find("id")) lv.id = id->asString();
            if (const JsonValue* nm = item.find("name")) lv.name = nm->asString();
            if (const JsonValue* h = item.find("height_mm")) lv.height_mm = h->asNumber();
            if (const JsonValue* st = item.find("slab_thickness_mm")) lv.slab_thickness_mm = st->asNumber();
            if (const JsonValue* bd = item.find("beam_depth_mm")) lv.beam_depth_mm = bd->asNumber();
            out.levels.push_back(lv);
        }
    }
    out.columns = rows_from_json_arr(root.find("columns"));
    out.beams = rows_from_json_arr(root.find("beams"));
    out.slabs = rows_from_json_arr(root.find("slabs"));
    out.footings = rows_from_json_arr(root.find("footings"));
    out.walls = rows_from_json_arr(root.find("walls"));
    out.stairs = rows_from_json_arr(root.find("stairs"));
    return true;
}

static BridgeResult from_column(const std::vector<RawRow>& rows, const Settings& s) {
    std::vector<ColumnInput> in;
    for (const auto& r : rows) {
        ColumnInput c;
        c.mark = to_str(r, "mark");
        c.width = to_float(r, "width"); c.depth = to_float(r, "depth");
        c.height = to_float(r, "height"); c.cover = to_float(r, "cover");
        c.stirrup_dia = to_float(r, "stirrup_dia"); c.spacing = to_float(r, "spacing");
        c.hook_angle = to_int(r, "hook_angle", 135);
        c.tie_type = to_str(r, "tie_type", "Auto");
        c.column_type = to_str(r, "column_type", "Rectangular");
        c.bars = parse_bars(to_str(r, "bars"));
        c.concrete_grade = to_str(r, "concrete_grade", "M25");
        c.steel_grade = to_str(r, "steel_grade", "Fe500");
        c.level = to_str(r, "level", "Lvl0");
        c.pedestal_h = to_float(r, "pedestal_h");
        c.pedestal_w = to_float(r, "pedestal_w");
        c.pedestal_d = to_float(r, "pedestal_d");
        c.pedestal_stirrup_dia = to_float(r, "pedestal_stirrup_dia");
        c.pedestal_spacing = to_float(r, "pedestal_spacing");
        c.pedestal_bars = parse_bars(to_str(r, "pedestal_bars"));
        c.extra_fixed = collect_extra_fixed(r);
        c.provide_lap = to_str(r, "provide_lap", "No");
        c.lap_nos = to_int(r, "lap_nos", 0);
        in.push_back(c);
    }
    auto res = generate_column_bbs(in, s);
    BridgeResult out;
    out.bbs.headers = {"Mark", "Role", "Dia (mm)", "Length (mm)", "Nos"};
    out.summary.headers = {"Dia (mm)", "Nos", "Total Length (m)", "Weight (kg)"};
    out.checks.headers = {"Note"};
    for (const auto& e : res.entries) out.bbs.rows.push_back(bar_row(e));
    for (const auto& r : res.summary) out.summary.rows.push_back(summ_row(r));
    for (const auto& n : res.notes) out.checks.rows.push_back({n});
    out.summaryTyped = res.summary;
    return out;
}

static BridgeResult from_beam(const std::vector<RawRow>& rows, const Settings& s) {
    std::vector<BeamInput> in;
    for (const auto& r : rows) {
        BeamInput b;
        b.mark = to_str(r, "mark");
        b.span = to_float(r, "span"); b.width = to_float(r, "width");
        b.depth = to_float(r, "depth"); b.cover = to_float(r, "cover");
        b.concrete_grade = to_str(r, "concrete_grade", "M25");
        b.steel_grade = to_str(r, "steel_grade", "Fe500");
        b.stirrup_dia = to_float(r, "stirrup_dia");
        b.spacing_support = to_float(r, "spacing_support");
        b.spacing_middle = to_float(r, "spacing_middle");
        b.legs = to_int(r, "legs", 2);
        b.hook_angle = to_int(r, "hook_angle", 135);
        b.top_bar_type = to_str(r, "top_bar_type", "At Support");
        b.hanger_bars = parse_bars(to_str(r, "hanger_bars"));
        b.top_bars = parse_bars(to_str(r, "top_bars"));
        b.bottom_bars = parse_bars(to_str(r, "bottom_bars"));
        b.extra_fixed = collect_extra_fixed(r);
        b.extra_span = collect_extra_span(r);
        b.skin_dia = to_float(r, "skin_dia");
        b.skin_spacing = to_float(r, "skin_spacing");
        b.skin_nos = to_int(r, "skin_nos", 0);
        b.end_anchorage = to_str(r, "end_anchorage", "Straight Ld");
        b.provide_lap = to_str(r, "provide_lap", "None");
        b.lap_nos = to_int(r, "lap_nos", 0);
        in.push_back(b);
    }
    auto res = generate_beam_bbs(in, s);
    BridgeResult out;
    out.bbs.headers = {"Mark", "Role", "Dia (mm)", "Length (mm)", "Nos"};
    out.summary.headers = {"Dia (mm)", "Nos", "Total Length (m)", "Weight (kg)"};
    out.checks.headers = {"Note"};
    for (const auto& e : res.entries) out.bbs.rows.push_back(bar_row(e));
    for (const auto& r : res.summary) out.summary.rows.push_back(summ_row(r));
    for (const auto& n : res.notes) out.checks.rows.push_back({n});
    out.summaryTyped = res.summary;
    return out;
}

static BridgeResult from_slab(const std::vector<RawRow>& rows, const Settings& s) {
    std::vector<SlabInput> in;
    for (const auto& r : rows) {
        SlabInput sl;
        sl.mark = to_str(r, "mark");
        sl.span_x = to_float(r, "span_x"); sl.span_y = to_float(r, "span_y");
        sl.thickness = to_float(r, "thickness"); sl.cover = to_float(r, "cover");
        sl.concrete_grade = to_str(r, "concrete_grade", "M25");
        sl.steel_grade = to_str(r, "steel_grade", "Fe415");
        sl.slab_type = to_str(r, "slab_type", "Two-Way");
        sl.dia_x = to_float(r, "dia_x"); sl.spacing_x = to_float(r, "spacing_x");
        sl.dia_y = to_float(r, "dia_y"); sl.spacing_y = to_float(r, "spacing_y");
        sl.crank_count = to_int(r, "crank_count", 0);
        sl.crank_rise = to_float(r, "crank_rise");
        sl.extra_fixed = collect_extra_fixed(r);
        sl.extra_mesh = collect_extra_mesh(r);
        in.push_back(sl);
    }
    auto res = generate_slab_bbs(in, s);
    BridgeResult out;
    out.bbs.headers = {"Mark", "Role", "Dia (mm)", "Length (mm)", "Nos"};
    out.summary.headers = {"Dia (mm)", "Nos", "Total Length (m)", "Weight (kg)"};
    out.checks.headers = {"Mark", "Ast prov-X", "Ast min", "Status-X", "Ast prov-Y", "Status-Y"};
    for (const auto& e : res.entries) out.bbs.rows.push_back(bar_row(e));
    for (const auto& r : res.summary) out.summary.rows.push_back(summ_row(r));
    for (const auto& c : res.checks)
        out.checks.rows.push_back({c.mark, format_num(c.ast_provided_x), format_num(c.ast_min), c.status_x,
                                   format_num(c.ast_provided_y), c.status_y});
    out.summaryTyped = res.summary;
    return out;
}

static BridgeResult from_footing(const std::vector<RawRow>& rows, const Settings& s) {
    std::vector<FootingInput> in;
    for (const auto& r : rows) {
        FootingInput f;
        f.mark = to_str(r, "mark");
        f.footing_type = to_str(r, "footing_type", "Isolated");
        f.length_l = to_float(r, "length_l"); f.width_b = to_float(r, "width_b");
        f.col_dim_l = to_float(r, "col_dim_l"); f.col_dim_b = to_float(r, "col_dim_b");
        f.col2_dim_l = to_float(r, "col2_dim_l"); f.col2_dim_b = to_float(r, "col2_dim_b");
        f.n_steps = to_int(r, "n_steps", 0);
        f.step_height = to_float(r, "step_height");
        f.top_length = to_float(r, "top_length");
        f.top_width = to_float(r, "top_width");
        f.depth = to_float(r, "depth"); f.cover = to_float(r, "cover");
        f.concrete_grade = to_str(r, "concrete_grade", "M25");
        f.steel_grade = to_str(r, "steel_grade", "Fe500");
        f.dia_l = to_float(r, "dia_l"); f.spacing_l = to_float(r, "spacing_l");
        f.dia_b = to_float(r, "dia_b"); f.spacing_b = to_float(r, "spacing_b");
        f.top_dia_l = to_float(r, "top_dia_l"); f.top_spacing_l = to_float(r, "top_spacing_l");
        f.top_dia_b = to_float(r, "top_dia_b"); f.top_spacing_b = to_float(r, "top_spacing_b");
        f.extra_fixed = collect_extra_fixed(r);
        in.push_back(f);
    }
    auto res = generate_footing_bbs(in, s);
    BridgeResult out;
    out.bbs.headers = {"Mark", "Role", "Dia (mm)", "Length (mm)", "Nos"};
    out.summary.headers = {"Dia (mm)", "Nos", "Total Length (m)", "Weight (kg)"};
    out.checks.headers = {"Mark", "Ld req-L", "Avail-L", "Anchorage-L", "Ld req-B", "Avail-B", "Anchorage-B",
                          "Ast prov-L", "Ast min", "Min steel-L", "Min steel-B", "Note"};
    for (const auto& e : res.entries) out.bbs.rows.push_back(bar_row(e));
    for (const auto& r : res.summary) out.summary.rows.push_back(summ_row(r));
    for (const auto& c : res.checks)
        out.checks.rows.push_back({c.mark, format_num(c.ld_required_l), format_num(c.available_l),
                                   c.status_anchorage_l, format_num(c.ld_required_b), format_num(c.available_b),
                                   c.status_anchorage_b, format_num(c.ast_provided_l), format_num(c.ast_min),
                                   c.status_minsteel_l, c.status_minsteel_b, c.note});
    out.summaryTyped = res.summary;
    return out;
}

static BridgeResult from_wall(const std::vector<RawRow>& rows, const Settings& s) {
    std::vector<WallInput> in;
    for (const auto& r : rows) {
        WallInput w;
        w.mark = to_str(r, "mark");
        w.wall_length = to_float(r, "wall_length");
        w.stem_h = to_float(r, "stem_h"); w.stem_t = to_float(r, "stem_t");
        w.heel = to_float(r, "heel"); w.toe = to_float(r, "toe");
        if (to_str(r, "include_toe", "Yes") != "Yes") w.toe = 0;
        w.base_t = to_float(r, "base_t"); w.cover = to_float(r, "cover");
        w.concrete_grade = to_str(r, "concrete_grade", "M25");
        w.steel_grade = to_str(r, "steel_grade", "Fe500");
        w.tension_face = to_str(r, "tension_face", "Front");
        w.stem_v_dia = to_float(r, "stem_v_dia"); w.stem_v_spacing = to_float(r, "stem_v_spacing");
        w.stem_v_back_dia = to_float(r, "stem_v_back_dia");
        w.stem_v_back_spacing = to_float(r, "stem_v_back_spacing");
        w.stem_h_dia = to_float(r, "stem_h_dia"); w.stem_h_spacing = to_float(r, "stem_h_spacing");
        w.base_l_dia = to_float(r, "base_l_dia"); w.base_l_spacing = to_float(r, "base_l_spacing");
        w.base_b_dia = to_float(r, "base_b_dia"); w.base_b_spacing = to_float(r, "base_b_spacing");
        w.extra_fixed = collect_extra_fixed(r);
        w.link_dia = to_float(r, "link_dia"); w.link_spacing = to_float(r, "link_spacing");
        w.link_legs = to_int(r, "link_legs", 2);
        in.push_back(w);
    }
    auto res = generate_wall_bbs(in, s);
    BridgeResult out;
    out.bbs.headers = {"Mark", "Role", "Dia (mm)", "Length (mm)", "Nos"};
    out.summary.headers = {"Dia (mm)", "Nos", "Total Length (m)", "Weight (kg)"};
    out.checks.headers = {"Mark", "Ast stem", "Ast min stem", "Status stem", "Ast base", "Ast min base",
                          "Status base", "Note"};
    for (const auto& e : res.entries) out.bbs.rows.push_back(bar_row(e));
    for (const auto& r : res.summary) out.summary.rows.push_back(summ_row(r));
    for (const auto& c : res.checks)
        out.checks.rows.push_back({c.mark, format_num(c.ast_stem), format_num(c.ast_min_stem), c.status_stem,
                                   format_num(c.ast_base), format_num(c.ast_min_base), c.status_base, c.note});
    out.summaryTyped = res.summary;
    return out;
}

static BridgeResult from_stair(const std::vector<RawRow>& rows, const Settings& s) {
    std::vector<StairInput> in;
    for (const auto& r : rows) {
        StairInput st;
        st.mark = to_str(r, "mark");
        st.n_risers = to_int(r, "n_risers", 12);
        st.n_flights = to_int(r, "n_flights", 1);
        st.going = to_float(r, "going");
        st.riser = to_float(r, "riser");
        st.waist_t = to_float(r, "waist_t");
        st.flight_width = to_float(r, "flight_width");
        st.cover = to_float(r, "cover");
        st.landing_len = to_float(r, "landing_len");
        st.landing_width = to_float(r, "landing_width");
        st.landing_t = to_float(r, "landing_t");
        st.concrete_grade = to_str(r, "concrete_grade", "M25");
        st.steel_grade = to_str(r, "steel_grade", "Fe500");
        st.main_dia = to_float(r, "main_dia");
        st.main_spacing = to_float(r, "main_spacing");
        st.dist_dia = to_float(r, "dist_dia");
        st.dist_spacing = to_float(r, "dist_spacing");
        st.landing_dia = to_float(r, "landing_dia");
        st.landing_spacing = to_float(r, "landing_spacing");
        st.extra_fixed = collect_extra_fixed(r);
        in.push_back(st);
    }
    auto res = generate_stair_bbs(in, s);
    BridgeResult out;
    out.bbs.headers = {"Mark", "Role", "Dia (mm)", "Length (mm)", "Nos"};
    out.summary.headers = {"Dia (mm)", "Nos", "Total Length (m)", "Weight (kg)"};
    out.checks.headers = {"Mark", "Slope (mm)", "Rise (mm)", "Going (mm)", "Ast main", "Ast min",
                          "Status", "Note"};
    for (const auto& e : res.entries) out.bbs.rows.push_back(bar_row(e));
    for (const auto& r : res.summary) out.summary.rows.push_back(summ_row(r));
    for (const auto& c : res.checks)
        out.checks.rows.push_back({c.mark, format_num(c.slope_len), format_num(c.rise_total),
                                   format_num(c.going_total), format_num(c.ast_main),
                                   format_num(c.ast_min), c.status_main, c.note});
    out.summaryTyped = res.summary;
    return out;
}

BridgeResult generate_kind(const std::string& kind, const Settings& s, const std::vector<RawRow>& rows) {
    if (kind == "columns") return from_column(rows, s);
    if (kind == "beams") return from_beam(rows, s);
    if (kind == "slabs") return from_slab(rows, s);
    if (kind == "footings") return from_footing(rows, s);
    if (kind == "walls") return from_wall(rows, s);
    if (kind == "stairs") return from_stair(rows, s);
    BridgeResult err;
    err.error = "Unknown kind: " + kind;
    return err;
}

std::vector<ReportSection> build_report_sections(const ProjectData& p) {
    std::vector<ReportSection> sec;
    std::vector<std::vector<SummaryRow>> allSummaries;

    auto add = [&](const std::string& name, const BridgeResult& gr, const std::string& chkTitle) {
        if (gr.bbs.rows.empty()) return;
        sec.push_back({name + " — bar bending schedule", gr.bbs.headers, gr.bbs.rows, ""});
        sec.push_back({name + " — steel summary", gr.summary.headers, gr.summary.rows, ""});
        if (!gr.checks.rows.empty())
            sec.push_back({chkTitle, gr.checks.headers, gr.checks.rows, ""});
        allSummaries.push_back(gr.summaryTyped);
    };

    add("Columns", generate_kind("columns", p.settings, p.columns), "");
    add("Beams", generate_kind("beams", p.settings, p.beams), "Beams — detailing notes (IS 456)");
    add("Slabs", generate_kind("slabs", p.settings, p.slabs),
        "Slabs — minimum steel check (IS 456 Cl. 26.5.2.1)");
    add("Footings", generate_kind("footings", p.settings, p.footings),
        "Footings — anchorage & min steel (IS 456 Cl. 26.2 / Cl. 34)");
    add("Retaining walls", generate_kind("walls", p.settings, p.walls),
        "Walls — minimum steel check (IS 456 Cl. 26.5.2.1)");
    add("Staircase", generate_kind("stairs", p.settings, p.stairs),
        "Stairs — geometry & min steel (waist)");

    if (!allSummaries.empty()) {
        auto merged = merge_summaries(allSummaries);
        std::vector<std::vector<std::string>> rows;
        for (const auto& r : merged)
            rows.push_back({r.dia, std::to_string(r.nos), format_num(r.total_length_m), format_num(r.weight_kg)});
        sec.insert(sec.begin(),
                   {"Project steel summary (all elements)",
                    {"Dia (mm)", "Nos", "Total Length (m)", "Weight (kg)"}, rows,
                    "Consolidated steel total by bar diameter."});
    }
    return sec;
}

static JsonValue table_to_json(const GenTable& t) {
    JsonValue o = JsonValue::Object();
    JsonValue hdr = JsonValue::Array();
    for (const auto& h : t.headers) hdr.arr.push_back(JsonValue::Str(h));
    o.set("headers", hdr);
    JsonValue rows = JsonValue::Array();
    for (const auto& r : t.rows) {
        JsonValue arr = JsonValue::Array();
        for (const auto& c : r) arr.arr.push_back(JsonValue::Str(c));
        rows.arr.push_back(arr);
    }
    o.set("rows", rows);
    return o;
}

std::string bridge_result_to_json(const BridgeResult& r) {
    JsonValue root = JsonValue::Object();
    if (!r.error.empty()) {
        root.set("ok", JsonValue::Bool(false));
        root.set("error", JsonValue::Str(r.error));
        return json_dump(root, 0);
    }
    root.set("ok", JsonValue::Bool(true));
    root.set("bbs", table_to_json(r.bbs));
    root.set("summary", table_to_json(r.summary));
    root.set("checks", table_to_json(r.checks));
    return json_dump(root, 0);
}

}  // namespace bbs
