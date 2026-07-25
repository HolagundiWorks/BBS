// Project.h — save/load a whole BBS project (settings + all input rows) to a
// single human-readable .bbsproj (JSON) file.
#pragma once

#include "Model.h"
#include <string>
#include <vector>

namespace bbs {

struct ProjectData {
    std::string name = "Untitled Project";
    Settings settings;
    std::vector<RawRow> columns, beams, slabs, footings, walls;
};

bool save_project(const std::wstring& path, const ProjectData& data, std::string& err);
bool load_project(const std::wstring& path, ProjectData& out, std::string& err);

// UTF-8 text file helpers (used by exporters too).
bool write_text_file(const std::wstring& path, const std::string& utf8, std::string& err);
bool read_text_file(const std::wstring& path, std::string& out, std::string& err);

}  // namespace bbs
