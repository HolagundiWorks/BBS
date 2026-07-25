// Engine.h — BBS calculation engine (columns, beams, slabs, footings, walls, stairs).
#pragma once

#include "Model.h"

namespace bbs {

double round2(double x);

std::string format_dia(double x);
std::string format_num(double x, int max_decimals = 2);

std::vector<SummaryRow> summarize(const std::vector<BarEntry>& entries);
std::vector<SummaryRow> merge_summaries(const std::vector<std::vector<SummaryRow>>& lists);

ColumnResult  generate_column_bbs(const std::vector<ColumnInput>& rows, const Settings& s);
BeamResult    generate_beam_bbs(const std::vector<BeamInput>& rows, const Settings& s);
SlabResult    generate_slab_bbs(const std::vector<SlabInput>& rows, const Settings& s);
FootingResult generate_footing_bbs(const std::vector<FootingInput>& rows, const Settings& s);
WallResult    generate_wall_bbs(const std::vector<WallInput>& rows, const Settings& s);
StairResult   generate_stair_bbs(const std::vector<StairInput>& rows, const Settings& s);

}  // namespace bbs
