// Widgets.cpp — Fluent 2 control implementations.
#include "Widgets.h"

#include <commctrl.h>
#include <unordered_map>
#include <uxtheme.h>
#include <windowsx.h>

#pragma comment(lib, "comctl32.lib")

namespace ui {

using namespace Gdiplus;

std::wstring toW(const std::string& s) {
    if (s.empty()) return L"";
    int n = MultiByteToWideChar(CP_UTF8, 0, s.data(), (int)s.size(), nullptr, 0);
    std::wstring w(n, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, s.data(), (int)s.size(), &w[0], n);
    return w;
}
std::string fromW(const std::wstring& w) {
    if (w.empty()) return "";
    int n = WideCharToMultiByte(CP_UTF8, 0, w.data(), (int)w.size(), nullptr, 0, nullptr, nullptr);
    std::string s(n, '\0');
    WideCharToMultiByte(CP_UTF8, 0, w.data(), (int)w.size(), &s[0], n, nullptr, nullptr);
    return s;
}
std::wstring getText(HWND ctl) {
    int n = GetWindowTextLengthW(ctl);
    std::wstring s(n, L'\0');
    GetWindowTextW(ctl, &s[0], n + 1);
    return s;
}
void setText(HWND ctl, const std::wstring& s) { SetWindowTextW(ctl, s.c_str()); }

// ------------------------------ Buttons ------------------------------

struct ButtonInfo {
    ButtonKind kind = ButtonKind::Default;
    std::wstring glyph;
    bool hot = false;
};
static std::unordered_map<HWND, ButtonInfo> g_buttons;

static LRESULT CALLBACK btnSubclass(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp, UINT_PTR, DWORD_PTR) {
    switch (msg) {
        case WM_MOUSEMOVE: {
            auto it = g_buttons.find(hwnd);
            if (it != g_buttons.end() && !it->second.hot) {
                it->second.hot = true;
                TRACKMOUSEEVENT tme{sizeof(tme), TME_LEAVE, hwnd, 0};
                TrackMouseEvent(&tme);
                InvalidateRect(hwnd, nullptr, FALSE);
            }
            break;
        }
        case WM_MOUSELEAVE: {
            auto it = g_buttons.find(hwnd);
            if (it != g_buttons.end()) { it->second.hot = false; InvalidateRect(hwnd, nullptr, FALSE); }
            break;
        }
        case WM_NCDESTROY:
            g_buttons.erase(hwnd);
            RemoveWindowSubclass(hwnd, btnSubclass, 1);
            break;
    }
    return DefSubclassProc(hwnd, msg, wp, lp);
}

HWND createButton(HWND parent, int id, const std::wstring& text, ButtonKind kind,
                  const std::wstring& glyph) {
    // Prefer native themed push-buttons (Fluent via visual styles). Owner-draw only for glyph icons.
    if (glyph.empty()) {
        DWORD style = WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_PUSHBUTTON;
        if (kind == ButtonKind::Primary) style |= BS_DEFPUSHBUTTON;
        HWND b = CreateWindowExW(0, L"BUTTON", text.c_str(), style, 0, 0, 0, 0, parent,
                                 (HMENU)(INT_PTR)id, GetModuleHandleW(nullptr), nullptr);
        SendMessageW(b, WM_SETFONT, (WPARAM)theme().fBody, TRUE);
        SetWindowTheme(b, theme().c.dark ? L"DarkMode_Explorer" : L"Explorer", nullptr);
        return b;
    }
    HWND b = CreateWindowExW(0, L"BUTTON", text.c_str(),
                             WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_OWNERDRAW, 0, 0, 0, 0, parent,
                             (HMENU)(INT_PTR)id, GetModuleHandleW(nullptr), nullptr);
    g_buttons[b] = ButtonInfo{kind, glyph, false};
    SetWindowSubclass(b, btnSubclass, 1, 0);
    return b;
}
void setButtonKind(HWND btn, ButtonKind kind) {
    auto it = g_buttons.find(btn);
    if (it != g_buttons.end()) it->second.kind = kind;
    InvalidateRect(btn, nullptr, FALSE);
}

void paintButton(const DRAWITEMSTRUCT* dis) {
    auto& t = theme();
    HWND btn = dis->hwndItem;
    ButtonInfo info;
    auto it = g_buttons.find(btn);
    if (it != g_buttons.end()) info = it->second;

    RECT rc = dis->rcItem;
    int w = rc.right - rc.left, h = rc.bottom - rc.top;
    bool pressed = (dis->itemState & ODS_SELECTED) != 0;
    bool focus = (dis->itemState & ODS_FOCUS) != 0;
    bool disabled = (dis->itemState & ODS_DISABLED) != 0;

    // Double buffer.
    HDC hdc = dis->hDC;
    HDC mem = CreateCompatibleDC(hdc);
    HBITMAP bmp = CreateCompatibleBitmap(hdc, w, h);
    HBITMAP old = (HBITMAP)SelectObject(mem, bmp);
    {
        Graphics g(mem);
        g.SetSmoothingMode(SmoothingModeAntiAlias);

        Color fill, border(0, 0, 0, 0), textCol = t.c.textPrimary;
        bool hasBorder = false;
        switch (info.kind) {
            case ButtonKind::Primary:
                fill = pressed ? t.c.accentPressed : info.hot ? t.c.accentHover : t.c.accent;
                textCol = t.c.textOnAccent;
                break;
            case ButtonKind::Default:
                fill = pressed ? t.c.subtlePressed : info.hot ? t.c.controlBgHover : t.c.controlBg;
                border = t.c.controlBorder; hasBorder = true;
                break;
            case ButtonKind::Subtle:
                fill = pressed ? t.c.subtlePressed : info.hot ? t.c.subtleHover : t.c.card;
                break;
            case ButtonKind::Danger:
                fill = pressed ? t.c.subtlePressed : info.hot ? t.c.controlBgHover : t.c.controlBg;
                border = t.c.controlBorder; hasBorder = true;
                textCol = t.c.danger;
                break;
        }
        if (disabled) { fill = t.c.controlBg; textCol = t.c.textDisabled; }

        RectF rf(0, 0, (float)w, (float)h);
        float rad = (float)t.dp(radius::control);
        fillRound(g, rf, rad, fill);
        if (hasBorder) strokeRound(g, rf, rad, border, 1.0f);

        std::wstring label = getText(btn);
        int tpx = t.dp(13);
        // Center glyph + label as a group.
        float labelW = label.empty() ? 0 : measureText(g, label, tpx, FW_SEMIBOLD, (float)w).Width;
        float glyphW = info.glyph.empty() ? 0 : (float)t.dp(16);
        float gap = (glyphW > 0 && labelW > 0) ? (float)t.dp(8) : 0;
        float groupW = glyphW + gap + labelW;
        float startX = (w - groupW) / 2.0f;

        if (!info.glyph.empty()) {
            RectF gb(startX, 0, glyphW, (float)h);
            drawGlyph(g, info.glyph, gb, t.dp(15), textCol, Align::Center, Align::Center);
        }
        if (!label.empty()) {
            RectF tb(startX + glyphW + gap, 0, labelW + 2, (float)h);
            TextStyle st{tpx, FW_SEMIBOLD, textCol};
            drawText(g, label, tb, st, Align::Near, Align::Center);
        }
        if (focus && !disabled) {
            Color fc = t.c.dark ? Color(255, 255, 255, 255) : Color(255, 0, 0, 0);
            RectF fr(1.5f, 1.5f, w - 3.0f, h - 3.0f);
            strokeRound(g, fr, rad, fc, 1.5f);
        }
    }
    BitBlt(hdc, rc.left, rc.top, w, h, mem, 0, 0, SRCCOPY);
    SelectObject(mem, old);
    DeleteObject(bmp);
    DeleteDC(mem);
}

// ------------------------------ Edit field ------------------------------

HWND createEdit(HWND parent, int id, const std::wstring& text) {
    HWND e = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", text.c_str(),
                             WS_CHILD | WS_VISIBLE | WS_TABSTOP | ES_AUTOHSCROLL, 0, 0, 0, 0, parent,
                             (HMENU)(INT_PTR)id, GetModuleHandleW(nullptr), nullptr);
    SendMessageW(e, WM_SETFONT, (WPARAM)theme().fBody, TRUE);
    SetWindowTheme(e, theme().c.dark ? L"DarkMode_Explorer" : L"Explorer", nullptr);
    return e;
}

HWND createStatic(HWND parent, int id, const std::wstring& text, bool bold) {
    HWND s = CreateWindowExW(0, L"STATIC", text.c_str(),
                             WS_CHILD | WS_VISIBLE | SS_LEFT | SS_NOPREFIX, 0, 0, 0, 0, parent,
                             (HMENU)(INT_PTR)id, GetModuleHandleW(nullptr), nullptr);
    SendMessageW(s, WM_SETFONT, (WPARAM)(bold ? theme().fBodyStrong : theme().fCaption), TRUE);
    return s;
}

void paintFieldContainer(Graphics& g, RECT fieldRect, bool focused) {
    auto& t = theme();
    RectF r = toRectF(fieldRect);
    float rad = (float)t.dp(radius::control);
    fillRound(g, r, rad, t.c.controlBg);
    if (focused) {
        strokeRound(g, r, rad, t.c.accent, 1.5f);
        // Fluent focus underline.
        RectF u(r.X + 1, r.GetBottom() - 2.0f, r.Width - 2, 2.0f);
        SolidBrush b(t.c.accent);
        g.FillRectangle(&b, u);
    } else {
        strokeRound(g, r, rad, t.c.controlBorder, 1.0f);
    }
}

// ------------------------------ Combo box ------------------------------

HWND createCombo(HWND parent, int id, const std::vector<std::wstring>& items, int selected) {
    HWND c = CreateWindowExW(0, L"COMBOBOX", L"",
                             WS_CHILD | WS_VISIBLE | WS_TABSTOP | CBS_DROPDOWNLIST | WS_VSCROLL, 0, 0,
                             0, 0, parent, (HMENU)(INT_PTR)id, GetModuleHandleW(nullptr), nullptr);
    for (const auto& s : items) SendMessageW(c, CB_ADDSTRING, 0, (LPARAM)s.c_str());
    SendMessageW(c, CB_SETCURSEL, selected, 0);
    SendMessageW(c, WM_SETFONT, (WPARAM)theme().fBody, TRUE);
    SetWindowTheme(c, theme().c.dark ? L"DarkMode_CFD" : L"CFD", nullptr);
    return c;
}

// ------------------------------ List view ------------------------------

HWND createListView(HWND parent, int id) {
    HWND lv = CreateWindowExW(0, WC_LISTVIEWW, L"",
                              WS_CHILD | WS_VISIBLE | LVS_REPORT | LVS_SINGLESEL | LVS_SHOWSELALWAYS,
                              0, 0, 0, 0, parent, (HMENU)(INT_PTR)id, GetModuleHandleW(nullptr),
                              nullptr);
    ListView_SetExtendedListViewStyle(
        lv, LVS_EX_FULLROWSELECT | LVS_EX_DOUBLEBUFFER | LVS_EX_AUTOSIZECOLUMNS);
    SendMessageW(lv, WM_SETFONT, (WPARAM)theme().fBody, TRUE);
    applyListTheme(lv);
    return lv;
}

void applyListTheme(HWND lv) {
    auto& t = theme();
    SetWindowTheme(lv, t.c.dark ? L"DarkMode_Explorer" : L"Explorer", nullptr);
    ListView_SetBkColor(lv, Theme::toRef(t.c.card));
    ListView_SetTextBkColor(lv, Theme::toRef(t.c.card));
    ListView_SetTextColor(lv, Theme::toRef(t.c.textPrimary));
    HWND hdr = ListView_GetHeader(lv);
    if (hdr) {
        // Disable visual styles so NM_CUSTOMDRAW can paint readable headers.
        SetWindowTheme(hdr, L"", L"");
        InvalidateRect(hdr, nullptr, TRUE);
    }
    InvalidateRect(lv, nullptr, TRUE);
}

std::vector<std::wstring> diameterOptions(const bbs::Settings& s, bool optionalBlank) {
    std::vector<std::wstring> opts;
    if (optionalBlank) opts.push_back(L"");
    for (int d : s.diameters) opts.push_back(std::to_wstring(d));
    if (opts.empty()) opts.push_back(L"8");
    return opts;
}

void refillCombo(HWND combo, const std::vector<std::wstring>& items, const std::wstring& select) {
    if (!combo) return;
    std::wstring cur = select;
    if (cur.empty()) {
        int sel = (int)SendMessageW(combo, CB_GETCURSEL, 0, 0);
        wchar_t buf[64] = L"";
        if (sel >= 0) SendMessageW(combo, CB_GETLBTEXT, sel, (LPARAM)buf);
        cur = buf;
    }
    SendMessageW(combo, CB_RESETCONTENT, 0, 0);
    int pick = 0;
    for (int i = 0; i < (int)items.size(); ++i) {
        SendMessageW(combo, CB_ADDSTRING, 0, (LPARAM)items[i].c_str());
        if (items[i] == cur) pick = i;
    }
    SendMessageW(combo, CB_SETCURSEL, pick, 0);
}

LRESULT handleHeaderCustomDraw(NMCUSTOMDRAW* cd) {
    auto& t = theme();
    switch (cd->dwDrawStage) {
        case CDDS_PREPAINT:
            return CDRF_NOTIFYITEMDRAW;
        case CDDS_ITEMPREPAINT: {
            HWND hdr = cd->hdr.hwndFrom;
            int i = (int)cd->dwItemSpec;
            wchar_t text[128] = L"";
            HDITEMW hi{};
            hi.mask = HDI_TEXT;
            hi.pszText = text;
            hi.cchTextMax = 128;
            Header_GetItem(hdr, i, &hi);

            RECT rc = cd->rc;
            HDC hdc = cd->hdc;
            HBRUSH br = CreateSolidBrush(Theme::toRef(t.c.gridHeaderBg));
            FillRect(hdc, &rc, br);
            DeleteObject(br);
            // Bottom divider line for separation from rows.
            HPEN pen = CreatePen(PS_SOLID, 1, Theme::toRef(t.c.divider));
            HGDIOBJ oldPen = SelectObject(hdc, pen);
            MoveToEx(hdc, rc.left, rc.bottom - 1, nullptr);
            LineTo(hdc, rc.right, rc.bottom - 1);
            SelectObject(hdc, oldPen);
            DeleteObject(pen);

            SetBkMode(hdc, TRANSPARENT);
            SetTextColor(hdc, Theme::toRef(t.c.textPrimary));
            HFONT old = (HFONT)SelectObject(hdc, t.fBodyStrong ? t.fBodyStrong : t.fBody);
            RECT tr = rc;
            InflateRect(&tr, -theme().dp(8), 0);
            DrawTextW(hdc, text, -1, &tr, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS);
            SelectObject(hdc, old);
            return CDRF_SKIPDEFAULT;
        }
    }
    return CDRF_DODEFAULT;
}

void setListColumns(HWND lv, const std::vector<Column>& cols) {
    // Remove existing columns.
    while (ListView_DeleteColumn(lv, 0)) {}
    for (int i = 0; i < (int)cols.size(); ++i) {
        LVCOLUMNW c{};
        c.mask = LVCF_TEXT | LVCF_WIDTH | LVCF_FMT;
        c.fmt = cols[i].rightAlign ? LVCFMT_RIGHT : LVCFMT_LEFT;
        c.cx = theme().dp(cols[i].width);
        c.pszText = const_cast<LPWSTR>(cols[i].title.c_str());
        ListView_InsertColumn(lv, i, &c);
    }
}

void setListRows(HWND lv, const std::vector<std::vector<std::wstring>>& rows) {
    ListView_DeleteAllItems(lv);
    for (int r = 0; r < (int)rows.size(); ++r) {
        LVITEMW it{};
        it.mask = LVIF_TEXT;
        it.iItem = r;
        it.pszText = const_cast<LPWSTR>(rows[r].empty() ? L"" : rows[r][0].c_str());
        ListView_InsertItem(lv, &it);
        for (int c = 1; c < (int)rows[r].size(); ++c)
            ListView_SetItemText(lv, r, c, const_cast<LPWSTR>(rows[r][c].c_str()));
    }
}

LRESULT handleListCustomDraw(HWND lv, NMLVCUSTOMDRAW* cd) {
    auto& t = theme();
    switch (cd->nmcd.dwDrawStage) {
        case CDDS_PREPAINT:
            return CDRF_NOTIFYITEMDRAW;
        case CDDS_ITEMPREPAINT: {
            int row = (int)cd->nmcd.dwItemSpec;
            wchar_t buf[64] = L"";
            ListView_GetItemText(lv, row, 0, buf, 64);
            bool total = (wcscmp(buf, L"TOTAL") == 0);
            bool alt = (row % 2) == 1;
            cd->clrText = Theme::toRef(t.c.textPrimary);
            cd->clrTextBk = Theme::toRef(total ? t.c.gridHeaderBg : (alt ? t.c.gridAltRow : t.c.card));
            if (total) {
                SelectObject(cd->nmcd.hdc, theme().fBodyStrong);
                return CDRF_NOTIFYSUBITEMDRAW | CDRF_NEWFONT;
            }
            return CDRF_NOTIFYSUBITEMDRAW;
        }
        case CDDS_ITEMPREPAINT | CDDS_SUBITEM: {
            int row = (int)cd->nmcd.dwItemSpec;
            int col = cd->iSubItem;
            wchar_t buf[128] = L"";
            ListView_GetItemText(lv, row, col, buf, 128);
            std::wstring s = buf;
            if (s == L"OK")
                cd->clrText = Theme::toRef(t.c.success);
            else if (s.find(L"Insufficient") != std::wstring::npos ||
                     s.find(L"Increase") != std::wstring::npos)
                cd->clrText = Theme::toRef(t.c.danger);
            return CDRF_DODEFAULT;
        }
    }
    return CDRF_DODEFAULT;
}

}  // namespace ui
