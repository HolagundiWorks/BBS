// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

// Export.cpp — CSV + HTML report generation.
#include "Export.h"

#include "Engine.h"
#include "Project.h"  // write_text_file

#include <ctime>

namespace bbs {

static std::string csv_cell(const std::string& s) {
    bool needs_quote = s.find_first_of(",\"\n\r") != std::string::npos;
    if (!needs_quote) return s;
    std::string out = "\"";
    for (char c : s) { if (c == '"') out += "\"\""; else out.push_back(c); }
    out += "\"";
    return out;
}

static std::string join_csv(const std::vector<std::string>& cells) {
    std::string line;
    for (size_t i = 0; i < cells.size(); ++i) {
        if (i) line += ",";
        line += csv_cell(cells[i]);
    }
    line += "\r\n";
    return line;
}

bool export_bbs_csv(const std::vector<BarEntry>& entries, const std::wstring& path, std::string& err) {
    std::string out = join_csv({"Element Type", "Mark", "Bar Role", "Diameter (mm)", "Cutting Length (mm)", "Nos"});
    for (const auto& e : entries)
        out += join_csv({e.element_type, e.mark, e.bar_role, format_dia(e.dia), format_num(e.length_mm),
                         std::to_string(e.nos)});
    return write_text_file(path, out, err);
}

bool export_summary_csv(const std::vector<SummaryRow>& summary, const std::wstring& path, std::string& err) {
    std::string out = join_csv({"Dia (mm)", "Nos", "Total Length (m)", "Weight (kg)"});
    for (const auto& r : summary)
        out += join_csv({r.dia, std::to_string(r.nos), format_num(r.total_length_m), format_num(r.weight_kg)});
    return write_text_file(path, out, err);
}

bool export_table_csv(const std::vector<std::string>& headers,
                      const std::vector<std::vector<std::string>>& rows,
                      const std::wstring& path, std::string& err) {
    std::string out = join_csv(headers);
    for (const auto& r : rows) out += join_csv(r);
    return write_text_file(path, out, err);
}

// ------------------------------ HTML report ------------------------------

static std::string html_escape(const std::string& s) {
    std::string out;
    for (char c : s) {
        switch (c) {
            case '&': out += "&amp;"; break;
            case '<': out += "&lt;"; break;
            case '>': out += "&gt;"; break;
            case '"': out += "&quot;"; break;
            default: out.push_back(c);
        }
    }
    return out;
}

static bool is_status_bad(const std::string& s) {
    return s.find("Insufficient") != std::string::npos || s.find("Increase") != std::string::npos;
}

bool export_html_report(const std::string& project_name,
                        const std::vector<ReportSection>& sections,
                        const std::wstring& path, std::string& err) {
    std::time_t t = std::time(nullptr);
    char datebuf[64];
    std::strftime(datebuf, sizeof(datebuf), "%Y-%m-%d %H:%M", std::localtime(&t));

    std::string h;
    h += "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">";
    h += "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">";
    h += "<title>BBS Report - " + html_escape(project_name) + "</title><style>";
    h += ":root{--accent:#0f6cbd;--ink:#1a1a1a;--muted:#4a4a4a;--line:#d0d0d0;--bg:#f5f5f5;--card:#fff;}";
    h += "*{box-sizing:border-box}body{margin:0;font-family:'Segoe UI Variable Text','Segoe UI',system-ui,sans-serif;";
    h += "color:var(--ink);background:var(--bg);padding:32px;line-height:1.5}";
    h += ".wrap{max-width:1040px;margin:0 auto}";
    h += "header{border-bottom:2px solid var(--accent);padding-bottom:16px;margin-bottom:24px}";
    h += "h1{font-family:'Segoe UI Variable Display','Segoe UI',sans-serif;font-size:28px;margin:0 0 4px;font-weight:600}";
    h += ".sub{color:var(--muted);font-size:14px}";
    h += "section{background:var(--card);border:1px solid var(--line);border-radius:8px;padding:20px 24px;margin-bottom:20px}";
    h += "h2{font-size:18px;margin:0 0 12px;font-weight:600}";
    h += "table{width:100%;border-collapse:collapse;font-size:13px}";
    h += "th{text-align:left;background:#ececec;color:var(--ink);font-weight:600;padding:8px 10px;border-bottom:1px solid var(--line)}";
    h += "td{padding:7px 10px;border-bottom:1px solid var(--line)}";
    h += "tr:last-child td{border-bottom:none}";
    h += "tr.total td{font-weight:700;background:#f0f0f0}";
    h += ".ok{color:#0a5c2e;font-weight:600}.bad{color:#8b0a14;font-weight:600}";
    h += ".note{color:var(--muted);font-size:13px;margin-top:8px}";
    h += "footer{color:var(--muted);font-size:13px;text-align:center;margin-top:24px}";
    h += "@media print{body{background:#fff;padding:12px}section{box-shadow:none}}";
    h += "</style></head><body><div class=\"wrap\">";
    h += "<header><h1>Bar Bending Schedule</h1><p class=\"sub\">Project: <strong>" +
         html_escape(project_name) + "</strong> · Generated " + datebuf + "</p></header>";
    h += "<main>";

    for (const auto& sec : sections) {
        h += "<section><h2>" + html_escape(sec.title) + "</h2>";
        if (sec.rows.empty()) {
            h += "<p class=\"note\">No data generated for this section.</p>";
        } else {
            h += "<table><caption class=\"note\" style=\"caption-side:top;text-align:left;margin-bottom:6px\">" +
                 html_escape(sec.title) + "</caption><thead><tr>";
            for (const auto& col : sec.headers) h += "<th scope=\"col\">" + html_escape(col) + "</th>";
            h += "</tr></thead><tbody>";
            for (const auto& row : sec.rows) {
                bool total = !row.empty() && row[0] == "TOTAL";
                h += total ? "<tr class=\"total\">" : "<tr>";
                for (const auto& cell : row) {
                    if (cell == "OK")
                        h += "<td><span class=\"ok\">OK</span></td>";
                    else if (is_status_bad(cell))
                        h += "<td><span class=\"bad\">" + html_escape(cell) + "</span></td>";
                    else
                        h += "<td>" + html_escape(cell) + "</td>";
                }
                h += "</tr>";
            }
            h += "</tbody></table>";
        }
        if (!sec.note.empty()) h += "<p class=\"note\">" + html_escape(sec.note) + "</p>";
        h += "</section>";
    }
    h += "</main>";

    h += "<footer>Estimation tool (IS 456-derived). Cross-check against structural drawings before construction.</footer>";
    h += "</div></body></html>";
    return write_text_file(path, h, err);
}

}  // namespace bbs
