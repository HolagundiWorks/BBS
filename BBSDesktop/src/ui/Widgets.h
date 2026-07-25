// Widgets.h — reusable Fluent 2 controls built on native Win32 controls
// (accessible + keyboard-navigable) with custom painting for the Fluent look:
// owner-drawn buttons, painted text-field containers, and themed list views.
#pragma once

#include "../core/Model.h"
#include "Draw.h"
#include <commctrl.h>
#include <string>
#include <vector>

namespace ui {

enum class ButtonKind { Primary, Default, Subtle, Danger };

// --- Buttons (owner-drawn BUTTON: keeps native focus/keyboard/role) ---
HWND createButton(HWND parent, int id, const std::wstring& text, ButtonKind kind,
                  const std::wstring& glyph = L"");
void setButtonKind(HWND btn, ButtonKind kind);
void paintButton(const DRAWITEMSTRUCT* dis);  // call from parent WM_DRAWITEM

// --- Text field (borderless EDIT inside a painted rounded container) ---
HWND createEdit(HWND parent, int id, const std::wstring& text);
// Paint the field container behind an edit; `edit` gives geometry + focus state.
void paintFieldContainer(Graphics& g, RECT fieldRect, bool focused);

// --- Combo box (native dropdown, themed) ---
HWND createCombo(HWND parent, int id, const std::vector<std::wstring>& items, int selected = 0);

// --- List view (report mode, themed, double-buffered) ---
struct Column { std::wstring title; int width; bool rightAlign = false; };
HWND createListView(HWND parent, int id);
void setListColumns(HWND lv, const std::vector<Column>& cols);
void setListRows(HWND lv, const std::vector<std::vector<std::wstring>>& rows);
void applyListTheme(HWND lv);
// Custom-draw hook for list views (alt rows, TOTAL emphasis, status colors).
LRESULT handleListCustomDraw(HWND lv, NMLVCUSTOMDRAW* cd);
// Custom-draw for listview column headers (fixes dark/light contrast).
LRESULT handleHeaderCustomDraw(NMCUSTOMDRAW* cd);

// Diameter dropdown options from project settings.
std::vector<std::wstring> diameterOptions(const bbs::Settings& s, bool optionalBlank = false);
void refillCombo(HWND combo, const std::vector<std::wstring>& items, const std::wstring& select);

// Shared helpers.
std::wstring toW(const std::string& s);
std::string fromW(const std::wstring& s);
std::wstring getText(HWND ctl);
void setText(HWND ctl, const std::wstring& s);

}  // namespace ui
