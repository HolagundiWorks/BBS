// Widgets.h — Fluent-themed native Win32 controls (Common Controls + visual styles).
#pragma once

#include "../core/Model.h"
#include "Draw.h"
#include <commctrl.h>
#include <string>
#include <vector>

namespace ui {

enum class ButtonKind { Primary, Default, Subtle, Danger };

// Native themed button. Owner-draw only when a glyph is supplied (nav chrome).
HWND createButton(HWND parent, int id, const std::wstring& text, ButtonKind kind,
                  const std::wstring& glyph = L"");
void setButtonKind(HWND btn, ButtonKind kind);
void paintButton(const DRAWITEMSTRUCT* dis);

HWND createEdit(HWND parent, int id, const std::wstring& text);
HWND createStatic(HWND parent, int id, const std::wstring& text, bool bold = false);
void paintFieldContainer(Graphics& g, RECT fieldRect, bool focused);  // Settings page only

HWND createCombo(HWND parent, int id, const std::vector<std::wstring>& items, int selected = 0);

struct Column { std::wstring title; int width; bool rightAlign = false; };
HWND createListView(HWND parent, int id);
void setListColumns(HWND lv, const std::vector<Column>& cols);
void setListRows(HWND lv, const std::vector<std::vector<std::wstring>>& rows);
void applyListTheme(HWND lv);
LRESULT handleListCustomDraw(HWND lv, NMLVCUSTOMDRAW* cd);
LRESULT handleHeaderCustomDraw(NMCUSTOMDRAW* cd);

std::vector<std::wstring> diameterOptions(const bbs::Settings& s, bool optionalBlank = false);
void refillCombo(HWND combo, const std::vector<std::wstring>& items, const std::wstring& select);

std::wstring toW(const std::string& s);
std::string fromW(const std::wstring& s);
std::wstring getText(HWND ctl);
void setText(HWND ctl, const std::wstring& s);

}  // namespace ui
