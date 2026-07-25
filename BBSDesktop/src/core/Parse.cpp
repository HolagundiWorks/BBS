// Parse.cpp — raw-text parsing helpers.
#include "Parse.h"

#include "Engine.h"
#include <cctype>
#include <cstdlib>
#include <vector>

namespace bbs {

static std::string trim(const std::string& s) {
    size_t a = 0, b = s.size();
    while (a < b && std::isspace((unsigned char)s[a])) ++a;
    while (b > a && std::isspace((unsigned char)s[b - 1])) --b;
    return s.substr(a, b - a);
}

static std::vector<std::string> split_list(const std::string& text) {
    std::vector<std::string> out;
    std::string cur;
    for (char ch : text) {
        if (ch == ',' || ch == ';' || ch == '\n') {
            auto t = trim(cur);
            if (!t.empty()) out.push_back(t);
            cur.clear();
        } else {
            cur.push_back(ch);
        }
    }
    auto t = trim(cur);
    if (!t.empty()) out.push_back(t);
    return out;
}

static std::vector<std::string> split_parts(const std::string& token) {
    std::vector<std::string> parts;
    std::string cur;
    for (char ch : token) {
        if (ch == ':' || ch == 'x' || ch == 'X' || ch == '@') {
            parts.push_back(trim(cur));
            cur.clear();
        } else {
            cur.push_back(ch);
        }
    }
    parts.push_back(trim(cur));
    return parts;
}

std::string to_str(const RawRow& row, const std::string& key, const std::string& def) {
    auto it = row.find(key);
    return it == row.end() ? def : it->second;
}

double to_float(const RawRow& row, const std::string& key, double def) {
    auto it = row.find(key);
    if (it == row.end()) return def;
    std::string s = trim(it->second);
    if (s.empty()) return def;
    char* end = nullptr;
    double v = std::strtod(s.c_str(), &end);
    return end == s.c_str() ? def : v;
}

int to_int(const RawRow& row, const std::string& key, int def) {
    double v = to_float(row, key, (double)def);
    return (int)v;
}

std::map<int, int> parse_bars(const std::string& text) {
    std::map<int, int> bars;
    for (const auto& token : split_list(text)) {
        auto parts = split_parts(token);
        if (parts.size() < 2) continue;
        int dia = std::atoi(parts[0].c_str());
        int qty = std::atoi(parts[1].c_str());
        if (dia > 0 && qty > 0) bars[dia] += qty;
    }
    return bars;
}

std::string bars_to_text(const std::map<int, int>& bars) {
    std::string out;
    for (const auto& kv : bars) {
        if (!out.empty()) out += ", ";
        out += std::to_string(kv.first) + ":" + std::to_string(kv.second);
    }
    return out;
}

std::vector<ExtraFixed> parse_extra_fixed(const std::string& text) {
    std::vector<ExtraFixed> out;
    for (const auto& token : split_list(text)) {
        auto p = split_parts(token);
        if (p.size() < 3) continue;
        ExtraFixed e;
        e.dia = std::atof(p[0].c_str());
        e.nos = std::atoi(p[1].c_str());
        e.length_mm = std::atof(p[2].c_str());
        if (e.dia > 0 && e.nos > 0 && e.length_mm > 0) out.push_back(e);
    }
    return out;
}

std::vector<ExtraSpan> parse_extra_span(const std::string& text) {
    std::vector<ExtraSpan> out;
    for (const auto& token : split_list(text)) {
        auto p = split_parts(token);
        if (p.size() < 3) continue;
        ExtraSpan e;
        e.dia = std::atof(p[0].c_str());
        e.nos = std::atoi(p[1].c_str());
        e.frac = std::atof(p[2].c_str());
        if (e.dia > 0 && e.nos > 0 && e.frac > 0) out.push_back(e);
    }
    return out;
}

std::vector<ExtraMesh> parse_extra_mesh(const std::string& text) {
    std::vector<ExtraMesh> out;
    for (const auto& token : split_list(text)) {
        auto p = split_parts(token);
        if (p.size() < 3) continue;
        ExtraMesh e;
        e.dia = std::atof(p[0].c_str());
        e.length_mm = std::atof(p[1].c_str());
        e.spacing = std::atof(p[2].c_str());
        if (e.dia > 0 && e.length_mm > 0 && e.spacing > 0) out.push_back(e);
    }
    return out;
}

}  // namespace bbs
