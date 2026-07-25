// Draw.cpp — GDI+ drawing primitive implementations.
#include "Draw.h"

namespace ui {

using namespace Gdiplus;

static void addRoundRect(GraphicsPath& path, RectF r, float radius) {
    float d = radius * 2;
    if (d > r.Width) d = r.Width;
    if (d > r.Height) d = r.Height;
    if (d <= 0.5f) {
        path.AddRectangle(r);
        return;
    }
    path.AddArc(r.X, r.Y, d, d, 180, 90);
    path.AddArc(r.GetRight() - d, r.Y, d, d, 270, 90);
    path.AddArc(r.GetRight() - d, r.GetBottom() - d, d, d, 0, 90);
    path.AddArc(r.X, r.GetBottom() - d, d, d, 90, 90);
    path.CloseFigure();
}

void fillRound(Graphics& g, RectF r, float radius, Color c) {
    GraphicsPath path;
    addRoundRect(path, r, radius);
    SolidBrush b(c);
    g.FillPath(&b, &path);
}

void strokeRound(Graphics& g, RectF r, float radius, Color c, float width) {
    GraphicsPath path;
    RectF rr(r.X + width / 2, r.Y + width / 2, r.Width - width, r.Height - width);
    addRoundRect(path, rr, radius);
    Pen pen(c, width);
    g.DrawPath(&pen, &path);
}

void drawCard(Graphics& g, RECT rc) {
    auto& t = theme();
    RectF r = toRectF(rc);
    float rad = (float)t.dp(radius::card);

    // Soft elevation: a couple of offset translucent silhouettes.
    for (int i = 3; i >= 1; --i) {
        RectF s(r.X - i, r.Y + i, r.Width + 2 * i, r.Height + 2 * i);
        BYTE a = (BYTE)(t.c.shadow.GetA() / (i * 2));
        fillRound(g, s, rad + i, Color(a, t.c.shadow.GetR(), t.c.shadow.GetG(), t.c.shadow.GetB()));
    }
    fillRound(g, r, rad, t.c.card);
    strokeRound(g, r, rad, t.c.cardBorder, 1.0f);
}

static const wchar_t* familyFor(int weight) {
    return weight >= 600 ? L"Segoe UI Semibold" : L"Segoe UI";
}

float drawText(Graphics& g, const std::wstring& s, RectF box, const TextStyle& st, Align h,
               Align v, bool wrap, bool ellipsis) {
    g.SetTextRenderingHint(TextRenderingHintClearTypeGridFit);
    FontFamily fam(familyFor(st.weight));
    FontFamily fallback(L"Segoe UI");
    const FontFamily& use = fam.IsAvailable() ? fam : fallback;
    Font font(&use, (REAL)st.px, FontStyleRegular, UnitPixel);

    StringFormat fmt;
    fmt.SetAlignment(h == Align::Near ? StringAlignmentNear
                     : h == Align::Center ? StringAlignmentCenter
                                          : StringAlignmentFar);
    fmt.SetLineAlignment(v == Align::Near ? StringAlignmentNear
                         : v == Align::Center ? StringAlignmentCenter
                                              : StringAlignmentFar);
    if (!wrap) fmt.SetFormatFlags(StringFormatFlagsNoWrap);
    if (ellipsis) fmt.SetTrimming(StringTrimmingEllipsisCharacter);

    SolidBrush brush(st.color);
    g.DrawString(s.c_str(), (INT)s.size(), &font, box, &fmt, &brush);

    RectF bounds;
    g.MeasureString(s.c_str(), (INT)s.size(), &font, box, &fmt, &bounds);
    return bounds.Height;
}

RectF measureText(Graphics& g, const std::wstring& s, int px, int weight, float maxWidth) {
    FontFamily fam(familyFor(weight));
    FontFamily fallback(L"Segoe UI");
    const FontFamily& use = fam.IsAvailable() ? fam : fallback;
    Font font(&use, (REAL)px, FontStyleRegular, UnitPixel);
    StringFormat fmt;
    RectF layout(0, 0, maxWidth, 100000.f);
    RectF bounds;
    g.MeasureString(s.c_str(), (INT)s.size(), &font, layout, &fmt, &bounds);
    return bounds;
}

void drawGlyph(Graphics& g, const std::wstring& glyph, RectF box, int px, Color c, Align h, Align v) {
    g.SetTextRenderingHint(TextRenderingHintAntiAliasGridFit);
    FontFamily fam(L"Segoe Fluent Icons");
    FontFamily fam2(L"Segoe MDL2 Assets");
    const FontFamily& use = fam.IsAvailable() ? fam : fam2;
    Font font(&use, (REAL)px, FontStyleRegular, UnitPixel);
    StringFormat fmt;
    fmt.SetAlignment(h == Align::Near ? StringAlignmentNear
                     : h == Align::Center ? StringAlignmentCenter
                                          : StringAlignmentFar);
    fmt.SetLineAlignment(v == Align::Near ? StringAlignmentNear
                         : v == Align::Center ? StringAlignmentCenter
                                              : StringAlignmentFar);
    fmt.SetFormatFlags(StringFormatFlagsNoWrap);
    SolidBrush brush(c);
    g.DrawString(glyph.c_str(), (INT)glyph.size(), &font, box, &fmt, &brush);
}

}  // namespace ui
