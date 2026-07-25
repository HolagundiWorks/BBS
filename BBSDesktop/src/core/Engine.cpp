// Engine.cpp — BBS calculation engine (aggregated Nos schedule lines).
#include "Engine.h"

#include <algorithm>
#include <cmath>
#include <cstdlib>
#include <cstdio>
#include <map>

namespace bbs {

static constexpr double PI = 3.14159265358979323846;

double round2(double x) {
    return std::round(x * 100.0) / 100.0;
}

std::string format_num(double x, int max_decimals) {
    char buf[64];
    std::snprintf(buf, sizeof(buf), "%.*f", max_decimals, x);
    std::string s(buf);
    if (s.find('.') != std::string::npos) {
        while (!s.empty() && s.back() == '0') s.pop_back();
        if (!s.empty() && s.back() == '.') s.pop_back();
    }
    if (s == "-0") s = "0";
    return s;
}

std::string format_dia(double x) { return format_num(x, 3); }

static int spacing_count(double span, double spacing) {
    if (spacing <= 0) return 1;
    return static_cast<int>(std::floor(span / spacing)) + 1;
}

static void push_bar(std::vector<BarEntry>& entries, const std::string& etype,
                     const std::string& mark, const std::string& role,
                     double dia, double length_mm, int nos) {
    if (nos <= 0 || dia <= 0 || length_mm <= 0) return;
    entries.push_back({etype, mark, role, dia, round2(length_mm), nos});
}

static void push_extras_fixed(std::vector<BarEntry>& entries, const std::string& etype,
                              const std::string& mark, const std::vector<ExtraFixed>& extras) {
    for (const auto& e : extras)
        push_bar(entries, etype, mark, "Extra", e.dia, e.length_mm, e.nos);
}

static double ast_provided(double dia, double spacing) {
    if (spacing <= 0 || dia <= 0) return 0.0;
    return (PI / 4.0 * dia * dia) * (1000.0 / spacing);
}
static double ast_min_val(double thickness, const std::string& steel, const Settings& s) {
    return s.min_steel_percent(steel) / 100.0 * 1000.0 * thickness;
}

// ============================================================ COLUMNS

namespace {
struct StirrupResult {
    bool continuous = false;
    int count = 0;
    double length_each = 0.0;
    double total_length = 0.0;
};

StirrupResult column_stirrups(double width, double depth, double height, double cover,
                              double dia, double spacing, int hook_angle,
                              const std::string& tie_type, const Settings& s) {
    double hook_per_end = s.hook_allowance_per_hook(hook_angle) * dia;
    double total_hook = 2 * hook_per_end;
    StirrupResult r;

    if (tie_type == "Spiral") {
        double di = width - 2 * cover;
        double pitch = spacing;
        double len_per_turn = std::sqrt(std::pow(PI * di, 2) + pitch * pitch);
        double turns = (pitch > 0) ? height / pitch : 0.0;
        r.continuous = true;
        r.total_length = turns * len_per_turn + total_hook;
        r.count = 1;
        return r;
    }
    if (tie_type == "Circular") {
        double di = width - 2 * cover;
        r.length_each = PI * di + total_hook;
        r.count = spacing_count(height, spacing);
        return r;
    }
    double b = width - 2 * cover;
    double h = depth - 2 * cover;
    double length_each = 2 * (b + h) + total_hook;
    int multiplier = (tie_type == "Double Tie") ? 2 : 1;
    r.count = spacing_count(height, spacing);
    r.length_each = multiplier * length_each;
    return r;
}
}  // namespace

ColumnResult generate_column_bbs(const std::vector<ColumnInput>& rows, const Settings& s) {
    std::vector<BarEntry> entries;
    for (const auto& row : rows) {
        if (!(row.width && row.depth && row.height)) continue;

        StirrupResult st = column_stirrups(row.width, row.depth, row.height, row.cover,
                                           row.stirrup_dia, row.spacing, row.hook_angle,
                                           row.tie_type, s);
        if (st.continuous) {
            push_bar(entries, "Column", row.mark, "Stirrup(Spiral)", row.stirrup_dia, st.total_length, 1);
        } else {
            push_bar(entries, "Column", row.mark, "Stirrup", row.stirrup_dia, st.length_each, st.count);
        }
        for (const auto& kv : row.bars) {
            if (!kv.second) continue;
            push_bar(entries, "Column", row.mark, "Main", static_cast<double>(kv.first), row.height, kv.second);
        }
    }
    return {entries, summarize(entries)};
}

// ============================================================ BEAMS

BeamResult generate_beam_bbs(const std::vector<BeamInput>& rows, const Settings& s) {
    std::vector<BarEntry> entries;
    std::vector<std::string> notes;
    for (const auto& row : rows) {
        if (!(row.span && row.width && row.depth)) continue;

        double hook_per_end = s.hook_allowance_per_hook(row.hook_angle) * row.stirrup_dia;
        double total_hook = 2 * hook_per_end;
        double b = row.width - 2 * row.cover;
        double h = row.depth - 2 * row.cover;
        double length_each = 2 * (b + h) + total_hook;

        double effective_depth = row.depth - row.cover;
        double support_zone_len = 2 * effective_depth;
        int count_support_zone = spacing_count(support_zone_len, row.spacing_support);
        double middle_len = row.span - 2 * support_zone_len;
        int count_middle = (middle_len > 0) ? spacing_count(middle_len, row.spacing_middle) : 0;
        int total_count = 2 * count_support_zone + count_middle;

        push_bar(entries, "Beam", row.mark, "Stirrup", row.stirrup_dia, length_each, total_count);

        if (row.legs == 4) {
            double ct_length = h + total_hook;
            push_bar(entries, "Beam", row.mark, "Crosstie", row.stirrup_dia, ct_length, total_count);
        }

        for (const auto& kv : row.top_bars) {
            if (!kv.second) continue;
            double dia = static_cast<double>(kv.first);
            double ld = s.development_length(dia, row.concrete_grade, row.steel_grade);
            double top_len;
            int physical_count;
            if (row.top_bar_type == "At Support") {
                top_len = ld + 0.3 * row.span;
                physical_count = kv.second * 2;
            } else {
                top_len = row.span + 2 * ld;
                physical_count = kv.second;
            }
            push_bar(entries, "Beam", row.mark, "Top", dia, top_len, physical_count);
        }
        for (const auto& kv : row.bottom_bars) {
            if (!kv.second) continue;
            double dia = static_cast<double>(kv.first);
            double ld = s.development_length(dia, row.concrete_grade, row.steel_grade);
            double bot_len = row.span + 2 * ld;
            push_bar(entries, "Beam", row.mark, "Bottom", dia, bot_len, kv.second);
        }

        push_extras_fixed(entries, "Beam", row.mark, row.extra_fixed);

        for (const auto& e : row.extra_span) {
            if (e.nos <= 0 || e.dia <= 0 || e.frac <= 0) continue;
            push_bar(entries, "Beam", row.mark, "Extra-Span", e.dia, e.frac * row.span, e.nos);
        }

        // Skin / side-face reinforcement — IS 456 Cl. 26.5.1.3 when overall depth > 750 mm.
        if (row.skin_dia > 0 && row.skin_spacing > 0) {
            double skin_len = row.depth - 2 * row.cover;
            if (skin_len > 0) {
                // Clear web height between top & bottom bars ≈ depth − 2*cover; both faces.
                int per_face = spacing_count(std::max(0.0, skin_len), row.skin_spacing);
                int nos = 2 * per_face;  // both side faces
                // Length along span with nominal end cover.
                double along = row.span - 2 * row.cover;
                if (along <= 0) along = row.span;
                push_bar(entries, "Beam", row.mark, "Skin", row.skin_dia, along, nos);
            }
        } else if (row.depth > 750.0) {
            notes.push_back(row.mark + ": overall depth > 750 mm — provide side-face (skin) "
                            "reinforcement per IS 456 Cl. 26.5.1.3 (enter Skin Ø / spacing).");
        }
    }
    return {entries, summarize(entries), notes};
}

// ============================================================ SLABS

SlabResult generate_slab_bbs(const std::vector<SlabInput>& rows, const Settings& s) {
    std::vector<BarEntry> entries;
    std::vector<SlabCheck> checks;
    for (const auto& row : rows) {
        if (!(row.span_x && row.span_y && row.thickness)) continue;

        double len_x = row.span_x + 2 * s.development_length(row.dia_x, row.concrete_grade, row.steel_grade);
        int count_x = spacing_count(row.span_y, row.spacing_x);
        push_bar(entries, "Slab", row.mark, "Main-X", row.dia_x, len_x, count_x);

        double len_y;
        std::string role_y;
        if (row.slab_type == "Two-Way") {
            len_y = row.span_y + 2 * s.development_length(row.dia_y, row.concrete_grade, row.steel_grade);
            role_y = "Main-Y";
        } else {
            len_y = row.span_y - 2 * row.cover;
            role_y = "Distribution-Y";
        }
        int count_y = spacing_count(row.span_x, row.spacing_y);
        push_bar(entries, "Slab", row.mark, role_y, row.dia_y, len_y, count_y);

        push_extras_fixed(entries, "Slab", row.mark, row.extra_fixed);

        for (const auto& m : row.extra_mesh) {
            if (m.dia <= 0 || m.length_mm <= 0 || m.spacing <= 0) continue;
            // Count across the longer plan dimension as a default orthogonal span.
            double ortho = std::max(row.span_x, row.span_y);
            int nos = spacing_count(ortho, m.spacing);
            push_bar(entries, "Slab", row.mark, "Extra-Mesh", m.dia, m.length_mm, nos);
        }

        double apx = ast_provided(row.dia_x, row.spacing_x);
        double amin = ast_min_val(row.thickness, row.steel_grade, s);
        double apy = ast_provided(row.dia_y, row.spacing_y);
        SlabCheck c;
        c.mark = row.mark;
        c.ast_provided_x = round2(apx);
        c.ast_min = round2(amin);
        c.status_x = apx >= amin ? "OK" : "Increase steel / reduce spacing";
        c.ast_provided_y = round2(apy);
        c.ast_min_y = round2(amin);
        c.status_y = apy >= amin ? "OK" : "Increase steel / reduce spacing";
        checks.push_back(c);
    }
    return {entries, summarize(entries), checks};
}

// ============================================================ FOOTINGS

static void footing_mesh(std::vector<BarEntry>& entries, const std::string& mark,
                         double length_l, double width_b, double cover,
                         double dia_l, double spacing_l, double dia_b, double spacing_b,
                         const char* role_l, const char* role_b) {
    if (dia_l > 0 && spacing_l > 0) {
        double bar_len_l = length_l - 2 * cover;
        int count_l = spacing_count(width_b, spacing_l);
        push_bar(entries, "Footing", mark, role_l, dia_l, bar_len_l, count_l);
    }
    if (dia_b > 0 && spacing_b > 0) {
        double bar_len_b = width_b - 2 * cover;
        int count_b = spacing_count(length_l, spacing_b);
        push_bar(entries, "Footing", mark, role_b, dia_b, bar_len_b, count_b);
    }
}

FootingResult generate_footing_bbs(const std::vector<FootingInput>& rows, const Settings& s) {
    std::vector<BarEntry> entries;
    std::vector<FootingCheck> checks;
    for (const auto& row : rows) {
        if (!(row.length_l && row.width_b && row.depth)) continue;
        std::string type = row.footing_type.empty() ? "Isolated" : row.footing_type;

        footing_mesh(entries, row.mark, row.length_l, row.width_b, row.cover,
                     row.dia_l, row.spacing_l, row.dia_b, row.spacing_b, "Main-L", "Main-B");

        // Optional top mesh (Double / Raft / any type when entered).
        if (row.top_dia_l > 0 || row.top_dia_b > 0) {
            footing_mesh(entries, row.mark, row.length_l, row.width_b, row.cover,
                         row.top_dia_l, row.top_spacing_l, row.top_dia_b, row.top_spacing_b,
                         "Top-L", "Top-B");
        }

        push_extras_fixed(entries, "Footing", row.mark, row.extra_fixed);

        // Anchorage: isolated / double use column faces; strip/raft skip or use entered col dims if any.
        double col_l = row.col_dim_l, col_b = row.col_dim_b;
        if (type == "Double" && row.col2_dim_l > 0) {
            // Conservative: use the larger column for available embedment estimate.
            col_l = std::max(row.col_dim_l, row.col2_dim_l);
            col_b = std::max(row.col_dim_b, row.col2_dim_b);
        }

        double ld_l = s.development_length(row.dia_l, row.concrete_grade, row.steel_grade);
        double ld_b = s.development_length(row.dia_b, row.concrete_grade, row.steel_grade);
        double avail_l = 0, avail_b = 0;
        std::string anch_l = "N/A", anch_b = "N/A";
        if (col_l > 0 && (type == "Isolated" || type == "Double")) {
            avail_l = (row.length_l - col_l) / 2 - row.cover;
            avail_b = (row.width_b - col_b) / 2 - row.cover;
            anch_l = avail_l >= ld_l ? "OK" : "Insufficient - add hook or rework";
            anch_b = avail_b >= ld_b ? "OK" : "Insufficient - add hook or rework";
        } else if (type == "Strip" || type == "Raft") {
            anch_l = "Check on drawings";
            anch_b = "Check on drawings";
        }

        double apl = ast_provided(row.dia_l, row.spacing_l);
        double amin = ast_min_val(row.depth, row.steel_grade, s);
        double apb = ast_provided(row.dia_b, row.spacing_b);

        FootingCheck c;
        c.mark = row.mark;
        c.ld_required_l = round2(ld_l);
        c.available_l = round2(avail_l);
        c.status_anchorage_l = anch_l;
        c.ld_required_b = round2(ld_b);
        c.available_b = round2(avail_b);
        c.status_anchorage_b = anch_b;
        c.ast_provided_l = round2(apl);
        c.ast_min = round2(amin);
        c.status_minsteel_l = apl >= amin ? "OK" : "Increase steel / reduce spacing";
        c.ast_provided_b = round2(apb);
        c.ast_min_b = round2(amin);
        c.status_minsteel_b = apb >= amin ? "OK" : "Increase steel / reduce spacing";
        c.note = type + " — IS 456 Cl. 34 / Cl. 26.5.2.1";
        checks.push_back(c);
    }
    return {entries, summarize(entries), checks};
}

// ============================================================ RETAINING WALLS

WallResult generate_wall_bbs(const std::vector<WallInput>& rows, const Settings& s) {
    std::vector<BarEntry> entries;
    std::vector<WallCheck> checks;
    for (const auto& row : rows) {
        if (!(row.wall_length && row.stem_h && row.stem_t)) continue;

        double base_w = row.heel + row.toe + row.stem_t;

        // Stem vertical — tension face
        if (row.stem_v_dia > 0 && row.stem_v_spacing > 0) {
            double len = row.stem_h - row.cover;
            if (row.base_t > 0) len += std::min(row.base_t - row.cover, row.base_t);  // embed into base
            int nos = spacing_count(row.wall_length, row.stem_v_spacing);
            std::string role = row.tension_face == "Back" ? "Stem-V-Back" : "Stem-V-Front";
            push_bar(entries, "Wall", row.mark, role, row.stem_v_dia, len, nos);
        }
        // Other face vertical
        if (row.stem_v_back_dia > 0 && row.stem_v_back_spacing > 0) {
            double len = row.stem_h - row.cover;
            int nos = spacing_count(row.wall_length, row.stem_v_back_spacing);
            std::string role = row.tension_face == "Back" ? "Stem-V-Front" : "Stem-V-Back";
            push_bar(entries, "Wall", row.mark, role, row.stem_v_back_dia, len, nos);
        }
        // Stem horizontal (distribution along height)
        if (row.stem_h_dia > 0 && row.stem_h_spacing > 0) {
            double len = row.wall_length - 2 * row.cover;
            int nos = spacing_count(row.stem_h, row.stem_h_spacing);
            push_bar(entries, "Wall", row.mark, "Stem-H", row.stem_h_dia, len, nos);
        }
        // Base mesh
        if (base_w > 0 && row.base_t > 0) {
            if (row.base_l_dia > 0 && row.base_l_spacing > 0) {
                double len = row.wall_length - 2 * row.cover;
                int nos = spacing_count(base_w, row.base_l_spacing);
                push_bar(entries, "Wall", row.mark, "Base-L", row.base_l_dia, len, nos);
            }
            if (row.base_b_dia > 0 && row.base_b_spacing > 0) {
                double len = base_w - 2 * row.cover;
                int nos = spacing_count(row.wall_length, row.base_b_spacing);
                push_bar(entries, "Wall", row.mark, "Base-B", row.base_b_dia, len, nos);
            }
        }

        push_extras_fixed(entries, "Wall", row.mark, row.extra_fixed);

        if (row.link_dia > 0 && row.link_spacing > 0) {
            double hook = 2 * s.hook_allowance_per_hook(135) * row.link_dia;
            double b_clr = std::max(0.0, row.stem_t - 2 * row.cover);
            // Closed link around stem thickness; second dimension uses a nominal 100 mm lap band.
            double h_clr = 100.0;
            double length_each = 2 * (b_clr + h_clr) + hook;
            if (row.link_legs >= 4) length_each += h_clr + hook;
            double along = row.stem_v_spacing > 0 ? row.stem_v_spacing : row.link_spacing;
            int nos = spacing_count(row.stem_h, row.link_spacing) * spacing_count(row.wall_length, along);
            push_bar(entries, "Wall", row.mark, "Link", row.link_dia, length_each, nos);
        }

        double amin_stem = ast_min_val(row.stem_t, row.steel_grade, s);
        double amin_base = row.base_t > 0 ? ast_min_val(row.base_t, row.steel_grade, s) : 0;
        double ap_stem = ast_provided(row.stem_v_dia, row.stem_v_spacing);
        double ap_base = ast_provided(row.base_l_dia > 0 ? row.base_l_dia : row.base_b_dia,
                                      row.base_l_spacing > 0 ? row.base_l_spacing : row.base_b_spacing);

        WallCheck c;
        c.mark = row.mark;
        c.ast_stem = round2(ap_stem);
        c.ast_min_stem = round2(amin_stem);
        c.status_stem = ap_stem >= amin_stem ? "OK" : "Increase stem steel / reduce spacing";
        c.ast_base = round2(ap_base);
        c.ast_min_base = round2(amin_base);
        c.status_base = (row.base_t <= 0) ? "N/A"
                        : (ap_base >= amin_base ? "OK" : "Increase base steel / reduce spacing");
        c.note = "Quantity estimate — IS 456 min steel Cl. 26.5.2.1; verify tension face on drawings.";
        checks.push_back(c);
    }
    return {entries, summarize(entries), checks};
}

// ============================================================ SUMMARIES

std::vector<SummaryRow> summarize(const std::vector<BarEntry>& entries) {
    std::map<double, std::pair<int, double>> by_dia;  // nos, sum length_mm
    for (const auto& e : entries) {
        auto& a = by_dia[e.dia];
        a.first += e.nos;
        a.second += e.nos * e.length_mm;
    }

    std::vector<SummaryRow> rows;
    int total_nos = 0;
    double total_len = 0.0, total_wt = 0.0;
    for (const auto& kv : by_dia) {
        double dia = kv.first;
        int nos = kv.second.first;
        double total_length_m = kv.second.second / 1000.0;
        double weight_kg = total_length_m * dia * dia / 162.0;

        SummaryRow r;
        r.dia = format_dia(dia);
        r.nos = nos;
        r.total_length_m = round2(total_length_m);
        r.weight_kg = round2(weight_kg);
        rows.push_back(r);

        total_nos += nos;
        total_len += round2(total_length_m);
        total_wt += round2(weight_kg);
    }
    rows.push_back({"TOTAL", total_nos, round2(total_len), round2(total_wt)});
    return rows;
}

std::vector<SummaryRow> merge_summaries(const std::vector<std::vector<SummaryRow>>& lists) {
    struct Acc { int nos = 0; double len = 0, wt = 0; };
    std::map<double, Acc> by_dia;
    for (const auto& summary : lists) {
        for (const auto& r : summary) {
            if (r.dia == "TOTAL") continue;
            double d = std::atof(r.dia.c_str());
            auto& a = by_dia[d];
            a.nos += r.nos;
            a.len += r.total_length_m;
            a.wt += r.weight_kg;
        }
    }
    std::vector<SummaryRow> rows;
    int total_nos = 0;
    double total_len = 0.0, total_wt = 0.0;
    for (const auto& kv : by_dia) {
        SummaryRow r;
        r.dia = format_dia(kv.first);
        r.nos = kv.second.nos;
        r.total_length_m = round2(kv.second.len);
        r.weight_kg = round2(kv.second.wt);
        rows.push_back(r);
        total_nos += kv.second.nos;
        total_len += round2(kv.second.len);
        total_wt += round2(kv.second.wt);
    }
    rows.push_back({"TOTAL", total_nos, round2(total_len), round2(total_wt)});
    return rows;
}

}  // namespace bbs
