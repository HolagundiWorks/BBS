// Json.cpp — implementation of the tiny JSON reader/writer.
#include "Json.h"

#include <cmath>
#include <cstdio>
#include <sstream>

namespace bbs {

// ------------------------------ serialize ------------------------------

static void dump_string(std::string& out, const std::string& s) {
    out.push_back('"');
    for (char c : s) {
        switch (c) {
            case '"':  out += "\\\""; break;
            case '\\': out += "\\\\"; break;
            case '\n': out += "\\n"; break;
            case '\r': out += "\\r"; break;
            case '\t': out += "\\t"; break;
            default:
                if (static_cast<unsigned char>(c) < 0x20) {
                    char buf[8];
                    std::snprintf(buf, sizeof(buf), "\\u%04x", c);
                    out += buf;
                } else {
                    out.push_back(c);  // UTF-8 bytes pass through unchanged
                }
        }
    }
    out.push_back('"');
}

static void dump_number(std::string& out, double n) {
    if (std::floor(n) == n && std::fabs(n) < 1e15) {
        char buf[32];
        std::snprintf(buf, sizeof(buf), "%lld", static_cast<long long>(n));
        out += buf;
    } else {
        char buf[64];
        std::snprintf(buf, sizeof(buf), "%.10g", n);
        out += buf;
    }
}

static void dump_value(std::string& out, const JsonValue& v, int indent, int depth) {
    const std::string pad(static_cast<size_t>(indent) * (depth + 1), ' ');
    const std::string pad_close(static_cast<size_t>(indent) * depth, ' ');
    switch (v.type) {
        case JsonValue::Type::Null:   out += "null"; break;
        case JsonValue::Type::Bool:   out += v.bval ? "true" : "false"; break;
        case JsonValue::Type::Number: dump_number(out, v.num); break;
        case JsonValue::Type::String: dump_string(out, v.str); break;
        case JsonValue::Type::Array:
            if (v.arr.empty()) { out += "[]"; break; }
            out += "[\n";
            for (size_t i = 0; i < v.arr.size(); ++i) {
                out += pad;
                dump_value(out, v.arr[i], indent, depth + 1);
                if (i + 1 < v.arr.size()) out += ",";
                out += "\n";
            }
            out += pad_close; out += "]";
            break;
        case JsonValue::Type::Object:
            if (v.obj.empty()) { out += "{}"; break; }
            out += "{\n";
            for (size_t i = 0; i < v.obj.size(); ++i) {
                out += pad;
                dump_string(out, v.obj[i].first);
                out += ": ";
                dump_value(out, v.obj[i].second, indent, depth + 1);
                if (i + 1 < v.obj.size()) out += ",";
                out += "\n";
            }
            out += pad_close; out += "}";
            break;
    }
}

std::string json_dump(const JsonValue& v, int indent) {
    std::string out;
    dump_value(out, v, indent, 0);
    return out;
}

// ------------------------------ parse ------------------------------

namespace {
struct Parser {
    const std::string& s;
    size_t i = 0;
    std::string err;

    explicit Parser(const std::string& text) : s(text) {}

    void skip_ws() {
        while (i < s.size() && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) ++i;
    }
    bool fail(const std::string& m) { if (err.empty()) err = m; return false; }

    bool parse_value(JsonValue& out) {
        skip_ws();
        if (i >= s.size()) return fail("unexpected end of input");
        char c = s[i];
        if (c == '{') return parse_object(out);
        if (c == '[') return parse_array(out);
        if (c == '"') { out.type = JsonValue::Type::String; return parse_string(out.str); }
        if (c == 't' || c == 'f') return parse_bool(out);
        if (c == 'n') return parse_null(out);
        return parse_number(out);
    }

    bool parse_object(JsonValue& out) {
        out = JsonValue::Object();
        ++i;  // {
        skip_ws();
        if (i < s.size() && s[i] == '}') { ++i; return true; }
        while (true) {
            skip_ws();
            if (i >= s.size() || s[i] != '"') return fail("expected string key");
            std::string key;
            if (!parse_string(key)) return false;
            skip_ws();
            if (i >= s.size() || s[i] != ':') return fail("expected ':'");
            ++i;
            JsonValue val;
            if (!parse_value(val)) return false;
            out.obj.emplace_back(std::move(key), std::move(val));
            skip_ws();
            if (i >= s.size()) return fail("unterminated object");
            if (s[i] == ',') { ++i; continue; }
            if (s[i] == '}') { ++i; return true; }
            return fail("expected ',' or '}'");
        }
    }

    bool parse_array(JsonValue& out) {
        out = JsonValue::Array();
        ++i;  // [
        skip_ws();
        if (i < s.size() && s[i] == ']') { ++i; return true; }
        while (true) {
            JsonValue val;
            if (!parse_value(val)) return false;
            out.arr.push_back(std::move(val));
            skip_ws();
            if (i >= s.size()) return fail("unterminated array");
            if (s[i] == ',') { ++i; continue; }
            if (s[i] == ']') { ++i; return true; }
            return fail("expected ',' or ']'");
        }
    }

    bool parse_string(std::string& out) {
        ++i;  // opening quote
        while (i < s.size()) {
            char c = s[i++];
            if (c == '"') return true;
            if (c == '\\') {
                if (i >= s.size()) return fail("bad escape");
                char e = s[i++];
                switch (e) {
                    case '"': out.push_back('"'); break;
                    case '\\': out.push_back('\\'); break;
                    case '/': out.push_back('/'); break;
                    case 'n': out.push_back('\n'); break;
                    case 'r': out.push_back('\r'); break;
                    case 't': out.push_back('\t'); break;
                    case 'b': out.push_back('\b'); break;
                    case 'f': out.push_back('\f'); break;
                    case 'u': {
                        if (i + 4 > s.size()) return fail("bad \\u");
                        unsigned cp = 0;
                        for (int k = 0; k < 4; ++k) {
                            char h = s[i++];
                            cp <<= 4;
                            if (h >= '0' && h <= '9') cp |= (h - '0');
                            else if (h >= 'a' && h <= 'f') cp |= (h - 'a' + 10);
                            else if (h >= 'A' && h <= 'F') cp |= (h - 'A' + 10);
                            else return fail("bad hex in \\u");
                        }
                        // encode BMP code point as UTF-8 (surrogate pairs not needed here)
                        if (cp < 0x80) {
                            out.push_back(static_cast<char>(cp));
                        } else if (cp < 0x800) {
                            out.push_back(static_cast<char>(0xC0 | (cp >> 6)));
                            out.push_back(static_cast<char>(0x80 | (cp & 0x3F)));
                        } else {
                            out.push_back(static_cast<char>(0xE0 | (cp >> 12)));
                            out.push_back(static_cast<char>(0x80 | ((cp >> 6) & 0x3F)));
                            out.push_back(static_cast<char>(0x80 | (cp & 0x3F)));
                        }
                        break;
                    }
                    default: return fail("unknown escape");
                }
            } else {
                out.push_back(c);
            }
        }
        return fail("unterminated string");
    }

    bool parse_number(JsonValue& out) {
        size_t start = i;
        if (i < s.size() && (s[i] == '-' || s[i] == '+')) ++i;
        while (i < s.size() &&
               ((s[i] >= '0' && s[i] <= '9') || s[i] == '.' || s[i] == 'e' || s[i] == 'E' ||
                s[i] == '+' || s[i] == '-'))
            ++i;
        if (i == start) return fail("invalid number");
        out.type = JsonValue::Type::Number;
        out.num = std::atof(s.substr(start, i - start).c_str());
        return true;
    }

    bool parse_bool(JsonValue& out) {
        if (s.compare(i, 4, "true") == 0) { out = JsonValue::Bool(true); i += 4; return true; }
        if (s.compare(i, 5, "false") == 0) { out = JsonValue::Bool(false); i += 5; return true; }
        return fail("invalid literal");
    }
    bool parse_null(JsonValue& out) {
        if (s.compare(i, 4, "null") == 0) { out = JsonValue(); i += 4; return true; }
        return fail("invalid literal");
    }
};
}  // namespace

bool json_parse(const std::string& text, JsonValue& out, std::string& err) {
    Parser p(text);
    if (!p.parse_value(out)) { err = p.err; return false; }
    p.skip_ws();
    if (p.i != text.size()) { err = "trailing characters after JSON value"; return false; }
    return true;
}

}  // namespace bbs
