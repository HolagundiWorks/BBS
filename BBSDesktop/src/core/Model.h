// Model.h — core data types for the Bar Bending Schedule engine.
// IS 456 quantity-estimation model (not a full design tool).
#pragma once

#include <string>
#include <vector>
#include <map>

namespace bbs {

// A raw input row as typed into the UI: field-key -> text value.
using RawRow = std::map<std::string, std::string>;

struct Settings {
    std::vector<int> diameters{8, 10, 12, 16, 20, 25, 28, 32, 36, 40};

    std::map<int, double> hook_allowance{{90, 9}, {135, 12}, {180, 16}};

    std::map<std::string, double> tau_bd{
        {"M20", 1.92}, {"M25", 2.24}, {"M30", 2.4}, {"M35", 2.56}, {"M40", 2.72}};

    std::map<std::string, double> fy{
        {"Fe250", 250}, {"Fe415", 415}, {"Fe500", 500}, {"Fe550", 550}};

    double hook_allowance_per_hook(int angle) const {
        auto it = hook_allowance.find(angle);
        return it == hook_allowance.end() ? 12.0 : it->second;
    }
    double get_tau_bd(const std::string& grade) const {
        auto it = tau_bd.find(grade);
        return it == tau_bd.end() ? 1.92 : it->second;
    }
    double get_fy(const std::string& grade) const {
        auto it = fy.find(grade);
        return it == fy.end() ? 415.0 : it->second;
    }
    // Ld = (dia * 0.87 * fy) / (4 * tau_bd)  [IS 456 Cl. 26.2.1, design form].
    double development_length(double dia, const std::string& concrete,
                              const std::string& steel) const {
        double f = get_fy(steel), t = get_tau_bd(concrete);
        if (t <= 0) return 0.0;
        return dia * 0.87 * f / (4 * t);
    }
    // IS 456 Cl. 26.5.2.1: 0.15% for mild steel (Fe250), 0.12% for HYSD.
    double min_steel_percent(const std::string& steel) const {
        return steel == "Fe250" ? 0.15 : 0.12;
    }
};

// One schedule line: identical bars aggregated with Nos (not one row per physical bar).
struct BarEntry {
    std::string element_type;  // "Column" / "Beam" / "Slab" / "Footing" / "Wall"
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
    std::map<int, int> bars;
};

struct BeamInput {
    std::string mark;
    double span = 0, width = 0, depth = 0, cover = 0;
    std::string concrete_grade = "M25", steel_grade = "Fe500";
    double stirrup_dia = 0, spacing_support = 0, spacing_middle = 0;
    int legs = 2, hook_angle = 135;
    std::string top_bar_type = "At Support";
    std::map<int, int> top_bars, bottom_bars;
    std::vector<ExtraFixed> extra_fixed;
    std::vector<ExtraSpan> extra_span;
    double skin_dia = 0, skin_spacing = 0;
};

struct SlabInput {
    std::string mark;
    double span_x = 0, span_y = 0, thickness = 0, cover = 0;
    std::string concrete_grade = "M25", steel_grade = "Fe415", slab_type = "Two-Way";
    double dia_x = 0, spacing_x = 0, dia_y = 0, spacing_y = 0;
    std::vector<ExtraFixed> extra_fixed;
    std::vector<ExtraMesh> extra_mesh;  // length + spacing; count uses span_y as orthogonal
};

struct FootingInput {
    std::string mark;
    std::string footing_type = "Isolated";  // Isolated | Double | Strip | Raft
    double length_l = 0, width_b = 0, col_dim_l = 0, col_dim_b = 0, depth = 0, cover = 0;
    // Double footing: second column footprint (optional).
    double col2_dim_l = 0, col2_dim_b = 0;
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

struct ColumnResult { std::vector<BarEntry> entries; std::vector<SummaryRow> summary; };
struct BeamResult   { std::vector<BarEntry> entries; std::vector<SummaryRow> summary;
                      std::vector<std::string> notes; };
struct SlabResult   { std::vector<BarEntry> entries; std::vector<SummaryRow> summary; std::vector<SlabCheck> checks; };
struct FootingResult{ std::vector<BarEntry> entries; std::vector<SummaryRow> summary; std::vector<FootingCheck> checks; };
struct WallResult   { std::vector<BarEntry> entries; std::vector<SummaryRow> summary; std::vector<WallCheck> checks; };

}  // namespace bbs
