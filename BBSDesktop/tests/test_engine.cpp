// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

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
    c.bars = {{16, 4}};
    auto cr = generate_column_bbs({c}, s);
    expect(!cr.entries.empty(), "column has stirrup+main lines");
    int stirrup_nos = 0;
    for (const auto& e : cr.entries)
        if (e.bar_role == "Stirrup") stirrup_nos = e.nos;
    expect(stirrup_nos > 1, "column stirrups aggregated with Nos>1");
    print_summary("Column", cr.summary);

    // Cross ties for 8 bars on larger section (Auto / explicit)
    ColumnInput c2 = c;
    c2.mark = "C2"; c2.width = 450; c2.depth = 450; c2.bars = {{16, 8}}; c2.tie_type = "Cross Ties";
    auto cr2 = generate_column_bbs({c2}, s);
    bool has_cross = false;
    for (const auto& e : cr2.entries) if (e.bar_role == "Crosstie") has_cross = true;
    expect(has_cross, "cross ties present for Cross Ties arrangement");

    ColumnInput c3 = c2; c3.mark = "C3"; c3.tie_type = "Diagonal Ties";
    auto cr3 = generate_column_bbs({c3}, s);
    bool has_diag = false;
    for (const auto& e : cr3.entries) if (e.bar_role == "DiagonalTie") has_diag = true;
    expect(has_diag, "diagonal ties present");

    ColumnInput c4 = c2; c4.mark = "C4"; c4.bars = {{20, 12}}; c4.tie_type = "Group Ties";
    auto cr4 = generate_column_bbs({c4}, s);
    bool has_grp = false;
    for (const auto& e : cr4.entries) if (e.bar_role == "GroupTie") has_grp = true;
    expect(has_grp, "group ties present");

    // ---- Beams: extras + skin ----
    BeamInput b;
    b.mark = "B1"; b.span = 4000; b.width = 230; b.depth = 800; b.cover = 25;
    b.concrete_grade = "M25"; b.steel_grade = "Fe500"; b.stirrup_dia = 8;
    b.spacing_support = 100; b.spacing_middle = 150; b.legs = 2; b.hook_angle = 135;
    b.top_bar_type = "At Support";
    b.hanger_bars = {{12, 2}};
    b.top_bars = {{16, 2}}; b.bottom_bars = {{16, 3}};
    b.extra_fixed = {{12, 2, 2500}};
    b.extra_span = {{16, 2, 0.3}};
    b.skin_dia = 10; b.skin_spacing = 200;
    auto br = generate_beam_bbs({b}, s);
    bool has_extra = false, has_span = false, has_skin = false, has_hanger = false;
    for (const auto& e : br.entries) {
        if (e.bar_role == "Extra") has_extra = true;
        if (e.bar_role == "Extra-Span") { has_span = true; expect_near(e.length_mm, 1200.0, "extra-span 0.3*4000"); }
        if (e.bar_role == "Skin") has_skin = true;
        if (e.bar_role == "Hanger") { has_hanger = true; expect(e.nos == 2, "hanger nos"); }
        if (e.bar_role == "Stirrup-s1" || e.bar_role == "Stirrup-s2")
            expect(e.nos >= 1, "beam stirrups use Nos");
        if (e.bar_role == "TopMain" || e.bar_role == "BottomMain")
            expect(e.nos >= 1, "beam main bars present");
    }
    expect(has_extra && has_span && has_skin && has_hanger, "beam extras + skin + hanger present");
    print_summary("Beam", br.summary);

    // Skin by explicit nos per face
    BeamInput bSkin = b; bSkin.mark = "B1s"; bSkin.skin_spacing = 0; bSkin.skin_nos = 4;
    auto brSkin = generate_beam_bbs({bSkin}, s);
    bool skin4 = false;
    for (const auto& e : brSkin.entries)
        if (e.bar_role == "Skin") { skin4 = (e.nos == 8); break; }  // 4 per face × 2
    expect(skin4, "skin_nos 4/face → 8 total");

    // Deep beam without skin → note
    BeamInput b2 = b; b2.skin_dia = 0; b2.skin_spacing = 0; b2.skin_nos = 0; b2.mark = "B2";
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

    // ---- Stairs ----
    StairInput st;
    st.mark = "ST1"; st.n_risers = 12; st.n_flights = 1;
    st.going = 250; st.riser = 150; st.waist_t = 150; st.flight_width = 1200; st.cover = 20;
    st.landing_len = 1200; st.landing_t = 150;
    st.concrete_grade = "M25"; st.steel_grade = "Fe500";
    st.main_dia = 12; st.main_spacing = 150;
    st.dist_dia = 8; st.dist_spacing = 200;
    st.landing_dia = 10; st.landing_spacing = 150;
    auto str = generate_stair_bbs({st}, s);
    bool has_main = false, has_dist = false, has_land = false;
    for (const auto& e : str.entries) {
        if (e.bar_role == "Main") has_main = true;
        if (e.bar_role == "Dist") has_dist = true;
        if (e.bar_role == "Landing-L" || e.bar_role == "Landing-B") has_land = true;
    }
    expect(has_main && has_dist && has_land, "stair main+dist+landing present");
    expect(!str.checks.empty(), "stair geometry check");
    double expect_slope = std::sqrt(std::pow(11 * 250.0, 2) + std::pow(12 * 150.0, 2));
    expect_near(str.checks[0].slope_len, expect_slope, "stair slope √(((n−1)g)²+(n·r)²)");
    print_summary("Stair", str.summary);

    // ---- Parse helpers ----
    auto xf = parse_extra_fixed("16:2:2500, 12:4:1800");
    expect(xf.size() == 2 && xf[0].nos == 2, "parse_extra_fixed");
    auto xs = parse_extra_span("16:2:0.3");
    expect(xs.size() == 1 && std::fabs(xs[0].frac - 0.3) < 1e-9, "parse_extra_span");

    // ---- IS 456 / IS 2502 detailing formulas ----
    // Fe500/M25 φ16 with HYSD 1.6×τbd → Ld ≈ 47.0φ
    {
        Settings sd;
        double ld = sd.development_length(16, "M25", "Fe500");
        // Table 21 plain M25=1.4 × 1.6 HYSD → τbd_eff=2.24; Ld≈48.55φ (~47φ charts)
        double expect_ld = 16.0 * 0.87 * 500.0 / (4.0 * 1.4 * 1.6);
        expect_near(ld, expect_ld, "Ld Fe500/M25 φ16 with HYSD 1.6×τbd");
        expect(std::fabs(ld / 16.0 - 48.549) < 0.05, "Ld ≈ 47–49φ for Fe500/M25");

        Settings plain = sd; plain.hysd_bond = false;
        double ld_plain = plain.development_length(16, "M25", "Fe500");
        expect(ld_plain > ld * 1.3, "plain τbd (no HYSD) gives longer Ld");

        double lap_t = sd.lap_length(16, "M25", "Fe500", "Tension");
        expect_near(lap_t, std::max(ld, 30.0 * 16), "tension lap max(Ld, 30φ)");
        double lap_c = sd.lap_length(16, "M25", "Fe500", "Compression");
        expect_near(lap_c, std::max(0.8 * ld, 24.0 * 16), "compression lap max(0.8Ld, 24φ)");
        double lap_d = sd.lap_length(16, "M25", "Fe500", "DirectTension");
        expect_near(lap_d, std::max(2.0 * ld, 30.0 * 16), "direct tension lap max(2Ld, 30φ)");
    }

    // Stirrup cutting via beam BBS (same closed_link_cutting)
    {
        BeamInput bs;
        bs.mark = "Bstir"; bs.span = 3000; bs.width = 230; bs.depth = 450; bs.cover = 25;
        bs.concrete_grade = "M25"; bs.steel_grade = "Fe500";
        bs.stirrup_dia = 8; bs.spacing_support = 150; bs.spacing_middle = 200;
        bs.legs = 2; bs.hook_angle = 135;
        bs.hanger_bars = {{12, 2}}; bs.bottom_bars = {{16, 3}};
        auto bsr = generate_beam_bbs({bs}, s);
        double stir_len = 0;
        for (const auto& e : bsr.entries)
            if (e.bar_role == "Stirrup-s1" || e.bar_role == "Stirrup-s2") {
                stir_len = e.length_mm;
                break;
            }
        // a = 230−2·25 = 180, b = 450−2·25 = 400; hooks 2·max(10·8,75)=160; deduct 12·8=96
        // L = 2(180+400)+160−96 = 1224
        expect_near(stir_len, 1224.0, "IS 2502 closed stirrup 230×450 φ8 135° = 1224 mm");
        bool cites = false;
        for (const auto& n : bsr.notes)
            if (n.find("26.2") != std::string::npos) cites = true;
        expect(cites, "beam notes cite IS 456 Cl. 26.2");
    }

    // Column compression lap opt-in
    {
        ColumnInput cl = c;
        cl.mark = "Clap"; cl.steel_grade = "Fe500"; cl.concrete_grade = "M25";
        cl.provide_lap = "Yes"; cl.bars = {{16, 4}};
        auto clr = generate_column_bbs({cl}, s);
        bool has_lap = false;
        double lap_len = 0;
        for (const auto& e : clr.entries)
            if (e.bar_role == "Lap") { has_lap = true; lap_len = e.length_mm; }
        expect(has_lap, "column provide_lap adds Lap BBS line");
        double expect_lap = s.lap_length(16, "M25", "Fe500", "Compression");
        expect_near(lap_len, expect_lap, "column lap = max(Ld_c, 24φ)");
        bool cites = false;
        for (const auto& n : clr.notes)
            if (n.find("26.2.5") != std::string::npos) cites = true;
        expect(cites, "column lap note cites Cl. 26.2.5");
    }

    // Beam tension lap + hooked anchorage
    {
        BeamInput bl = b;
        bl.mark = "Blap"; bl.provide_lap = "Tension"; bl.lap_nos = 3;
        bl.end_anchorage = "90 Hook";
        auto blr = generate_beam_bbs({bl}, s);
        bool has_lap = false;
        for (const auto& e : blr.entries)
            if (e.bar_role == "Lap") has_lap = true;
        expect(has_lap, "beam Tension lap adds Lap BBS line");
    }

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
