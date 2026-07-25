// Dialogs.cpp — common dialog wrappers.
#include "Dialogs.h"

#include <commdlg.h>

#pragma comment(lib, "comdlg32.lib")

namespace ui {

std::wstring saveFileDialog(HWND owner, const wchar_t* filter, const wchar_t* defExt,
                            const std::wstring& suggestedName) {
    wchar_t buf[MAX_PATH] = L"";
    wcsncpy_s(buf, suggestedName.c_str(), _TRUNCATE);
    OPENFILENAMEW ofn{};
    ofn.lStructSize = sizeof(ofn);
    ofn.hwndOwner = owner;
    ofn.lpstrFilter = filter;
    ofn.lpstrFile = buf;
    ofn.nMaxFile = MAX_PATH;
    ofn.lpstrDefExt = defExt;
    ofn.Flags = OFN_OVERWRITEPROMPT | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR;
    if (GetSaveFileNameW(&ofn)) return buf;
    return L"";
}

std::wstring openFileDialog(HWND owner, const wchar_t* filter) {
    wchar_t buf[MAX_PATH] = L"";
    OPENFILENAMEW ofn{};
    ofn.lStructSize = sizeof(ofn);
    ofn.hwndOwner = owner;
    ofn.lpstrFilter = filter;
    ofn.lpstrFile = buf;
    ofn.nMaxFile = MAX_PATH;
    ofn.Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR;
    if (GetOpenFileNameW(&ofn)) return buf;
    return L"";
}

void infoBox(HWND owner, const std::wstring& text, const std::wstring& title) {
    MessageBoxW(owner, text.c_str(), title.c_str(), MB_OK | MB_ICONINFORMATION);
}
void errorBox(HWND owner, const std::wstring& text, const std::wstring& title) {
    MessageBoxW(owner, text.c_str(), title.c_str(), MB_OK | MB_ICONERROR);
}
bool confirmBox(HWND owner, const std::wstring& text, const std::wstring& title) {
    return MessageBoxW(owner, text.c_str(), title.c_str(), MB_YESNO | MB_ICONQUESTION) == IDYES;
}

}  // namespace ui
