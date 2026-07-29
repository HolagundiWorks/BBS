// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

// Export.h — CSV exporters plus a client-ready HTML report that combines
// every generated schedule table.
#pragma once

#include "Model.h"
#include <string>
#include <vector>

namespace bbs {

bool export_bbs_csv(const std::vector<BarEntry>& entries, const std::wstring& path, std::string& err);
bool export_summary_csv(const std::vector<SummaryRow>& summary, const std::wstring& path, std::string& err);

// Generic table CSV (used for the slab/footing check tables).
bool export_table_csv(const std::vector<std::string>& headers,
                      const std::vector<std::vector<std::string>>& rows,
                      const std::wstring& path, std::string& err);

// A section of the HTML report: a titled table.
struct ReportSection {
    std::string title;
    std::vector<std::string> headers;
    std::vector<std::vector<std::string>> rows;
    std::string note;  // optional caption under the table
};

bool export_html_report(const std::string& project_name,
                        const std::vector<ReportSection>& sections,
                        const std::wstring& path, std::string& err);

}  // namespace bbs
