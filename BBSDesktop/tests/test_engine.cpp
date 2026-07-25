// Engine smoke tests — aggregated Nos, extras, footing types, retaining walls.
#include "../src/core/Engine.h"
#include "../src/core/Parse.h"
#include <cstdio>
#include <cmath>
#include <cstdlib>

using namespace bbs;

static int g_fails = 0;

static void expect(bool ok, const char* msg) {
    if (!ok) {
        std::printf("FAIL: %s\n", msg);
        ++g_fails;
    } else {
        std::printf("ok: %s\n", msg);
    }
}

static void expect_near(double a, double b, const char* msg) {
    expect(std::fabs(a - b) < 0.05, msg);
}

static void print_summary(const char* label, const std::vector<SummaryRow>& s) {
    std::printf("[%s]\n", label);
    for (const auto& r : s)
        std::printf("  dia=%s nos=%d len=%s wt=%s\n", r.dia.c_str(), r.nos,
                    format_num(r.total_length_m).c_str(), format_num(r.weight_kg).c_str());
}

int main() {
    Settings s;

    // ---- Columns: one stirrup line with Nos ----
    ColumnInput c;
    c.mark = "C1"; c.width = 300; c.depth = 450; c.height = 3200; c.cover = 40;
    c.stirrup_dia = 8; c.spacing = 150; c.hook_angle = 135; c.tie_type = "Closed";
    c.bars = {{12, 4}, {16, 2}, {20, 4}};
    auto cr = generate_column_bbs({c}, s);
    expect(cr.entries.size() == 4, "column has 4 aggregated lines (stirrup+3 dias)");
    int stirrup_nos = 0;
    for (const auto& e : cr.entries)
        if (e.bar_role == "Stirrup") stirrup_nos = e.nos;
    expect(stirrup_nos > 1, "column stirrups aggregated with Nos>1");
    print_summary("Column", cr.summary);

    // ---- Beams: extras + skin ----
    BeamInput b;
    b.mark = "B1"; b.span = 4000; b.width = 230; b.depth = 800; b.cover = 25;
    b.concrete_grade = "M25"; b.steel_grade = "Fe500"; b.stirrup_dia = 8;
    b.spacing_support = 100; b.spacing_middle = 150; b.legs = 2; b.hook_angle = 135;
    b.top_bar_type = "At Support"; b.top_bars = {{16, 2}}; b.bottom_bars = {{16, 3}};
    b.extra_fixed = {{12, 2, 2500}};
    b.extra_span = {{16, 2, 0.3}};
    b.skin_dia = 10; b.skin_spacing = 200;
    auto br = generate_beam_bbs({b}, s);
    bool has_extra = false, has_span = false, has_skin = false;
    for (const auto& e : br.entries) {
        if (e.bar_role == "Extra") has_extra = true;
        if (e.bar_role == "Extra-Span") { has_span = true; expect_near(e.length_mm, 1200.0, "extra-span 0.3*4000"); }
        if (e.bar_role == "Skin") has_skin = true;
        if (e.bar_role == "Stirrup") expect(e.nos > 1, "beam stirrups use Nos");
    }
    expect(has_extra && has_span && has_skin, "beam extras + skin present");
    print_summary("Beam", br.summary);

    // Deep beam without skin → note
    BeamInput b2 = b; b2.skin_dia = 0; b2.skin_spacing = 0; b2.mark = "B2";
    auto br2 = generate_beam_bbs({b2}, s);
    expect(!br2.notes.empty(), "skin note when depth>750 and no skin");

    // ---- Slabs extras + crank ----
    SlabInput sl;
    sl.mark = "S1"; sl.span_x = 3000; sl.span_y = 4500; sl.thickness = 125; sl.cover = 20;
    sl.concrete_grade = "M25"; sl.steel_grade = "Fe415"; sl.slab_type = "Two-Way";
    sl.dia_x = 10; sl.spacing_x = 150; sl.dia_y = 10; sl.spacing_y = 150;
    sl.extra_fixed = {{8, 4, 2000}};
    sl.extra_mesh = {{12, 3000, 200}};
    auto slr = generate_slab_bbs({sl}, s);
    bool has_mesh = false;
    for (const auto& e : slr.entries) if (e.bar_role == "Extra-Mesh") has_mesh = true;
    expect(has_mesh, "slab extra-mesh present");

    SlabInput slc = sl; slc.mark = "S2"; slc.crank_count = 2; slc.extra_fixed.clear(); slc.extra_mesh.clear();
    auto slcr = generate_slab_bbs({slc}, s);
    double rise = 125 - 2 * 20;
    double crank = 2 * rise * std::sqrt(1.0 + 0.42 * 0.42);
    double base_x = 0;
    for (const auto& e : slr.entries)
        if (e.mark == "S1" && e.bar_role == "Main-X") base_x = e.length_mm;
    double crank_x = 0;
    for (const auto& e : slcr.entries)
        if (e.bar_role == "Main-X") crank_x = e.length_mm;
    expect_near(crank_x - base_x, crank, "slab crank adds rise*√(1+0.42²)*count");

    // ---- Footing types ----
    FootingInput f;
    f.mark = "F1"; f.footing_type = "Isolated";
    f.length_l = 2000; f.width_b = 2000; f.col_dim_l = 400; f.col_dim_b = 400;
    f.depth = 500; f.cover = 50; f.concrete_grade = "M25"; f.steel_grade = "Fe500";
    f.dia_l = 12; f.spacing_l = 150; f.dia_b = 12; f.spacing_b = 150;
    auto fr = generate_footing_bbs({f}, s);
    expect(!fr.checks.empty() && fr.checks[0].status_minsteel_l.size() > 0, "footing min-steel status set");
    expect(fr.entries.size() == 2, "isolated footing 2 mesh lines");

    FootingInput fs = f; fs.mark = "FS"; fs.footing_type = "Stepped"; fs.n_steps = 2; fs.step_height = 250;
    auto fsr = generate_footing_bbs({fs}, s);
    bool has_vert = false, has_top = false;
    for (const auto& e : fsr.entries) {
        if (e.bar_role.find("Step-Vert") != std::string::npos) has_vert = true;
        if (e.bar_role == "Top-L" || e.bar_role == "Top-B") has_top = true;
    }
    expect(has_vert && has_top, "stepped footing has vert + top mesh");

    FootingInput raft = f; raft.footing_type = "Raft"; raft.mark = "R1";
    raft.top_dia_l = 10; raft.top_spacing_l = 200; raft.top_dia_b = 10; raft.top_spacing_b = 200;
    raft.extra_fixed = {{16, 6, 1800}};
    auto rr = generate_footing_bbs({raft}, s);
    expect(rr.entries.size() >= 5, "raft bottom+top+extra");

    // ---- Wall ----
    WallInput w;
    w.mark = "RW1"; w.wall_length = 5000; w.stem_h = 3000; w.stem_t = 250;
    w.heel = 1500; w.toe = 600; w.base_t = 400; w.cover = 50;
    w.concrete_grade = "M25"; w.steel_grade = "Fe500"; w.tension_face = "Front";
    w.stem_v_dia = 12; w.stem_v_spacing = 150;
    w.stem_v_back_dia = 10; w.stem_v_back_spacing = 200;
    w.stem_h_dia = 10; w.stem_h_spacing = 200;
    w.base_l_dia = 12; w.base_l_spacing = 150;
    w.base_b_dia = 12; w.base_b_spacing = 150;
    auto wr = generate_wall_bbs({w}, s);
    expect(wr.entries.size() >= 5, "wall has stem+base lines");
    expect(!wr.checks.empty(), "wall min-steel check");
    print_summary("Wall", wr.summary);

    // ---- Parse helpers ----
    auto xf = parse_extra_fixed("16:2:2500, 12:4:1800");
    expect(xf.size() == 2 && xf[0].nos == 2, "parse_extra_fixed");
    auto xs = parse_extra_span("16:2:0.3");
    expect(xs.size() == 1 && std::fabs(xs[0].frac - 0.3) < 1e-9, "parse_extra_span");

    // ---- Weight formula ----
    double wt = 0;
    for (const auto& e : cr.entries)
        wt += (e.nos * e.length_mm / 1000.0) * e.dia * e.dia / 162.0;
    expect_near(wt, cr.summary.back().weight_kg, "summary TOTAL weight matches Σ nos*L*d²/162");

    auto merged = merge_summaries({cr.summary, br.summary, slr.summary, fr.summary, wr.summary});
    print_summary("Project", merged);

    std::printf("\n%d failure(s)\n", g_fails);
    return g_fails == 0 ? 0 : 1;
}
