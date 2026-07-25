// Compute.h — the RawRow -> engine glue, shared by the element pages and the
// HTML report so the conversion logic lives in exactly one place.
#pragma once

#include "../core/Export.h"
#include "ElementPage.h"

namespace ui {

GenResult computeColumns(const std::vector<bbs::RawRow>& rows, const bbs::Settings& s);
GenResult computeBeams(const std::vector<bbs::RawRow>& rows, const bbs::Settings& s);
GenResult computeSlabs(const std::vector<bbs::RawRow>& rows, const bbs::Settings& s);
GenResult computeFootings(const std::vector<bbs::RawRow>& rows, const bbs::Settings& s);
GenResult computeWalls(const std::vector<bbs::RawRow>& rows, const bbs::Settings& s);

// Build the full HTML report sections from a whole project.
std::vector<bbs::ReportSection> buildReportSections(const bbs::ProjectData& p);

}  // namespace ui
