// Json.h — tiny, dependency-free JSON reader/writer.
// Just enough to persist a BBS project as a human-readable .bbsproj file.
#pragma once

#include <string>
#include <utility>
#include <vector>

namespace bbs {

struct JsonValue {
    enum class Type { Null, Bool, Number, String, Array, Object };
    Type type = Type::Null;
    bool bval = false;
    double num = 0.0;
    std::string str;
    std::vector<JsonValue> arr;
    std::vector<std::pair<std::string, JsonValue>> obj;

    static JsonValue Object() { JsonValue v; v.type = Type::Object; return v; }
    static JsonValue Array()  { JsonValue v; v.type = Type::Array;  return v; }
    static JsonValue Str(const std::string& s) { JsonValue v; v.type = Type::String; v.str = s; return v; }
    static JsonValue Num(double n) { JsonValue v; v.type = Type::Number; v.num = n; return v; }
    static JsonValue Bool(bool b)  { JsonValue v; v.type = Type::Bool; v.bval = b; return v; }

    bool isObject() const { return type == Type::Object; }
    bool isArray()  const { return type == Type::Array; }

    const JsonValue* find(const std::string& key) const {
        if (type != Type::Object) return nullptr;
        for (const auto& kv : obj)
            if (kv.first == key) return &kv.second;
        return nullptr;
    }
    void set(const std::string& key, JsonValue v) {
        for (auto& kv : obj)
            if (kv.first == key) { kv.second = std::move(v); return; }
        obj.emplace_back(key, std::move(v));
    }
    std::string asString() const { return type == Type::String ? str : std::string(); }
    double asNumber() const { return type == Type::Number ? num : 0.0; }
};

std::string json_dump(const JsonValue& v, int indent = 2);
bool json_parse(const std::string& text, JsonValue& out, std::string& err);

}  // namespace bbs
