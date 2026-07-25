// bbs_c_api.h — C ABI for C# / P/Invoke (UTF-8 JSON).
#pragma once

#ifdef _WIN32
#  ifdef BBS_ENGINE_EXPORTS
#    define BBS_API __declspec(dllexport)
#  else
#    define BBS_API __declspec(dllimport)
#  endif
#else
#  define BBS_API
#endif

#ifdef __cplusplus
extern "C" {
#endif

// Free a string returned by any bbs_* that allocates into *out.
BBS_API void bbs_free(char* p);

// kind: "columns" | "beams" | "slabs" | "footings" | "walls" | "stairs"
// settings_json / rows_json: UTF-8 JSON (settings object; array of row objects)
// Returns 1 on success, 0 on failure (*out_json always set; caller frees).
BBS_API int bbs_generate(const char* kind, const char* settings_json, const char* rows_json,
                         char** out_json);

// Load/save .bbsproj — paths are UTF-16 (Windows). *out_json is UTF-8 project JSON.
BBS_API int bbs_load_project(const wchar_t* path, char** out_json);
BBS_API int bbs_save_project(const wchar_t* path, const char* project_json, char** out_error);

// Export CSV table: headers_json = ["a","b"], rows_json = [["1","2"],...]
BBS_API int bbs_export_csv(const wchar_t* path, const char* headers_json, const char* rows_json,
                           char** out_error);

// Build + write HTML report from full project JSON.
BBS_API int bbs_export_html(const wchar_t* path, const char* project_json, char** out_error);

#ifdef __cplusplus
}
#endif
