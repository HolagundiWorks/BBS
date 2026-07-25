// Theme.h — Fluent 2 design tokens: color palette (light/dark), typography
// ramp, an 8px spacing scale, DPI scaling, and window backdrop (Mica) helpers.
#pragma once

#ifndef UNICODE
#define UNICODE
#endif
#include <windows.h>
#include <objidl.h>
#include <gdiplus.h>
#include <string>

namespace ui {

// ---- 8px spacing system (Fluent metrics) ----
namespace space {
constexpr int xs = 4, s = 8, m = 12, l = 16, xl = 20, xxl = 24, xxxl = 32;
}

// ---- Radii ----
namespace radius {
constexpr int control = 4, card = 8, pill = 999;
}

// Fluent 2 color palette resolved for the active theme.
struct Palette {
    bool dark = false;

    Gdiplus::Color appBg, navBg, layerBg;
    Gdiplus::Color card, cardBorder, cardHeaderLine;
    Gdiplus::Color textPrimary, textSecondary, textTertiary, textDisabled;
    Gdiplus::Color accent, accentHover, accentPressed, textOnAccent;
    Gdiplus::Color controlBg, controlBgHover, controlBorder, controlBorderFocus;
    Gdiplus::Color subtleHover, subtlePressed;
    Gdiplus::Color navItemHover, navSelectedFill, navIndicator;
    Gdiplus::Color divider, gridHeaderBg, gridAltRow, selectionFill;
    Gdiplus::Color success, danger, successBg, dangerBg;
    Gdiplus::Color shadow;
};

// The whole theme: palette + DPI + fonts, owned globally.
class Theme {
public:
    Palette c;
    int dpi = 96;

    // Fonts (owned). Recreated on DPI change.
    HFONT fDisplay = nullptr;    // 28 semibold  — page/dashboard hero
    HFONT fTitle = nullptr;      // 20 semibold  — page titles
    HFONT fSubtitle = nullptr;   // 15 regular   — page subtitles / big numbers
    HFONT fCardTitle = nullptr;  // 15 semibold  — card section titles
    HFONT fBodyStrong = nullptr; // 13 semibold  — labels / emphasis
    HFONT fBody = nullptr;       // 13 regular   — controls / text
    HFONT fCaption = nullptr;    // 12 regular   — captions / hints
    HFONT fIcon = nullptr;       // Segoe Fluent Icons — nav glyphs
    HFONT fIconSmall = nullptr;

    void init(int dpiValue);
    void reload(int dpiValue);   // theme changed / dpi changed
    void destroy();

    int dp(int px) const { return MulDiv(px, dpi, 96); }  // scale device-independent px

    // Convenience conversions.
    static COLORREF toRef(const Gdiplus::Color& c) {
        return RGB(c.GetR(), c.GetG(), c.GetB());
    }

private:
    void makeFonts();
    void resolvePalette();
};

// Global theme instance.
Theme& theme();

// ---- system integration ----
bool systemUsesDarkMode();
Gdiplus::Color systemAccentColor(bool dark);
void applyWindowBackdrop(HWND hwnd, bool dark);   // Mica + dark titlebar + rounded corners
void enableDarkScrollbars(HWND hwnd, bool dark);  // best-effort dark theme for a control

// GDI+ process lifetime.
void gdiplusStartup();
void gdiplusShutdown();

}  // namespace ui
