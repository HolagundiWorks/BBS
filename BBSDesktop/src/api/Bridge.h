// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

// Bridge.h — RawRow JSON <-> engine (UTF-8, no UI dependency).
#pragma once

#include "../core/Export.h"
#include "../core/Model.h"
#include "../core/Project.h"
#include <string>
#include <vector>

namespace bbs {

struct GenTable {
    std::vector<std::string> headers;
    std::vector<std::vector<std::string>> rows;
};

struct BridgeResult {
    GenTable bbs;
    GenTable summary;
    GenTable checks;
    std::vector<SummaryRow> summaryTyped;
    std::string error;
};

Settings settings_from_json_text(const std::string& json, std::string& err);
std::vector<RawRow> rows_from_json_text(const std::string& json, std::string& err);
std::string project_to_json_text(const ProjectData& p);
bool project_from_json_text(const std::string& json, ProjectData& out, std::string& err);

BridgeResult generate_kind(const std::string& kind, const Settings& s, const std::vector<RawRow>& rows);
std::vector<ReportSection> build_report_sections(const ProjectData& p);

std::string bridge_result_to_json(const BridgeResult& r);

}  // namespace bbs
