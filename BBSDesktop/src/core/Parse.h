// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

// Parse.h — helpers to turn raw UI text into typed numbers and bar lists.
#pragma once

#include "Model.h"
#include <map>
#include <string>
#include <vector>

namespace bbs {

double to_float(const RawRow& row, const std::string& key, double def = 0.0);
int to_int(const RawRow& row, const std::string& key, int def = 0);
std::string to_str(const RawRow& row, const std::string& key, const std::string& def = "");

// Parse "dia:qty, dia:qty" (also tolerates 'x' or '-' separators and spaces).
std::map<int, int> parse_bars(const std::string& text);
std::string bars_to_text(const std::map<int, int>& bars);

// "dia:nos:length, ..."  e.g. "16:2:2500, 12:4:1800"
std::vector<ExtraFixed> parse_extra_fixed(const std::string& text);
// "dia:nos:frac, ..."    e.g. "16:2:0.3"
std::vector<ExtraSpan> parse_extra_span(const std::string& text);
// "dia:length:spacing, ..." e.g. "12:3000:150"
std::vector<ExtraMesh> parse_extra_mesh(const std::string& text);

}  // namespace bbs
