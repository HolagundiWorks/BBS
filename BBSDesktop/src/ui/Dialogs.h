// Dialogs.h — thin wrappers over the common file dialogs + message helpers.
#pragma once

#include <windows.h>
#include <string>

namespace ui {

// Returns chosen path or empty string if cancelled.
std::wstring saveFileDialog(HWND owner, const wchar_t* filter, const wchar_t* defExt,
                            const std::wstring& suggestedName);
std::wstring openFileDialog(HWND owner, const wchar_t* filter);

void infoBox(HWND owner, const std::wstring& text, const std::wstring& title = L"BBS Studio");
void errorBox(HWND owner, const std::wstring& text, const std::wstring& title = L"BBS Studio");
bool confirmBox(HWND owner, const std::wstring& text, const std::wstring& title = L"BBS Studio");

}  // namespace ui
