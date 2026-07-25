// Project.cpp — project persistence + shared file I/O helpers.
#include "Project.h"

#include "Engine.h"
#include "Json.h"

#include <cstdio>
#include <fstream>

namespace bbs {

bool write_text_file(const std::wstring& path, const std::string& utf8, std::string& err) {
    std::ofstream f(path, std::ios::binary);  // MSVC accepts wide paths
    if (!f) { err = "Could not open file for writing."; return false; }
    f.write(utf8.data(), static_cast<std::streamsize>(utf8.size()));
    if (!f) { err = "Write failed."; return false; }
    return true;
}

bool read_text_file(const std::wstring& path, std::string& out, std::string& err) {
    std::ifstream f(path, std::ios::binary);
    if (!f) { err = "Could not open file for reading."; return false; }
    out.assign((std::istreambuf_iterator<char>(f)), std::istreambuf_iterator<char>());
    return true;
}

// ------------------------------ settings <-> json ------------------------------

static JsonValue settings_to_json(const Settings& s) {
    JsonValue o = JsonValue::Object();
    JsonValue dias = JsonValue::Array();
    for (int d : s.diameters) dias.arr.push_back(JsonValue::Num(d));
    o.set("diameters", dias);

    JsonValue hooks = JsonValue::Object();
    for (const auto& kv : s.hook_allowance) hooks.set(std::to_string(kv.first), JsonValue::Num(kv.second));
    o.set("hook_allowance", hooks);

    JsonValue tau = JsonValue::Object();
    for (const auto& kv : s.tau_bd) tau.set(kv.first, JsonValue::Num(kv.second));
    o.set("tau_bd", tau);

    JsonValue fy = JsonValue::Object();
    for (const auto& kv : s.fy) fy.set(kv.first, JsonValue::Num(kv.second));
    o.set("fy", fy);
    return o;
}

static void settings_from_json(const JsonValue* o, Settings& s) {
    if (!o || !o->isObject()) return;
    if (const JsonValue* d = o->find("diameters"); d && d->isArray()) {
        s.diameters.clear();
        for (const auto& v : d->arr) s.diameters.push_back(static_cast<int>(v.asNumber()));
    }
    if (const JsonValue* h = o->find("hook_allowance"); h && h->isObject()) {
        s.hook_allowance.clear();
        for (const auto& kv : h->obj) s.hook_allowance[std::atoi(kv.first.c_str())] = kv.second.asNumber();
    }
    if (const JsonValue* t = o->find("tau_bd"); t && t->isObject()) {
        s.tau_bd.clear();
        for (const auto& kv : t->obj) s.tau_bd[kv.first] = kv.second.asNumber();
    }
    if (const JsonValue* f = o->find("fy"); f && f->isObject()) {
        s.fy.clear();
        for (const auto& kv : f->obj) s.fy[kv.first] = kv.second.asNumber();
    }
}

static JsonValue rows_to_json(const std::vector<RawRow>& rows) {
    JsonValue arr = JsonValue::Array();
    for (const auto& row : rows) {
        JsonValue o = JsonValue::Object();
        for (const auto& kv : row) o.set(kv.first, JsonValue::Str(kv.second));
        arr.arr.push_back(o);
    }
    return arr;
}

static std::vector<RawRow> rows_from_json(const JsonValue* arr) {
    std::vector<RawRow> rows;
    if (!arr || !arr->isArray()) return rows;
    for (const auto& item : arr->arr) {
        if (!item.isObject()) continue;
        RawRow row;
        for (const auto& kv : item.obj) {
            if (kv.second.type == JsonValue::Type::String) row[kv.first] = kv.second.str;
            else if (kv.second.type == JsonValue::Type::Number) row[kv.first] = format_num(kv.second.num, 6);
        }
        rows.push_back(row);
    }
    return rows;
}

// ------------------------------ save / load ------------------------------

bool save_project(const std::wstring& path, const ProjectData& data, std::string& err) {
    JsonValue root = JsonValue::Object();
    root.set("format", JsonValue::Str("bbsproj"));
    root.set("version", JsonValue::Num(2));
    root.set("name", JsonValue::Str(data.name));
    root.set("settings", settings_to_json(data.settings));
    root.set("columns", rows_to_json(data.columns));
    root.set("beams", rows_to_json(data.beams));
    root.set("slabs", rows_to_json(data.slabs));
    root.set("footings", rows_to_json(data.footings));
    root.set("walls", rows_to_json(data.walls));
    return write_text_file(path, json_dump(root), err);
}

bool load_project(const std::wstring& path, ProjectData& out, std::string& err) {
    std::string text;
    if (!read_text_file(path, text, err)) return false;
    JsonValue root;
    if (!json_parse(text, root, err)) return false;
    if (!root.isObject() || !root.find("settings")) {
        err = "This file doesn't look like a BBS project (.bbsproj).";
        return false;
    }
    if (const JsonValue* n = root.find("name"); n && n->type == JsonValue::Type::String)
        out.name = n->str;
    settings_from_json(root.find("settings"), out.settings);
    out.columns  = rows_from_json(root.find("columns"));
    out.beams    = rows_from_json(root.find("beams"));
    out.slabs    = rows_from_json(root.find("slabs"));
    out.footings = rows_from_json(root.find("footings"));
    out.walls    = rows_from_json(root.find("walls"));  // absent in v1 → empty
    return true;
}

}  // namespace bbs
