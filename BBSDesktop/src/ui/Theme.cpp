// Theme.cpp — Fluent 2 palette resolution, fonts, DPI, and window backdrop.
#include "Theme.h"

#include <dwmapi.h>

#pragma comment(lib, "dwmapi.lib")
#pragma comment(lib, "uxtheme.lib")
#pragma comment(lib, "gdiplus.lib")

using Gdiplus::Color;

namespace ui {

static ULONG_PTR g_gdiplusToken = 0;

void gdiplusStartup() {
    Gdiplus::GdiplusStartupInput in;
    Gdiplus::GdiplusStartup(&g_gdiplusToken, &in, nullptr);
}
void gdiplusShutdown() {
    if (g_gdiplusToken) Gdiplus::GdiplusShutdown(g_gdiplusToken);
    g_gdiplusToken = 0;
}

Theme& theme() {
    static Theme t;
    return t;
}

// ---- color helpers ----
static Color rgb(BYTE r, BYTE g, BYTE b) { return Color(255, r, g, b); }

static double luminance(const Color& c) {
    return (0.2126 * c.GetR() + 0.7152 * c.GetG() + 0.0722 * c.GetB()) / 255.0;
}
static Color adjust(const Color& c, double factor) {  // factor>1 lighten, <1 darken
    auto clamp = [](double v) { return (BYTE)(v < 0 ? 0 : v > 255 ? 255 : v); };
    return Color(c.GetA(), clamp(c.GetR() * factor), clamp(c.GetG() * factor), clamp(c.GetB() * factor));
}

bool systemUsesDarkMode() {
    HKEY key;
    DWORD val = 1, sz = sizeof(val);
    if (RegOpenKeyExW(HKEY_CURRENT_USER,
                      L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
                      0, KEY_READ, &key) == ERROR_SUCCESS) {
        RegQueryValueExW(key, L"AppsUseLightTheme", nullptr, nullptr, (LPBYTE)&val, &sz);
        RegCloseKey(key);
    }
    return val == 0;  // AppsUseLightTheme == 0 means dark
}

Color systemAccentColor(bool dark) {
    HKEY key;
    DWORD val = 0, sz = sizeof(val);
    bool got = false;
    if (RegOpenKeyExW(HKEY_CURRENT_USER, L"Software\\Microsoft\\Windows\\DWM", 0, KEY_READ, &key) ==
        ERROR_SUCCESS) {
        if (RegQueryValueExW(key, L"AccentColor", nullptr, nullptr, (LPBYTE)&val, &sz) == ERROR_SUCCESS)
            got = true;
        RegCloseKey(key);
    }
    if (got) {
        // Stored as 0xAABBGGRR.
        BYTE r = val & 0xFF, g = (val >> 8) & 0xFF, b = (val >> 16) & 0xFF;
        Color accent = rgb(r, g, b);
        // On dark backgrounds nudge very dark accents lighter for contrast.
        if (dark && luminance(accent) < 0.30) accent = adjust(accent, 1.5);
        return accent;
    }
    return dark ? rgb(0x47, 0x9E, 0xF5) : rgb(0x0F, 0x6C, 0xBD);  // Fluent default blue
}

void Theme::resolvePalette() {
    bool dark = systemUsesDarkMode();
    c.dark = dark;
    c.accent = systemAccentColor(dark);
    c.accentHover = adjust(c.accent, dark ? 1.12 : 0.92);
    c.accentPressed = adjust(c.accent, dark ? 0.9 : 0.82);
    c.textOnAccent = luminance(c.accent) > 0.6 ? rgb(0, 0, 0) : rgb(255, 255, 255);

    if (dark) {
        c.appBg = rgb(0x20, 0x20, 0x20);
        c.navBg = rgb(0x1B, 0x1B, 0x1B);
        c.layerBg = rgb(0x27, 0x27, 0x27);
        c.card = rgb(0x2B, 0x2B, 0x2B);
        c.cardBorder = rgb(0x38, 0x38, 0x38);
        c.cardHeaderLine = rgb(0x38, 0x38, 0x38);
        c.textPrimary = rgb(0xFF, 0xFF, 0xFF);
        c.textSecondary = rgb(0xC8, 0xC8, 0xC8);
        c.textTertiary = rgb(0xA8, 0xA8, 0xA8);  // raised for WCAG AA on dark cards
        c.textDisabled = rgb(0x6E, 0x6E, 0x6E);
        c.controlBg = rgb(0x33, 0x33, 0x33);
        c.controlBgHover = rgb(0x3A, 0x3A, 0x3A);
        c.controlBorder = rgb(0x45, 0x45, 0x45);
        c.controlBorderFocus = c.accent;
        c.subtleHover = rgb(0x32, 0x32, 0x32);
        c.subtlePressed = rgb(0x2A, 0x2A, 0x2A);
        c.navItemHover = rgb(0x2E, 0x2E, 0x2E);
        c.navSelectedFill = rgb(0x33, 0x33, 0x33);
        c.navIndicator = c.accent;
        c.divider = rgb(0x33, 0x33, 0x33);
        c.gridHeaderBg = rgb(0x32, 0x32, 0x32);
        c.gridAltRow = rgb(0x25, 0x25, 0x25);
        c.selectionFill = adjust(c.accent, 0.5);
        c.success = rgb(0x6C, 0xCB, 0x5F);
        c.danger = rgb(0xFF, 0x99, 0xA4);
        c.successBg = rgb(0x18, 0x33, 0x1E);
        c.dangerBg = rgb(0x40, 0x1B, 0x20);
        c.shadow = Color(90, 0, 0, 0);
    } else {
        c.appBg = rgb(0xF3, 0xF3, 0xF3);
        c.navBg = rgb(0xEB, 0xEB, 0xEB);
        c.layerBg = rgb(0xFA, 0xFA, 0xFA);
        c.card = rgb(0xFF, 0xFF, 0xFF);
        c.cardBorder = rgb(0xE5, 0xE5, 0xE5);
        c.cardHeaderLine = rgb(0xEC, 0xEC, 0xEC);
        c.textPrimary = rgb(0x24, 0x24, 0x24);
        c.textSecondary = rgb(0x4A, 0x4A, 0x4A);  // ~7:1 on white — WCAG AA
        c.textTertiary = rgb(0x5C, 0x5C, 0x5C);   // ≥4.5:1 on #fff / card
        c.textDisabled = rgb(0xBD, 0xBD, 0xBD);
        c.controlBg = rgb(0xFF, 0xFF, 0xFF);
        c.controlBgHover = rgb(0xF7, 0xF7, 0xF7);
        c.controlBorder = rgb(0xD1, 0xD1, 0xD1);
        c.controlBorderFocus = c.accent;
        c.subtleHover = rgb(0xEE, 0xEE, 0xEE);
        c.subtlePressed = rgb(0xE3, 0xE3, 0xE3);
        c.navItemHover = rgb(0xE1, 0xE1, 0xE1);
        c.navSelectedFill = rgb(0xFF, 0xFF, 0xFF);
        c.navIndicator = c.accent;
        c.divider = rgb(0xE1, 0xDF, 0xDD);
        c.gridHeaderBg = rgb(0xF3, 0xF2, 0xF1);
        c.gridAltRow = rgb(0xFA, 0xFA, 0xFA);
        c.selectionFill = adjust(c.accent, 1.7);
        c.success = rgb(0x0B, 0x7A, 0x3B);
        c.danger = rgb(0xB1, 0x0E, 0x1C);
        c.successBg = rgb(0xE4, 0xF3, 0xE9);
        c.dangerBg = rgb(0xFC, 0xE9, 0xEB);
        c.shadow = Color(30, 0, 0, 0);
    }
}

static HFONT makeFont(int dpi, int px, int weight, const wchar_t* face) {
    LOGFONTW lf{};
    lf.lfHeight = -MulDiv(px, dpi, 96);
    lf.lfWeight = weight;
    lf.lfQuality = CLEARTYPE_QUALITY;
    lf.lfCharSet = DEFAULT_CHARSET;
    wcscpy_s(lf.lfFaceName, face);
    HFONT f = CreateFontIndirectW(&lf);
    if (!f) {  // fallback if the variable font isn't available
        wcscpy_s(lf.lfFaceName, L"Segoe UI");
        f = CreateFontIndirectW(&lf);
    }
    return f;
}

void Theme::makeFonts() {
    const wchar_t* disp = L"Segoe UI Variable Display";
    const wchar_t* text = L"Segoe UI Variable Text";
    fDisplay = makeFont(dpi, 28, FW_SEMIBOLD, disp);
    fTitle = makeFont(dpi, 20, FW_SEMIBOLD, disp);
    fSubtitle = makeFont(dpi, 15, FW_NORMAL, text);
    fCardTitle = makeFont(dpi, 15, FW_SEMIBOLD, text);
    fBodyStrong = makeFont(dpi, 13, FW_SEMIBOLD, text);
    fBody = makeFont(dpi, 13, FW_NORMAL, text);
    fCaption = makeFont(dpi, 12, FW_NORMAL, text);
    fIcon = makeFont(dpi, 17, FW_NORMAL, L"Segoe Fluent Icons");
    fIconSmall = makeFont(dpi, 14, FW_NORMAL, L"Segoe Fluent Icons");
}

void Theme::destroy() {
    HFONT* fonts[] = {&fDisplay, &fTitle, &fSubtitle, &fCardTitle, &fBodyStrong,
                      &fBody, &fCaption, &fIcon, &fIconSmall};
    for (HFONT* f : fonts) {
        if (*f) { DeleteObject(*f); *f = nullptr; }
    }
}

void Theme::init(int dpiValue) {
    dpi = dpiValue;
    resolvePalette();
    makeFonts();
}
void Theme::reload(int dpiValue) {
    destroy();
    dpi = dpiValue;
    resolvePalette();
    makeFonts();
}

// ---- window backdrop (Win11) ----
void applyWindowBackdrop(HWND hwnd, bool dark) {
    BOOL d = dark ? TRUE : FALSE;
    // DWMWA_USE_IMMERSIVE_DARK_MODE = 20
    DwmSetWindowAttribute(hwnd, 20, &d, sizeof(d));
    // DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2
    int corner = 2;
    DwmSetWindowAttribute(hwnd, 33, &corner, sizeof(corner));
    // DWMWA_SYSTEMBACKDROP_TYPE = 38, DWMSBT_MAINWINDOW (Mica) = 2
    int backdrop = 2;
    DwmSetWindowAttribute(hwnd, 38, &backdrop, sizeof(backdrop));
}

void enableDarkScrollbars(HWND hwnd, bool dark) {
    SetWindowTheme(hwnd, dark ? L"DarkMode_Explorer" : L"Explorer", nullptr);
}

}  // namespace ui
