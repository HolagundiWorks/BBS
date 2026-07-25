// Model.h — core data types for the Bar Bending Schedule engine.
// IS 456 quantity-estimation model (not a full design tool).
#pragma once

#include <string>
#include <vector>
#include <map>
#include <algorithm>
#include <cmath>

namespace bbs {

// A raw input row as typed into the UI: field-key -> text value.
using RawRow = std::map<std::string, std::string>;

struct Settings {
    std::vector<int> diameters{8, 10, 12, 16, 20, 25, 28, 32, 36, 40};

    // IS 2502 cutting allowances (×φ) per hook angle.
    std::map<int, double> hook_allowance{{90, 9}, {135, 10}, {180, 16}};

    // IS 2502 bend deductions (×φ): approx. 1d / 2d / 3d for 45° / 90° / 135°.
    std::map<int, double> bend_deduction{{45, 1}, {90, 2}, {135, 3}};

    // IS 456 Table 21 — plain-bar design bond stress (N/mm²).
    // HYSD / deformed: multiply by hysd_bond_factor (1.6) in effective_tau_bd.
    std::map<std::string, double> tau_bd{
        {"M20", 1.2}, {"M25", 1.4}, {"M30", 1.5}, {"M35", 1.7}, {"M40", 1.9}};

    std::map<std::string, double> fy{
        {"Fe250", 250}, {"Fe415", 415}, {"Fe500", 500}, {"Fe550", 550}};

    // Cl. 26.2.1.1 — deformed / HYSD: increase τbd by 60%.
    bool hysd_bond = true;
    double hysd_bond_factor = 1.6;
    double min_hook_mm = 75;  // IS 2502 / site floor for small φ

    double hook_allowance_per_hook(int angle) const {
        auto it = hook_allowance.find(angle);
        return it == hook_allowance.end() ? 10.0 : it->second;
    }
    double bend_deduction_factor(int angle) const {
        auto it = bend_deduction.find(angle);
        return it == bend_deduction.end() ? 2.0 : it->second;
    }
    double get_tau_bd(const std::string& grade) const {
        auto it = tau_bd.find(grade);
        return it == tau_bd.end() ? 1.2 : it->second;
    }
    double get_fy(const std::string& grade) const {
        auto it = fy.find(grade);
        return it == fy.end() ? 415.0 : it->second;
    }
    bool is_hysd(const std::string& steel) const {
        return steel != "Fe250";
    }
    double effective_tau_bd(const std::string& concrete, const std::string& steel) const {
        double t = get_tau_bd(concrete);
        if (hysd_bond && is_hysd(steel)) t *= hysd_bond_factor;
        return t;
    }
    // Hook cutting length per end (mm), floored at min_hook_mm.
    double hook_length_mm(int angle, double dia) const {
        return std::max(hook_allowance_per_hook(angle) * dia, min_hook_mm);
    }
    // Ld = (φ × 0.87 × fy) / (4 × τbd_eff)  [IS 456 Cl. 26.2.1].
    double development_length(double dia, const std::string& concrete,
                              const std::string& steel) const {
        double f = get_fy(steel), t = effective_tau_bd(concrete, steel);
        if (t <= 0 || dia <= 0) return 0.0;
        return dia * 0.87 * f / (4 * t);
    }
    // Compression development — commonly 0.8 × tension Ld.
    double compression_development_length(double dia, const std::string& concrete,
                                          const std::string& steel) const {
        return 0.8 * development_length(dia, concrete, steel);
    }
    // IS 456 Cl. 26.2.5 — mode: "Tension" | "Compression" | "DirectTension".
    double lap_length(double dia, const std::string& concrete, const std::string& steel,
                      const std::string& mode) const {
        if (dia <= 0) return 0.0;
        if (mode == "DirectTension") {
            double ld = development_length(dia, concrete, steel);
            return std::max(2.0 * ld, 30.0 * dia);
        }
        if (mode == "Compression") {
            double ld = compression_development_length(dia, concrete, steel);
            return std::max(ld, 24.0 * dia);
        }
        // Flexural tension (default)
        double ld = development_length(dia, concrete, steel);
        return std::max(ld, 30.0 * dia);
    }
    // Anchorage credit (straight equivalent) — IS 456 Cl. 26.2.2.
    double anchorage_credit_mm(const std::string& end_type, double dia) const {
        if (end_type == "90 Hook" || end_type == "90") return 8.0 * dia;
        if (end_type == "180 Hook" || end_type == "180") return 16.0 * dia;
        return 0.0;
    }
    // IS 456 Cl. 26.5.2.1: 0.15% for mild steel (Fe250), 0.12% for HYSD.
    double min_steel_percent(const std::string& steel) const {
        return steel == "Fe250" ? 0.15 : 0.12;
    }
};

// One schedule line: identical bars aggregated with Nos (not one row per physical bar).
struct BarEntry {
    std::string element_type;  // "Column" / "Beam" / "Slab" / "Footing" / "Wall" / "Stair"
    std::string mark;
    std::string bar_role;
    double dia = 0.0;
    double length_mm = 0.0;
    int nos = 1;
};

struct SummaryRow {
    std::string dia;
    int nos = 0;
    double total_length_m = 0.0;
    double weight_kg = 0.0;
};

// Extra bars: fixed cutting length.
struct ExtraFixed {
    double dia = 0;
    int nos = 0;
    double length_mm = 0;
};

// Extra bars: length = frac * span (curtailment / proportionate).
struct ExtraSpan {
    double dia = 0;
    int nos = 0;
    double frac = 0;  // e.g. 0.3 for 0.3L
};

// Extra mesh: length given; count from orthogonal span / spacing.
struct ExtraMesh {
    double dia = 0;
    double length_mm = 0;
    double spacing = 0;
};

struct ColumnInput {
    std::string mark;
    double width = 0, depth = 0, height = 0, cover = 0, stirrup_dia = 0, spacing = 0;
    int hook_angle = 135;
    std::string tie_type = "Closed";
    std::string column_type = "Rectangular";  // Circular | Square | Rectangular
    std::map<int, int> bars;
    std::string concrete_grade = "M25";
    std::string steel_grade = "Fe500";
    std::string level = "Lvl0";  // storey: Lvl0 = plinth
    // Pedestal (optional; typically under Lvl0 columns)
    double pedestal_h = 0, pedestal_w = 0, pedestal_d = 0;
    double pedestal_stirrup_dia = 0, pedestal_spacing = 0;
    std::map<int, int> pedestal_bars;
    std::vector<ExtraFixed> extra_fixed;
    // IS 456 Cl. 26.2.5 compression lap (opt-in)
    std::string provide_lap = "No";  // No | Yes
    int lap_nos = 0;  // 0 → all longitudinal bars
};

struct BeamInput {
    std::string mark;
    double span = 0, width = 0, depth = 0, cover = 0;
    std::string concrete_grade = "M25", steel_grade = "Fe500";
    double stirrup_dia = 0, spacing_support = 0, spacing_middle = 0;
    int legs = 2, hook_angle = 135;
    std::string top_bar_type = "At Support";
    std::map<int, int> top_bars, bottom_bars, hanger_bars;
    std::vector<ExtraFixed> extra_fixed;
    std::vector<ExtraSpan> extra_span;
    double skin_dia = 0, skin_spacing = 0;
    int skin_nos = 0;  // bars per face; 0 → derive from skin_spacing
    // End anchorage / optional tension lap (IS 456 Cl. 26.2)
    std::string end_anchorage = "Straight Ld";  // Straight Ld | 90 Hook | 180 Hook
    std::string provide_lap = "None";           // None | Tension
    int lap_nos = 0;  // 0 → sum of bottom bar nos
};

struct SlabInput {
    std::string mark;
    double span_x = 0, span_y = 0, thickness = 0, cover = 0;
    std::string concrete_grade = "M25", steel_grade = "Fe415", slab_type = "Two-Way";
    double dia_x = 0, spacing_x = 0, dia_y = 0, spacing_y = 0;
    // Bent-up / crank (IS 2502 practice): extra ≈ rise * √(1+0.42²) per crank.
    int crank_count = 0;       // typically 0 or 2
    double crank_rise = 0;     // 0 → thickness − 2·cover
    std::vector<ExtraFixed> extra_fixed;
    std::vector<ExtraMesh> extra_mesh;  // length + spacing; count uses span_y as orthogonal
};

struct FootingInput {
    std::string mark;
    std::string footing_type = "Isolated";  // Isolated | Double | Strip | Raft | Stepped
    double length_l = 0, width_b = 0, col_dim_l = 0, col_dim_b = 0, depth = 0, cover = 0;
    // Double footing: second column footprint (optional).
    double col2_dim_l = 0, col2_dim_b = 0;
    // Stepped: bottom plan = L×B; top plan defaults to column dims; equal setbacks.
    int n_steps = 0;             // number of risers (≥1)
    double step_height = 0;      // 0 → depth / n_steps
    double top_length = 0;       // 0 → col_dim_l
    double top_width = 0;        // 0 → col_dim_b
    std::string concrete_grade = "M25", steel_grade = "Fe500";
    double dia_l = 0, spacing_l = 0, dia_b = 0, spacing_b = 0;  // bottom mesh
    double top_dia_l = 0, top_spacing_l = 0, top_dia_b = 0, top_spacing_b = 0;  // optional top
    std::vector<ExtraFixed> extra_fixed;
};

struct WallInput {
    std::string mark;
    double wall_length = 0;   // out-of-plane length
    double stem_h = 0, stem_t = 0, heel = 0, toe = 0, base_t = 0, cover = 0;
    std::string concrete_grade = "M25", steel_grade = "Fe500";
    std::string tension_face = "Front";  // Front = earth/water face (user choice)
    double stem_v_dia = 0, stem_v_spacing = 0;           // tension-face vertical
    double stem_v_back_dia = 0, stem_v_back_spacing = 0; // other face (0 = skip)
    double stem_h_dia = 0, stem_h_spacing = 0;
    double base_l_dia = 0, base_l_spacing = 0;  // bars along wall length
    double base_b_dia = 0, base_b_spacing = 0;  // bars across base width
    std::vector<ExtraFixed> extra_fixed;
    double link_dia = 0, link_spacing = 0;
    int link_legs = 2;
};

// Single flight (or identical flights × n_flights). Quantity estimate for waist + landings.
struct StairInput {
    std::string mark;
    int n_risers = 12;
    int n_flights = 1;
    double going = 250, riser = 150, waist_t = 150, flight_width = 1200, cover = 20;
    double landing_len = 1200, landing_width = 0, landing_t = 150;  // width 0 → flight_width
    std::string concrete_grade = "M25", steel_grade = "Fe500";
    double main_dia = 0, main_spacing = 0;   // along slope, across width
    double dist_dia = 0, dist_spacing = 0;   // across slope
    double landing_dia = 0, landing_spacing = 0;  // mesh both ways when >0
    std::vector<ExtraFixed> extra_fixed;
};

struct SlabCheck {
    std::string mark;
    double ast_provided_x = 0, ast_min = 0, ast_provided_y = 0, ast_min_y = 0;
    std::string status_x, status_y;
};

struct FootingCheck {
    std::string mark;
    double ld_required_l = 0, available_l = 0, ld_required_b = 0, available_b = 0;
    double ast_provided_l = 0, ast_min = 0, ast_provided_b = 0, ast_min_b = 0;
    std::string status_anchorage_l, status_anchorage_b;
    std::string status_minsteel_l, status_minsteel_b;
    std::string note;  // e.g. footing type / IS clause hint
};

struct WallCheck {
    std::string mark;
    double ast_stem = 0, ast_min_stem = 0, ast_base = 0, ast_min_base = 0;
    std::string status_stem, status_base;
    std::string note;
};

struct StairCheck {
    std::string mark;
    double slope_len = 0, rise_total = 0, going_total = 0;
    double ast_main = 0, ast_min = 0;
    std::string status_main;
    std::string note;
};

struct ColumnResult { std::vector<BarEntry> entries; std::vector<SummaryRow> summary;
                      std::vector<std::string> notes; };
struct BeamResult   { std::vector<BarEntry> entries; std::vector<SummaryRow> summary;
                      std::vector<std::string> notes; };
struct SlabResult   { std::vector<BarEntry> entries; std::vector<SummaryRow> summary; std::vector<SlabCheck> checks; };
struct FootingResult{ std::vector<BarEntry> entries; std::vector<SummaryRow> summary; std::vector<FootingCheck> checks; };
struct WallResult   { std::vector<BarEntry> entries; std::vector<SummaryRow> summary; std::vector<WallCheck> checks; };
struct StairResult  { std::vector<BarEntry> entries; std::vector<SummaryRow> summary; std::vector<StairCheck> checks; };

}  // namespace bbs
