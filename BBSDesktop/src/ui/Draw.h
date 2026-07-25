// Draw.h — GDI+ drawing primitives for the Fluent 2 look: anti-aliased rounded
// rectangles, cards with soft elevation, text (via the Fluent type ramp) and
// Segoe Fluent Icons glyphs. All custom chrome is painted double-buffered.
#pragma once

#include "Theme.h"
#include <gdiplus.h>
#include <string>

namespace ui {

using Gdiplus::Color;
using Gdiplus::Graphics;
using Gdiplus::RectF;

inline RectF toRectF(const RECT& r) {
    return RectF((float)r.left, (float)r.top, (float)(r.right - r.left), (float)(r.bottom - r.top));
}
inline RECT inflate(RECT r, int dx, int dy) {
    return RECT{r.left - dx, r.top - dy, r.right + dx, r.bottom + dy};
}

enum class Align { Near, Center, Far };

struct TextStyle {
    int px = 13;
    int weight = FW_NORMAL;   // >=600 uses Segoe UI Semibold
    Color color = Color(255, 0, 0, 0);
};

void fillRound(Graphics& g, RectF r, float radius, Color c);
void strokeRound(Graphics& g, RectF r, float radius, Color c, float width = 1.0f);

// Card: soft shadow + fill + hairline border (uses the active palette).
void drawCard(Graphics& g, RECT r);

// Text drawing using the Fluent ramp. Returns the height used.
float drawText(Graphics& g, const std::wstring& s, RectF box, const TextStyle& st,
               Align h = Align::Near, Align v = Align::Near, bool wrap = false, bool ellipsis = true);

RectF measureText(Graphics& g, const std::wstring& s, int px, int weight, float maxWidth = 100000.f);

// Draw a Segoe Fluent Icons glyph centered in `box`.
void drawGlyph(Graphics& g, const std::wstring& glyph, RectF box, int px, Color c,
               Align h = Align::Center, Align v = Align::Center);

}  // namespace ui
