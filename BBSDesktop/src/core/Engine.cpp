// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

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

/** IS 2502 closed rectangular link / stirrup cutting length. */
static double closed_link_cutting(double a, double b, double dia, int hook_angle, const Settings& s) {
    double hooks = 2.0 * s.hook_length_mm(hook_angle, dia);
    double deduct;
    if (hook_angle >= 135) {
        // 3×90° corners + 2×135° hook bends (common chart practice → 12d)
        deduct = (3.0 * s.bend_deduction_factor(90) + 2.0 * s.bend_deduction_factor(135)) * dia;
    } else {
        // Five 90° bends
        deduct = 5.0 * s.bend_deduction_factor(90) * dia;
    }
    return std::max(0.0, 2.0 * (a + b) + hooks - deduct);
}

/** Straight crosstie / open leg with two end hooks. */
static double hooked_leg_cutting(double clear, double dia, int hook_angle, const Settings& s) {
    double hooks = 2.0 * s.hook_length_mm(hook_angle, dia);
    // Two end bends at hook angle (or 90° if straight bar ends)
    int bendAng = hook_angle >= 135 ? 135 : 90;
    double deduct = 2.0 * s.bend_deduction_factor(bendAng) * dia;
    return std::max(0.0, clear + hooks - deduct);
}

// ============================================================ COLUMNS

namespace {
struct StirrupResult {
    bool continuous = false;
    int count = 0;
    double length_each = 0.0;
    double total_length = 0.0;
};

static int count_long_bars(const std::map<int, int>& bars) {
    int n = 0;
    for (const auto& kv : bars) n += kv.second;
    return n;
}

/** Resolve IS 456 Cl. 26.5.3.2 tie case by column type + Auto heuristics. */
static std::string resolve_column_tie(const std::string& tie_type, const std::string& column_type,
                                      double width, double depth, double cover, double stirrup_dia, int nBars) {
    if (column_type == "Circular") {
        if (tie_type == "Spiral") return "Spiral";
        return "Circular";
    }
    if (tie_type == "Circular" || tie_type == "Spiral") {
        // Not valid for square/rect — fall through to Auto
    } else if (tie_type == "Closed" || tie_type == "Open Ties" || tie_type == "U-Ties" ||
               tie_type == "Group Ties" || tie_type == "Cross Ties" || tie_type == "Diagonal Ties") {
        if (column_type == "Square" && tie_type == "Open Ties") return "Closed";
        return tie_type;
    }
    if (tie_type == "Double Tie") return "U-Ties";

    double w = width, d = depth;
    if (column_type == "Square" || column_type == "Circular") d = w;

    double minSide = std::min(w, d);
    double clearB = std::max(1.0, w - 2 * cover);
    double clearD = std::max(1.0, d - 2 * cover);
    double longer = std::max(clearB, clearD);
    int along = std::max(2, (nBars + 3) / 4 + 1);
    double spacing = longer / std::max(1, along - 1);

    if (nBars <= 4 || minSide <= 300.0) return "Closed";
    if (nBars <= 8) {
        if (spacing > 75.0)
            return (column_type == "Square") ? "Cross Ties" : "Diagonal Ties";
        return "Closed";
    }
    if (nBars >= 12 && column_type == "Square") return "Group Ties";
    if (column_type != "Square" && std::abs(w - d) >= 50.0)
        return (spacing > 75.0 || longer > 48.0 * std::max(stirrup_dia, 6.0)) ? "Open Ties" : "U-Ties";
    return "Cross Ties";
}

StirrupResult column_stirrups(double width, double depth, double height, double cover,
                              double dia, double spacing, int hook_angle,
                              const std::string& tie_type, const Settings& s) {
    double total_hook = 2.0 * s.hook_length_mm(hook_angle, dia);
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
        // Circular tie: π·Di + 2 hooks − 2 bend deductions at hook angle
        double deduct = 2.0 * s.bend_deduction_factor(hook_angle >= 135 ? 135 : 90) * dia;
        r.length_each = std::max(0.0, PI * di + total_hook - deduct);
        r.count = spacing_count(height, spacing);
        return r;
    }
    double b = width - 2 * cover;
    double h = depth - 2 * cover;
    r.length_each = closed_link_cutting(b, h, dia, hook_angle, s);
    r.count = spacing_count(height, spacing);
    return r;
}

/** Push peripheral + intermediate ties for Cl. 26.5.3.2 arrangements. */
static void push_column_ties(std::vector<BarEntry>& entries, const std::string& mark,
                             double width, double depth, double height, double cover,
                             double dia, double spacing, int hook_angle,
                             const std::string& resolved, const Settings& s,
                             std::vector<std::string>& notes) {
    StirrupResult st = column_stirrups(width, depth, height, cover, dia, spacing, hook_angle, resolved, s);
    if (resolved == "Spiral") {
        push_bar(entries, "Column", mark, "Stirrup(Spiral)", dia, st.total_length, 1);
        return;
    }
    if (resolved == "Circular") {
        push_bar(entries, "Column", mark, "Stirrup(Circular)", dia, st.length_each, st.count);
        return;
    }

    double b = width - 2 * cover;
    double h = depth - 2 * cover;
    int sets = st.count;

    push_bar(entries, "Column", mark, "Stirrup", dia, st.length_each, sets);

    if (resolved == "Cross Ties") {
        push_bar(entries, "Column", mark, "Crosstie", dia,
                 hooked_leg_cutting(b, dia, hook_angle, s), sets);
        push_bar(entries, "Column", mark, "Crosstie", dia,
                 hooked_leg_cutting(h, dia, hook_angle, s), sets);
        notes.push_back(mark + ": Cross ties — bar pitch > 75 mm (IS 456 Cl. 26.5.3.2)");
    } else if (resolved == "Diagonal Ties") {
        double diag_clear = 2.0 * std::sqrt(b * b + h * h);
        push_bar(entries, "Column", mark, "DiagonalTie", dia,
                 hooked_leg_cutting(diag_clear, dia, hook_angle, s), sets);
        notes.push_back(mark + ": Diagonal ties — intermediate bars when pitch > 75 mm");
    } else if (resolved == "Open Ties") {
        double open_clear = h + 2.0 * s.hook_length_mm(hook_angle, dia);
        push_bar(entries, "Column", mark, "OpenTie", dia,
                 hooked_leg_cutting(open_clear, dia, hook_angle, s), 2 * sets);
        notes.push_back(mark + ": Alternative open ties for rectangular column");
    } else if (resolved == "U-Ties") {
        double u_clear = h + 0.3 * b;
        push_bar(entries, "Column", mark, "U-Tie", dia,
                 hooked_leg_cutting(u_clear, dia, hook_angle, s), 2 * sets);
        notes.push_back(mark + ": U-ties / intermediate ties for large section");
    } else if (resolved == "Group Ties") {
        double g = 0.35 * (b + h);
        push_bar(entries, "Column", mark, "GroupTie", dia,
                 closed_link_cutting(g, g, dia, hook_angle, s), 4 * sets);
        notes.push_back(mark + ": Individual group ties at corners + peripheral closed");
    } else if (resolved == "Closed") {
        double longer = std::max(b, h);
        int along = std::max(2, 3);
        double pitch = longer / (along - 1);
        if (pitch > 75.0)
            notes.push_back(mark + ": check bar pitch ≤ 75 mm or provide cross/diagonal ties (Cl. 26.5.3.2)");
    }
    notes.push_back(mark + ": stirrups IS 2502 — 2(a+b)+2×hook−bend deductions (hook ≥ "
                    + format_num(s.min_hook_mm) + " mm)");
}
}  // namespace

ColumnResult generate_column_bbs(const std::vector<ColumnInput>& rows, const Settings& s) {
    std::vector<BarEntry> entries;
    std::vector<std::string> notes;
    for (const auto& row : rows) {
        if (!(row.width && row.depth && row.height)) continue;

        int nBars = count_long_bars(row.bars);
        double w = row.width, d = row.depth;
        if (row.column_type == "Square" || row.column_type == "Circular") d = w;
        std::string resolved = resolve_column_tie(row.tie_type, row.column_type, w, d, row.cover,
                                                  row.stirrup_dia, nBars);
        if (row.tie_type == "Auto" || row.tie_type.empty())
            notes.push_back(row.mark + ": " + row.column_type + " · Auto tie → " + resolved);

        push_column_ties(entries, row.mark, w, d, row.height, row.cover,
                         row.stirrup_dia, row.spacing, row.hook_angle, resolved, s, notes);

        for (const auto& kv : row.bars) {
            if (!kv.second) continue;
            double dia = static_cast<double>(kv.first);
            double main_len = row.height + (row.pedestal_h > 0 ? row.pedestal_h : 0);
            push_bar(entries, "Column", row.mark, "Main", dia, main_len, kv.second);
            if (dia > 36.0)
                notes.push_back(row.mark + ": Ø" + format_dia(dia)
                                + " > 36 mm — lap not permitted (IS 456 Cl. 26.2.5); use coupler/weld.");
        }

        // Compression lap splice (opt-in) — IS 456 Cl. 26.2.5
        if (row.provide_lap == "Yes" && nBars > 0) {
            int lapNos = row.lap_nos > 0 ? row.lap_nos : nBars;
            for (const auto& kv : row.bars) {
                if (!kv.second) continue;
                double dia = static_cast<double>(kv.first);
                if (dia > 36.0) continue;
                double lap = s.lap_length(dia, row.concrete_grade, row.steel_grade, "Compression");
                int nos = (row.lap_nos > 0)
                    ? std::min(kv.second, lapNos)
                    : kv.second;
                // Distribute lap_nos across dia groups proportionally when override set
                if (row.lap_nos > 0 && nBars > 0)
                    nos = std::max(1, (int)std::lround(row.lap_nos * (kv.second / (double)nBars)));
                push_bar(entries, "Column", row.mark, "Lap", dia, lap, nos);
                notes.push_back(row.mark + ": compression lap Ø" + format_dia(dia) + " = "
                                + format_num(lap) + " mm (max(Ld_c, 24φ) Cl. 26.2.5)");
            }
        }

        if (row.pedestal_h > 0 && row.pedestal_w > 0 && row.pedestal_d > 0) {
            double pst_dia = row.pedestal_stirrup_dia > 0 ? row.pedestal_stirrup_dia : row.stirrup_dia;
            double pst_sp = row.pedestal_spacing > 0 ? row.pedestal_spacing : row.spacing;
            StirrupResult pst = column_stirrups(row.pedestal_w, row.pedestal_d, row.pedestal_h, row.cover,
                                               pst_dia, pst_sp, row.hook_angle, "Closed", s);
            push_bar(entries, "Column", row.mark, "Pedestal-Stirrup", pst_dia, pst.length_each, pst.count);
            for (const auto& kv : row.pedestal_bars) {
                if (!kv.second) continue;
                push_bar(entries, "Column", row.mark, "Pedestal-Main",
                         static_cast<double>(kv.first), row.pedestal_h, kv.second);
            }
        }

        push_extras_fixed(entries, "Column", row.mark, row.extra_fixed);
    }
    return {entries, summarize(entries), notes};
}

// ============================================================ BEAMS

/** Cutting length for a flexural bar with optional end hooks (IS 456 Cl. 26.2). */
static double flexural_bar_length(double span, double dia, double ld,
                                  const std::string& end_anchorage, const Settings& s) {
    double credit = s.anchorage_credit_mm(end_anchorage, dia);
    double embed = std::max(0.0, ld - credit);
    double len = span + 2.0 * embed;
    if (credit > 0) {
        int ang = (end_anchorage.find("180") != std::string::npos) ? 180 : 90;
        len += 2.0 * s.hook_length_mm(ang, dia);
    }
    return len;
}

BeamResult generate_beam_bbs(const std::vector<BeamInput>& rows, const Settings& s) {
    std::vector<BarEntry> entries;
    std::vector<std::string> notes;
    for (const auto& row : rows) {
        if (!(row.span && row.width && row.depth)) continue;

        double b = row.width - 2 * row.cover;
        double h = row.depth - 2 * row.cover;
        double length_each = closed_link_cutting(b, h, row.stirrup_dia, row.hook_angle, s);

        double effective_depth = row.depth - row.cover;
        double support_zone_len = 2 * effective_depth;
        int count_support_zone = spacing_count(support_zone_len, row.spacing_support);
        double middle_len = row.span - 2 * support_zone_len;
        int count_middle = (middle_len > 0) ? spacing_count(middle_len, row.spacing_middle) : 0;
        int count_s1 = 2 * count_support_zone;
        int total_count = count_s1 + count_middle;

        if (count_s1 > 0)
            push_bar(entries, "Beam", row.mark, "Stirrup-s1", row.stirrup_dia, length_each, count_s1);
        if (count_middle > 0)
            push_bar(entries, "Beam", row.mark, "Stirrup-s2", row.stirrup_dia, length_each, count_middle);

        if (row.legs == 4 && total_count > 0) {
            push_bar(entries, "Beam", row.mark, "Crosstie", row.stirrup_dia,
                     hooked_leg_cutting(h, row.stirrup_dia, row.hook_angle, s), total_count);
        }

        notes.push_back(row.mark + ": stirrups IS 2502 closed link · " + std::to_string(row.hook_angle)
                        + "° hooks · s1/s2 zones ≈ 2d each end");

        for (const auto& kv : row.hanger_bars) {
            if (!kv.second) continue;
            double dia = static_cast<double>(kv.first);
            double ld = s.development_length(dia, row.concrete_grade, row.steel_grade);
            double hanger_len = flexural_bar_length(row.span, dia, ld, row.end_anchorage, s);
            push_bar(entries, "Beam", row.mark, "Hanger", dia, hanger_len, kv.second);
        }

        for (const auto& kv : row.top_bars) {
            if (!kv.second) continue;
            double dia = static_cast<double>(kv.first);
            double ld = s.development_length(dia, row.concrete_grade, row.steel_grade);
            double top_len;
            int physical_count;
            if (row.top_bar_type == "At Support") {
                // Curtailment estimate: Ld + 0.3L each end (no full-span hooks)
                double credit = s.anchorage_credit_mm(row.end_anchorage, dia);
                top_len = std::max(0.0, ld - credit) + 0.3 * row.span;
                if (credit > 0) {
                    int ang = (row.end_anchorage.find("180") != std::string::npos) ? 180 : 90;
                    top_len += s.hook_length_mm(ang, dia);
                }
                physical_count = kv.second * 2;
            } else {
                top_len = flexural_bar_length(row.span, dia, ld, row.end_anchorage, s);
                physical_count = kv.second;
            }
            push_bar(entries, "Beam", row.mark, "TopMain", dia, top_len, physical_count);
        }
        int bottom_total = 0;
        for (const auto& kv : row.bottom_bars) {
            if (!kv.second) continue;
            double dia = static_cast<double>(kv.first);
            double ld = s.development_length(dia, row.concrete_grade, row.steel_grade);
            double bot_len = flexural_bar_length(row.span, dia, ld, row.end_anchorage, s);
            push_bar(entries, "Beam", row.mark, "BottomMain", dia, bot_len, kv.second);
            bottom_total += kv.second;
            notes.push_back(row.mark + ": BottomMain Ø" + format_dia(dia) + " Ld="
                            + format_num(ld) + " mm (IS 456 Cl. 26.2.1)");
        }

        // Optional flexural tension lap
        if (row.provide_lap == "Tension" && bottom_total > 0) {
            int lapNos = row.lap_nos > 0 ? row.lap_nos : bottom_total;
            for (const auto& kv : row.bottom_bars) {
                if (!kv.second) continue;
                double dia = static_cast<double>(kv.first);
                if (dia > 36.0) {
                    notes.push_back(row.mark + ": Ø" + format_dia(dia)
                                    + " > 36 mm — lap not permitted (Cl. 26.2.5)");
                    continue;
                }
                double lap = s.lap_length(dia, row.concrete_grade, row.steel_grade, "Tension");
                int nos = (row.lap_nos > 0)
                    ? std::max(1, (int)std::lround(lapNos * (kv.second / (double)bottom_total)))
                    : kv.second;
                push_bar(entries, "Beam", row.mark, "Lap", dia, lap, nos);
                notes.push_back(row.mark + ": tension lap Ø" + format_dia(dia) + " = "
                                + format_num(lap) + " mm (max(Ld, 30φ) Cl. 26.2.5)");
            }
        }

        if (row.end_anchorage != "Straight Ld" && !row.end_anchorage.empty())
            notes.push_back(row.mark + ": end anchorage = " + row.end_anchorage
                            + " (Cl. 26.2.2 credit applied to straight embedment)");

        push_extras_fixed(entries, "Beam", row.mark, row.extra_fixed);

        for (const auto& e : row.extra_span) {
            if (e.nos <= 0 || e.dia <= 0 || e.frac <= 0) continue;
            push_bar(entries, "Beam", row.mark, "Extra-Span", e.dia, e.frac * row.span, e.nos);
        }

        if (row.skin_dia > 0 && (row.skin_nos > 0 || row.skin_spacing > 0)) {
            double skin_len = row.depth - 2 * row.cover;
            if (skin_len > 0) {
                int per_face = row.skin_nos > 0
                    ? row.skin_nos
                    : spacing_count(std::max(0.0, skin_len), row.skin_spacing);
                int nos = 2 * per_face;
                double along = row.span - 2 * row.cover;
                if (along <= 0) along = row.span;
                push_bar(entries, "Beam", row.mark, "Skin", row.skin_dia, along, nos);
            }
        } else if (row.depth > 750.0) {
            notes.push_back(row.mark + ": overall depth > 750 mm — provide side-face (skin) "
                            "reinforcement per IS 456 Cl. 26.5.1.3 (enter Skin Ø / nos or spacing).");
        }
    }
    return {entries, summarize(entries), notes};
}

// ============================================================ SLABS

// IS 2502 / common BBS: crank allowance ≈ rise × √(1 + 0.42²) per crank.
static double crank_allowance(double rise, int count) {
    if (count <= 0 || rise <= 0) return 0.0;
    return count * rise * std::sqrt(1.0 + 0.42 * 0.42);
}

SlabResult generate_slab_bbs(const std::vector<SlabInput>& rows, const Settings& s) {
    std::vector<BarEntry> entries;
    std::vector<SlabCheck> checks;
    for (const auto& row : rows) {
        if (!(row.span_x && row.span_y && row.thickness)) continue;

        double rise = row.crank_rise > 0 ? row.crank_rise
                                         : std::max(0.0, row.thickness - 2 * row.cover);
        double crank_x = crank_allowance(rise, row.crank_count);

        double len_x = row.span_x + 2 * s.development_length(row.dia_x, row.concrete_grade, row.steel_grade)
                       + crank_x;
        int count_x = spacing_count(row.span_y, row.spacing_x);
        push_bar(entries, "Slab", row.mark, "Main-X", row.dia_x, len_x, count_x);

        double len_y;
        std::string role_y;
        if (row.slab_type == "Two-Way") {
            double crank_y = crank_allowance(rise, row.crank_count);
            len_y = row.span_y + 2 * s.development_length(row.dia_y, row.concrete_grade, row.steel_grade)
                    + crank_y;
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

        // Bottom mesh always covers the full plan.
        footing_mesh(entries, row.mark, row.length_l, row.width_b, row.cover,
                     row.dia_l, row.spacing_l, row.dia_b, row.spacing_b, "Main-L", "Main-B");

        double top_l = row.top_length > 0 ? row.top_length : row.col_dim_l;
        double top_b = row.top_width > 0 ? row.top_width : row.col_dim_b;

        // Stepped: bottom mat; intermediate landings at equal setbacks; vert bars = h + Ld.
        if (type == "Stepped" && row.n_steps >= 1) {
            int n = row.n_steps;
            double sh = row.step_height > 0 ? row.step_height : (row.depth / std::max(1, n));
            if (top_l <= 0) top_l = row.col_dim_l > 0 ? row.col_dim_l : row.length_l * 0.4;
            if (top_b <= 0) top_b = row.col_dim_b > 0 ? row.col_dim_b : row.width_b * 0.4;
            double set_l = (row.length_l - top_l) / (2.0 * n);
            double set_b = (row.width_b - top_b) / (2.0 * n);
            double ld_step_l = s.development_length(row.dia_l, row.concrete_grade, row.steel_grade);
            double ld_step_b = s.development_length(row.dia_b, row.concrete_grade, row.steel_grade);

            for (int i = 1; i < n; ++i) {
                double Li = row.length_l - 2.0 * i * set_l;
                double Bi = row.width_b - 2.0 * i * set_b;
                if (Li > 2 * row.cover && Bi > 2 * row.cover) {
                    footing_mesh(entries, row.mark, Li, Bi, row.cover,
                                 row.dia_l, row.spacing_l, row.dia_b, row.spacing_b,
                                 "Step-L", "Step-B");
                }
            }
            for (int i = 0; i < n; ++i) {
                double Li = row.length_l - 2.0 * i * set_l;
                double Bi = row.width_b - 2.0 * i * set_b;
                if (row.dia_l > 0 && row.spacing_l > 0 && Bi > 0) {
                    int nos = 2 * spacing_count(Bi, row.spacing_l);
                    push_bar(entries, "Footing", row.mark, "Step-Vert-L", row.dia_l, sh + ld_step_l, nos);
                }
                if (row.dia_b > 0 && row.spacing_b > 0 && Li > 0) {
                    int nos = 2 * spacing_count(Li, row.spacing_b);
                    push_bar(entries, "Footing", row.mark, "Step-Vert-B", row.dia_b, sh + ld_step_b, nos);
                }
            }
            if (row.top_dia_l > 0 || row.top_dia_b > 0) {
                footing_mesh(entries, row.mark, top_l, top_b, row.cover,
                             row.top_dia_l, row.top_spacing_l, row.top_dia_b, row.top_spacing_b,
                             "Top-L", "Top-B");
            } else if (top_l > 0 && top_b > 0) {
                footing_mesh(entries, row.mark, top_l, top_b, row.cover,
                             row.dia_l, row.spacing_l, row.dia_b, row.spacing_b,
                             "Top-L", "Top-B");
            }
        } else if (row.top_dia_l > 0 || row.top_dia_b > 0) {
            double mesh_l = (type == "Raft" || type == "Strip") ? row.length_l
                            : (top_l > 0 ? top_l : row.length_l);
            double mesh_b = (type == "Raft" || type == "Strip") ? row.width_b
                            : (top_b > 0 ? top_b : row.width_b);
            footing_mesh(entries, row.mark, mesh_l, mesh_b, row.cover,
                         row.top_dia_l, row.top_spacing_l, row.top_dia_b, row.top_spacing_b,
                         "Top-L", "Top-B");
        }

        push_extras_fixed(entries, "Footing", row.mark, row.extra_fixed);

        double col_l = row.col_dim_l > 0 ? row.col_dim_l : top_l;
        double col_b = row.col_dim_b > 0 ? row.col_dim_b : top_b;
        if (type == "Double" && row.col2_dim_l > 0) {
            col_l = std::max(row.col_dim_l, row.col2_dim_l);
            col_b = std::max(row.col_dim_b, row.col2_dim_b);
        }

        double ld_l = s.development_length(row.dia_l, row.concrete_grade, row.steel_grade);
        double ld_b = s.development_length(row.dia_b, row.concrete_grade, row.steel_grade);
        double avail_l = 0, avail_b = 0;
        std::string anch_l = "N/A", anch_b = "N/A";
        if (col_l > 0 && (type == "Isolated" || type == "Double" || type == "Stepped")) {
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
        if (type == "Stepped")
            c.note = "Stepped — bottom mat; step mats + vert (h+Ld); IS 456 Cl. 34";
        else
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
            double b_clr = std::max(0.0, row.stem_t - 2 * row.cover);
            double h_clr = 100.0;
            double length_each = closed_link_cutting(b_clr, h_clr, row.link_dia, 135, s);
            if (row.link_legs >= 4) length_each += hooked_leg_cutting(h_clr, row.link_dia, 135, s);
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

// ============================================================ STAIRCASE

StairResult generate_stair_bbs(const std::vector<StairInput>& rows, const Settings& s) {
    std::vector<BarEntry> entries;
    std::vector<StairCheck> checks;
    for (const auto& row : rows) {
        if (row.n_risers < 1 || !(row.going && row.riser && row.waist_t && row.flight_width)) continue;
        int flights = std::max(1, row.n_flights);

        // Going count between landings = n_risers − 1; total rise = n_risers × riser.
        double going_total = (row.n_risers - 1) * row.going;
        double rise_total = row.n_risers * row.riser;
        double slope = std::sqrt(going_total * going_total + rise_total * rise_total);
        double land_w = row.landing_width > 0 ? row.landing_width : row.flight_width;

        // Main bars along slope (bottom of waist) — develop into landings with Ld.
        if (row.main_dia > 0 && row.main_spacing > 0) {
            double ld = s.development_length(row.main_dia, row.concrete_grade, row.steel_grade);
            double len = slope + 2 * ld;
            int nos = spacing_count(row.flight_width, row.main_spacing) * flights;
            push_bar(entries, "Stair", row.mark, "Main", row.main_dia, len, nos);
        }

        // Distribution across slope
        if (row.dist_dia > 0 && row.dist_spacing > 0) {
            double len = std::max(0.0, row.flight_width - 2 * row.cover);
            int nos = spacing_count(slope, row.dist_spacing) * flights;
            push_bar(entries, "Stair", row.mark, "Dist", row.dist_dia, len, nos);
        }

        // Landing mesh — two landings per flight (bottom + top), both ways
        if (row.landing_dia > 0 && row.landing_spacing > 0 && row.landing_len > 0) {
            double len_along = std::max(0.0, row.landing_len - 2 * row.cover);
            double len_across = std::max(0.0, land_w - 2 * row.cover);
            int nos_along = spacing_count(land_w, row.landing_spacing);
            int nos_across = spacing_count(row.landing_len, row.landing_spacing);
            int landings = 2 * flights;
            push_bar(entries, "Stair", row.mark, "Landing-L", row.landing_dia, len_along,
                     nos_along * landings);
            push_bar(entries, "Stair", row.mark, "Landing-B", row.landing_dia, len_across,
                     nos_across * landings);
        }

        push_extras_fixed(entries, "Stair", row.mark, row.extra_fixed);

        StairCheck c;
        c.mark = row.mark;
        c.slope_len = round2(slope);
        c.rise_total = round2(rise_total);
        c.going_total = round2(going_total);
        c.ast_main = round2(ast_provided(row.main_dia, row.main_spacing));
        c.ast_min = round2(ast_min_val(row.waist_t, row.steel_grade, s));
        c.status_main = (row.main_dia <= 0) ? "N/A"
                        : (c.ast_main >= c.ast_min ? "OK" : "Increase main steel / reduce spacing");
        c.note = "Quantity estimate — waist slope √(((n−1)·going)² + (n·riser)²); verify on drawing.";
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
