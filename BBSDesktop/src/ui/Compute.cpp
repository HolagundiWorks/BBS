// Compute.cpp — RawRow -> engine glue + report assembly.
#include "Compute.h"

#include "../core/Parse.h"
#include "Widgets.h"

namespace ui {

using bbs::to_float;
using bbs::to_int;
using bbs::to_str;
using bbs::parse_bars;
using bbs::parse_extra_fixed;
using bbs::parse_extra_span;
using bbs::parse_extra_mesh;

static std::vector<std::wstring> summ(const bbs::SummaryRow& s) {
    return {toW(s.dia), std::to_wstring(s.nos), toW(bbs::format_num(s.total_length_m)),
            toW(bbs::format_num(s.weight_kg))};
}
static std::vector<std::wstring> bar(const bbs::BarEntry& e) {
    return {toW(e.mark), toW(e.bar_role), toW(bbs::format_dia(e.dia)), toW(bbs::format_num(e.length_mm)),
            std::to_wstring(e.nos)};
}

// Collect Extra1..Extra3 slots (exN_dia / exN_nos / exN_len) plus legacy extra_fixed text.
static std::vector<bbs::ExtraFixed> collect_extra_fixed(const bbs::RawRow& r) {
    auto out = parse_extra_fixed(to_str(r, "extra_fixed"));
    for (int i = 1; i <= 3; ++i) {
        std::string n = std::to_string(i);
        bbs::ExtraFixed e;
        e.dia = to_float(r, "ex" + n + "_dia");
        e.nos = to_int(r, "ex" + n + "_nos");
        e.length_mm = to_float(r, "ex" + n + "_len");
        if (e.dia > 0 && e.nos > 0 && e.length_mm > 0) out.push_back(e);
    }
    return out;
}

static std::vector<bbs::ExtraSpan> collect_extra_span(const bbs::RawRow& r) {
    auto out = parse_extra_span(to_str(r, "extra_span"));
    for (int i = 1; i <= 3; ++i) {
        std::string n = std::to_string(i);
        bbs::ExtraSpan e;
        e.dia = to_float(r, "esp" + n + "_dia");
        e.nos = to_int(r, "esp" + n + "_nos");
        e.frac = to_float(r, "esp" + n + "_frac");
        if (e.dia > 0 && e.nos > 0 && e.frac > 0) out.push_back(e);
    }
    return out;
}

static std::vector<bbs::ExtraMesh> collect_extra_mesh(const bbs::RawRow& r) {
    auto out = parse_extra_mesh(to_str(r, "extra_mesh"));
    for (int i = 1; i <= 3; ++i) {
        std::string n = std::to_string(i);
        bbs::ExtraMesh e;
        e.dia = to_float(r, "em" + n + "_dia");
        e.length_mm = to_float(r, "em" + n + "_len");
        e.spacing = to_float(r, "em" + n + "_sp");
        if (e.dia > 0 && e.length_mm > 0 && e.spacing > 0) out.push_back(e);
    }
    return out;
}

GenResult computeColumns(const std::vector<bbs::RawRow>& rows, const bbs::Settings& s) {
    std::vector<bbs::ColumnInput> in;
    for (const auto& r : rows) {
        bbs::ColumnInput c;
        c.mark = to_str(r, "mark");
        c.width = to_float(r, "width"); c.depth = to_float(r, "depth");
        c.height = to_float(r, "height"); c.cover = to_float(r, "cover");
        c.stirrup_dia = to_float(r, "stirrup_dia"); c.spacing = to_float(r, "spacing");
        c.hook_angle = to_int(r, "hook_angle", 135);
        c.tie_type = to_str(r, "tie_type", "Closed");
        c.bars = parse_bars(to_str(r, "bars"));
        in.push_back(c);
    }
    auto res = bbs::generate_column_bbs(in, s);
    GenResult out;
    for (const auto& e : res.entries) out.bbsRows.push_back(bar(e));
    for (const auto& r : res.summary) out.summaryRows.push_back(summ(r));
    out.summary = res.summary;
    return out;
}

GenResult computeBeams(const std::vector<bbs::RawRow>& rows, const bbs::Settings& s) {
    std::vector<bbs::BeamInput> in;
    for (const auto& r : rows) {
        bbs::BeamInput b;
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
        b.top_bars = parse_bars(to_str(r, "top_bars"));
        b.bottom_bars = parse_bars(to_str(r, "bottom_bars"));
        b.extra_fixed = collect_extra_fixed(r);
        b.extra_span = collect_extra_span(r);
        b.skin_dia = to_float(r, "skin_dia");
        b.skin_spacing = to_float(r, "skin_spacing");
        in.push_back(b);
    }
    auto res = bbs::generate_beam_bbs(in, s);
    GenResult out;
    for (const auto& e : res.entries) out.bbsRows.push_back(bar(e));
    for (const auto& r : res.summary) out.summaryRows.push_back(summ(r));
    for (const auto& n : res.notes)
        out.checkRows.push_back({toW(n)});
    out.summary = res.summary;
    return out;
}

GenResult computeSlabs(const std::vector<bbs::RawRow>& rows, const bbs::Settings& s) {
    std::vector<bbs::SlabInput> in;
    for (const auto& r : rows) {
        bbs::SlabInput sl;
        sl.mark = to_str(r, "mark");
        sl.span_x = to_float(r, "span_x"); sl.span_y = to_float(r, "span_y");
        sl.thickness = to_float(r, "thickness"); sl.cover = to_float(r, "cover");
        sl.concrete_grade = to_str(r, "concrete_grade", "M25");
        sl.steel_grade = to_str(r, "steel_grade", "Fe415");
        sl.slab_type = to_str(r, "slab_type", "Two-Way");
        sl.dia_x = to_float(r, "dia_x"); sl.spacing_x = to_float(r, "spacing_x");
        sl.dia_y = to_float(r, "dia_y"); sl.spacing_y = to_float(r, "spacing_y");
        sl.extra_fixed = collect_extra_fixed(r);
        sl.extra_mesh = collect_extra_mesh(r);
        in.push_back(sl);
    }
    auto res = bbs::generate_slab_bbs(in, s);
    GenResult out;
    for (const auto& e : res.entries) out.bbsRows.push_back(bar(e));
    for (const auto& r : res.summary) out.summaryRows.push_back(summ(r));
    for (const auto& c : res.checks)
        out.checkRows.push_back({toW(c.mark), toW(bbs::format_num(c.ast_provided_x)),
                                 toW(bbs::format_num(c.ast_min)), toW(c.status_x),
                                 toW(bbs::format_num(c.ast_provided_y)), toW(c.status_y)});
    out.summary = res.summary;
    return out;
}

GenResult computeFootings(const std::vector<bbs::RawRow>& rows, const bbs::Settings& s) {
    std::vector<bbs::FootingInput> in;
    for (const auto& r : rows) {
        bbs::FootingInput f;
        f.mark = to_str(r, "mark");
        f.footing_type = to_str(r, "footing_type", "Isolated");
        f.length_l = to_float(r, "length_l"); f.width_b = to_float(r, "width_b");
        f.col_dim_l = to_float(r, "col_dim_l"); f.col_dim_b = to_float(r, "col_dim_b");
        f.col2_dim_l = to_float(r, "col2_dim_l"); f.col2_dim_b = to_float(r, "col2_dim_b");
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
    auto res = bbs::generate_footing_bbs(in, s);
    GenResult out;
    for (const auto& e : res.entries) out.bbsRows.push_back(bar(e));
    for (const auto& r : res.summary) out.summaryRows.push_back(summ(r));
    for (const auto& c : res.checks)
        out.checkRows.push_back({toW(c.mark), toW(bbs::format_num(c.ld_required_l)),
                                 toW(bbs::format_num(c.available_l)), toW(c.status_anchorage_l),
                                 toW(bbs::format_num(c.ld_required_b)),
                                 toW(bbs::format_num(c.available_b)), toW(c.status_anchorage_b),
                                 toW(bbs::format_num(c.ast_provided_l)), toW(bbs::format_num(c.ast_min)),
                                 toW(c.status_minsteel_l), toW(c.status_minsteel_b), toW(c.note)});
    out.summary = res.summary;
    return out;
}

GenResult computeWalls(const std::vector<bbs::RawRow>& rows, const bbs::Settings& s) {
    std::vector<bbs::WallInput> in;
    for (const auto& r : rows) {
        bbs::WallInput w;
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
    auto res = bbs::generate_wall_bbs(in, s);
    GenResult out;
    for (const auto& e : res.entries) out.bbsRows.push_back(bar(e));
    for (const auto& r : res.summary) out.summaryRows.push_back(summ(r));
    for (const auto& c : res.checks)
        out.checkRows.push_back({toW(c.mark), toW(bbs::format_num(c.ast_stem)),
                                 toW(bbs::format_num(c.ast_min_stem)), toW(c.status_stem),
                                 toW(bbs::format_num(c.ast_base)), toW(bbs::format_num(c.ast_min_base)),
                                 toW(c.status_base), toW(c.note)});
    out.summary = res.summary;
    return out;
}

static std::vector<std::vector<std::string>> toUtf8(const std::vector<std::vector<std::wstring>>& in) {
    std::vector<std::vector<std::string>> out;
    for (const auto& r : in) {
        std::vector<std::string> cells;
        for (const auto& c : r) cells.push_back(fromW(c));
        out.push_back(cells);
    }
    return out;
}

std::vector<bbs::ReportSection> buildReportSections(const bbs::ProjectData& p) {
    std::vector<bbs::ReportSection> sec;
    std::vector<std::vector<bbs::SummaryRow>> allSummaries;

    auto add = [&](const std::string& name, const GenResult& gr,
                   const std::vector<std::string>& bbsHdr, const std::vector<std::string>& sumHdr,
                   const std::vector<std::string>& chkHdr, const std::string& chkTitle) {
        if (gr.bbsRows.empty()) return;
        sec.push_back({name + " — bar bending schedule", bbsHdr, toUtf8(gr.bbsRows), ""});
        sec.push_back({name + " — steel summary", sumHdr, toUtf8(gr.summaryRows), ""});
        if (!gr.checkRows.empty()) sec.push_back({chkTitle, chkHdr, toUtf8(gr.checkRows), ""});
        allSummaries.push_back(gr.summary);
    };

    std::vector<std::string> bbsHdr{"Mark", "Role", "Dia (mm)", "Length (mm)", "Nos"};
    std::vector<std::string> sumHdr{"Dia (mm)", "Nos", "Total Length (m)", "Weight (kg)"};

    add("Columns", computeColumns(p.columns, p.settings), bbsHdr, sumHdr, {}, "");
    add("Beams", computeBeams(p.beams, p.settings), bbsHdr, sumHdr,
        {"Note"}, "Beams — detailing notes (IS 456)");
    add("Slabs", computeSlabs(p.slabs, p.settings), bbsHdr, sumHdr,
        {"Mark", "Ast prov-X", "Ast min", "Status-X", "Ast prov-Y", "Status-Y"},
        "Slabs — minimum steel check (IS 456 Cl. 26.5.2.1)");
    add("Footings", computeFootings(p.footings, p.settings), bbsHdr, sumHdr,
        {"Mark", "Ld req-L", "Avail-L", "Anchorage-L", "Ld req-B", "Avail-B", "Anchorage-B",
         "Ast prov-L", "Ast min", "Min steel-L", "Min steel-B", "Note"},
        "Footings — anchorage & min steel (IS 456 Cl. 26.2 / Cl. 34)");
    add("Retaining walls", computeWalls(p.walls, p.settings), bbsHdr, sumHdr,
        {"Mark", "Ast stem", "Ast min stem", "Status stem", "Ast base", "Ast min base", "Status base", "Note"},
        "Walls — minimum steel check (IS 456 Cl. 26.5.2.1)");

    if (!allSummaries.empty()) {
        auto merged = bbs::merge_summaries(allSummaries);
        std::vector<std::vector<std::string>> rows;
        for (const auto& r : merged)
            rows.push_back({r.dia, std::to_string(r.nos), bbs::format_num(r.total_length_m),
                            bbs::format_num(r.weight_kg)});
        sec.insert(sec.begin(), {"Project steel summary (all elements)", sumHdr, rows,
                                 "Consolidated steel total by bar diameter."});
    }
    return sec;
}

}  // namespace ui
