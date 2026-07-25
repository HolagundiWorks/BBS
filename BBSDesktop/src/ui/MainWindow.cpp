// MainWindow.cpp — application shell: NavigationView rail, hosted pages,
// message routing, theming and project file operations.
#include "MainWindow.h"

#include "../core/Export.h"
#include "Compute.h"
#include "Dialogs.h"
#include "Draw.h"
#include "ElementPage.h"
#include "Pages.h"
#include "Widgets.h"

#include <uxtheme.h>
#include <windowsx.h>

namespace ui {

using namespace Gdiplus;

enum : int {
    ID_NEW = 0xA001, ID_OPEN = 0xA002, ID_SAVE = 0xA003, ID_SAVEAS = 0xA004,
    ID_REPORT = 0xB001, ID_DIAS_CHANGED = 0xB010, ID_PAGE0 = 0xC000,
};
enum : int { NAV_NEW = 100, NAV_OPEN = 101, NAV_SAVE = 102 };

static const wchar_t* kClass = L"BBSStudioMainWindow";

static TextStyle ts(int px, int weight, Color c) { return TextStyle{theme().dp(px), weight, c}; }

// ------------------------------ element configs ------------------------------

static ElementConfig columnsConfig() {
    ElementConfig c;
    c.navLabel = L"Columns"; c.glyph = L""; c.key = "columns";
    c.title = L"Columns";
    c.subtitle = L"Ties and longitudinal bars — rectangular, circular and spiral.";
    c.fields = {
        section(L"Identity"),
        textF("mark", L"Mark", L"C1", 3),
        section(L"Geometry"),
        textF("width", L"Width (mm)", L"300", 3),
        textF("depth", L"Depth (mm)", L"450", 3),
        textF("height", L"Height (mm)", L"3200", 3),
        textF("cover", L"Cover (mm)", L"40", 3),
        section(L"Ties"),
        diaF("stirrup_dia", L"Tie Ø (mm)", L"8", 3),
        textF("spacing", L"Tie spacing (mm)", L"150", 3),
        comboF("hook_angle", L"Hook (°)", {L"90", L"135", L"180"}, L"135", 3),
        comboF("tie_type", L"Tie type", {L"Closed", L"Double Tie", L"Circular", L"Spiral"}, L"Closed", 3),
        section(L"Longitudinal bars"),
        textF("bars", L"Bars (dia:qty, comma-separated)", L"12:4, 16:2, 20:4", 12),
    };
    c.inputCols = {{L"Mark", 60}, {L"W", 55, true}, {L"D", 55, true}, {L"H", 66, true}, {L"Bars", 150}};
    c.inputKeys = {"mark", "width", "depth", "height", "bars"};
    c.bbsCols = {{L"Mark", 80}, {L"Role", 120}, {L"Dia (mm)", 80, true}, {L"Length (mm)", 110, true}, {L"Nos", 70, true}};
    c.summaryCols = {{L"Dia (mm)", 100}, {L"Nos", 80, true}, {L"Length (m)", 110, true}, {L"Weight (kg)", 110, true}};
    c.generate = computeColumns;
    c.seed = {{{"mark", "C1"}, {"width", "300"}, {"depth", "450"}, {"height", "3200"}, {"cover", "40"},
               {"stirrup_dia", "8"}, {"spacing", "150"}, {"hook_angle", "135"}, {"tie_type", "Closed"},
               {"bars", "12:4, 16:2, 20:4"}}};
    return c;
}

static ElementConfig beamsConfig() {
    ElementConfig c;
    c.navLabel = L"Beams"; c.glyph = L""; c.key = "beams";
    c.title = L"Beams";
    c.subtitle = L"Stirrups, flexural bars, extras, skin (IS 456 Cl. 26.5.1.3).";
    c.fields = {
        section(L"Identity & geometry"),
        textF("mark", L"Mark", L"B1", 2),
        textF("span", L"Span (mm)", L"4000", 2),
        textF("width", L"Width (mm)", L"230", 2),
        textF("depth", L"Depth (mm)", L"450", 2),
        textF("cover", L"Cover (mm)", L"25", 2),
        comboF("concrete_grade", L"Concrete", {L"M20", L"M25", L"M30", L"M35", L"M40"}, L"M25", 2),
        comboF("steel_grade", L"Steel", {L"Fe415", L"Fe500", L"Fe550"}, L"Fe500", 2),
        section(L"Stirrups"),
        diaF("stirrup_dia", L"Stirrup Ø", L"8", 2),
        textF("spacing_support", L"Spacing at support", L"100", 3),
        textF("spacing_middle", L"Spacing at mid", L"150", 3),
        comboF("legs", L"Legs", {L"2", L"4"}, L"2", 2),
        comboF("hook_angle", L"Hook", {L"90", L"135", L"180"}, L"135", 2),
        section(L"Flexural bars"),
        comboF("top_bar_type", L"Top bar type", {L"At Support", L"Full Span"}, L"At Support", 3),
        textF("top_bars", L"Top bars (dia:qty)", L"16:2", 4),
        textF("bottom_bars", L"Bottom bars (dia:qty)", L"16:3, 20:2", 5),
        section(L"Side face (skin) — when depth > 750 mm"),
        diaF("skin_dia", L"Skin Ø (mm)", L"", 3, true),
        textF("skin_spacing", L"Skin spacing (mm)", L"", 3),
    };
    c.extraPanels = {
        {ExtraPanelKind::Fixed, L"Extra bars (fixed length)", "extra_fixed"},
        {ExtraPanelKind::SpanFrac, L"Extra bars (fraction of span)", "extra_span"},
    };
    c.inputCols = {{L"Mark", 60}, {L"Span", 66, true}, {L"W", 50, true}, {L"D", 50, true}, {L"Top", 90}, {L"Bottom", 110}};
    c.inputKeys = {"mark", "span", "width", "depth", "top_bars", "bottom_bars"};
    c.bbsCols = {{L"Mark", 80}, {L"Role", 120}, {L"Dia (mm)", 80, true}, {L"Length (mm)", 110, true}, {L"Nos", 70, true}};
    c.summaryCols = {{L"Dia (mm)", 100}, {L"Nos", 80, true}, {L"Length (m)", 110, true}, {L"Weight (kg)", 110, true}};
    c.hasChecks = true;
    c.checkTitle = L"Detailing notes (IS 456)";
    c.checkCols = {{L"Note", 520}};
    c.generate = computeBeams;
    c.seed = {{{"mark", "B1"}, {"span", "4000"}, {"width", "230"}, {"depth", "450"}, {"cover", "25"},
               {"concrete_grade", "M25"}, {"steel_grade", "Fe500"}, {"stirrup_dia", "8"},
               {"spacing_support", "100"}, {"spacing_middle", "150"}, {"legs", "2"}, {"hook_angle", "135"},
               {"top_bar_type", "At Support"}, {"top_bars", "16:2"}, {"bottom_bars", "16:3, 20:2"}}};
    return c;
}

static ElementConfig slabsConfig() {
    ElementConfig c;
    c.navLabel = L"Slabs"; c.glyph = L""; c.key = "slabs";
    c.title = L"Slabs";
    c.subtitle = L"One-way / two-way mesh, crank length (IS 2502), and extras.";
    c.typeKey = "slab_type";
    c.fields = {
        section(L"Identity & type"),
        textF("mark", L"Mark", L"S1", 3),
        comboF("slab_type", L"Slab type", {L"One-Way", L"Two-Way"}, L"Two-Way", 3),
        comboF("concrete_grade", L"Concrete", {L"M20", L"M25", L"M30", L"M35", L"M40"}, L"M25", 3),
        comboF("steel_grade", L"Steel", {L"Fe250", L"Fe415", L"Fe500", L"Fe550"}, L"Fe415", 3),
        section(L"Geometry"),
        textF("span_x", L"Span X (mm)", L"3000", 3),
        textF("span_y", L"Span Y (mm)", L"4500", 3),
        textF("thickness", L"Thickness (mm)", L"125", 3),
        textF("cover", L"Cover (mm)", L"20", 3),
        section(L"Main reinforcement — X"),
        diaF("dia_x", L"Bar Ø-X", L"10", 3),
        textF("spacing_x", L"Spacing-X (mm)", L"150", 3),
        when(section(L"Main bars — Y (two-way)"), "slab_type", {L"Two-Way"}),
        when(section(L"Distribution bars — Y (one-way)"), "slab_type", {L"One-Way"}),
        diaF("dia_y", L"Bar Ø-Y", L"10", 3),
        textF("spacing_y", L"Spacing-Y (mm)", L"150", 3),
        when(section(L"Crank / bent-up on main bars (IS 2502)"), "slab_type", {L"One-Way", L"Two-Way"}),
        comboF("crank_count", L"Cranks per bar", {L"0", L"1", L"2"}, L"0", 3),
        textF("crank_rise", L"Crank rise (mm, blank = t−2c)", L"", 5),
    };
    c.extraPanels = {
        {ExtraPanelKind::Fixed, L"Extra bars (fixed length)", "extra_fixed"},
        {ExtraPanelKind::Mesh, L"Extra mesh (length × spacing)", "extra_mesh"},
    };
    c.inputCols = {{L"Mark", 60}, {L"Span X", 66, true}, {L"Span Y", 66, true}, {L"Thick", 56, true}, {L"Type", 84}};
    c.inputKeys = {"mark", "span_x", "span_y", "thickness", "slab_type"};
    c.bbsCols = {{L"Mark", 80}, {L"Role", 120}, {L"Dia (mm)", 80, true}, {L"Length (mm)", 110, true}, {L"Nos", 70, true}};
    c.summaryCols = {{L"Dia (mm)", 100}, {L"Nos", 80, true}, {L"Length (m)", 110, true}, {L"Weight (kg)", 110, true}};
    c.hasChecks = true;
    c.checkTitle = L"Minimum steel check (IS 456 Cl. 26.5.2.1)";
    c.checkCols = {{L"Mark", 70}, {L"Ast prov-X", 100, true}, {L"Ast min", 90, true}, {L"Status-X", 170},
                   {L"Ast prov-Y", 100, true}, {L"Status-Y", 170}};
    c.generate = computeSlabs;
    c.seed = {{{"mark", "S1"}, {"span_x", "3000"}, {"span_y", "4500"}, {"thickness", "125"}, {"cover", "20"},
               {"slab_type", "Two-Way"}, {"concrete_grade", "M25"}, {"steel_grade", "Fe415"},
               {"dia_x", "10"}, {"spacing_x", "150"}, {"dia_y", "10"}, {"spacing_y", "150"},
               {"crank_count", "0"}}};
    return c;
}

static ElementConfig footingsConfig() {
    ElementConfig c;
    c.navLabel = L"Footings"; c.glyph = L""; c.key = "footings";
    c.title = L"Footings";
    c.subtitle = L"Isolated, stepped, double, strip and raft — inputs follow footing type.";
    c.typeKey = "footing_type";
    c.fields = {
        section(L"Identity & type"),
        textF("mark", L"Mark", L"F1", 3),
        comboF("footing_type", L"Footing type",
               {L"Isolated", L"Stepped", L"Double", L"Strip", L"Raft"}, L"Isolated", 4),
        comboF("concrete_grade", L"Concrete", {L"M20", L"M25", L"M30", L"M35", L"M40"}, L"M25", 3),
        comboF("steel_grade", L"Steel", {L"Fe415", L"Fe500", L"Fe550"}, L"Fe500", 2),
        section(L"Plan & depth"),
        textF("length_l", L"Length L (mm)", L"2000", 3),
        textF("width_b", L"Width B (mm)", L"2000", 3),
        textF("depth", L"Depth (mm)", L"500", 3),
        textF("cover", L"Cover (mm)", L"50", 3),
        when(section(L"Column on footing"), "footing_type", {L"Isolated", L"Stepped", L"Double"}),
        when(textF("col_dim_l", L"Column dim-L", L"400", 3), "footing_type", {L"Isolated", L"Stepped", L"Double"}),
        when(textF("col_dim_b", L"Column dim-B", L"400", 3), "footing_type", {L"Isolated", L"Stepped", L"Double"}),
        when(section(L"Second column (double footing)"), "footing_type", {L"Double"}),
        when(textF("col2_dim_l", L"Col2 dim-L", L"", 3), "footing_type", {L"Double"}),
        when(textF("col2_dim_b", L"Col2 dim-B", L"", 3), "footing_type", {L"Double"}),
        when(section(L"Stepped arrangement"), "footing_type", {L"Stepped"}),
        when(textF("n_steps", L"Number of steps", L"2", 3), "footing_type", {L"Stepped"}),
        when(textF("step_height", L"Step height (mm, blank = D/n)", L"", 4), "footing_type", {L"Stepped"}),
        when(textF("top_length", L"Top plan L (mm, blank = col)", L"", 3), "footing_type", {L"Stepped"}),
        when(textF("top_width", L"Top plan B (mm, blank = col)", L"", 3), "footing_type", {L"Stepped"}),
        section(L"Bottom mesh"),
        diaF("dia_l", L"Bottom Ø-L", L"12", 3),
        textF("spacing_l", L"Bottom spacing-L", L"150", 3),
        diaF("dia_b", L"Bottom Ø-B", L"12", 3),
        textF("spacing_b", L"Bottom spacing-B", L"150", 3),
        when(section(L"Top mesh (optional)"), "footing_type", {L"Isolated", L"Double", L"Strip", L"Raft", L"Stepped"}),
        diaF("top_dia_l", L"Top Ø-L", L"", 3, true),
        textF("top_spacing_l", L"Top spacing-L", L"", 3),
        diaF("top_dia_b", L"Top Ø-B", L"", 3, true),
        textF("top_spacing_b", L"Top spacing-B", L"", 3),
    };
    c.extraPanels = {
        {ExtraPanelKind::Fixed, L"Extra bars (fixed length)", "extra_fixed"},
    };
    c.inputCols = {{L"Mark", 50}, {L"Type", 70}, {L"L", 60, true}, {L"B", 60, true}, {L"Depth", 60, true}};
    c.inputKeys = {"mark", "footing_type", "length_l", "width_b", "depth"};
    c.bbsCols = {{L"Mark", 80}, {L"Role", 120}, {L"Dia (mm)", 80, true}, {L"Length (mm)", 110, true}, {L"Nos", 70, true}};
    c.summaryCols = {{L"Dia (mm)", 100}, {L"Nos", 80, true}, {L"Length (m)", 110, true}, {L"Weight (kg)", 110, true}};
    c.hasChecks = true;
    c.checkTitle = L"Anchorage & minimum steel (IS 456 Cl. 26.2 / Cl. 34)";
    c.checkCols = {{L"Mark", 50}, {L"Ld-L", 60, true}, {L"Av-L", 60, true}, {L"Anch-L", 100},
                   {L"Ld-B", 60, true}, {L"Av-B", 60, true}, {L"Anch-B", 100},
                   {L"Ast-L", 60, true}, {L"Amin", 55, true}, {L"Min-L", 80}, {L"Min-B", 80}, {L"Note", 140}};
    c.generate = computeFootings;
    c.seed = {{{"mark", "F1"}, {"footing_type", "Isolated"}, {"length_l", "2000"}, {"width_b", "2000"},
               {"col_dim_l", "400"}, {"col_dim_b", "400"}, {"depth", "500"}, {"cover", "50"},
               {"concrete_grade", "M25"}, {"steel_grade", "Fe500"},
               {"dia_l", "12"}, {"spacing_l", "150"}, {"dia_b", "12"}, {"spacing_b", "150"}}};
    return c;
}

static ElementConfig wallsConfig() {
    ElementConfig c;
    c.navLabel = L"Walls"; c.glyph = L""; c.key = "walls";
    c.title = L"Retaining walls";
    c.subtitle = L"Stem and base mesh — toe optional. Tension face is user-selected.";
    c.typeKey = "include_toe";
    c.fields = {
        section(L"Identity & geometry"),
        textF("mark", L"Mark", L"RW1", 2),
        textF("wall_length", L"Wall length (mm)", L"5000", 3),
        textF("stem_h", L"Stem H (mm)", L"3000", 2),
        textF("stem_t", L"Stem thick (mm)", L"250", 2),
        textF("heel", L"Heel (mm)", L"1500", 2),
        comboF("include_toe", L"Include toe", {L"Yes", L"No"}, L"Yes", 2),
        when(textF("toe", L"Toe (mm)", L"600", 2), "include_toe", {L"Yes"}),
        textF("base_t", L"Base thick (mm)", L"400", 2),
        textF("cover", L"Cover (mm)", L"50", 2),
        section(L"Materials & tension face"),
        comboF("concrete_grade", L"Concrete", {L"M20", L"M25", L"M30", L"M35", L"M40"}, L"M25", 3),
        comboF("steel_grade", L"Steel", {L"Fe415", L"Fe500", L"Fe550"}, L"Fe500", 3),
        comboF("tension_face", L"Tension face", {L"Front", L"Back"}, L"Front", 3),
        section(L"Stem reinforcement"),
        diaF("stem_v_dia", L"Stem V Ø", L"12", 3),
        textF("stem_v_spacing", L"Stem V spacing", L"150", 3),
        diaF("stem_v_back_dia", L"Other face Ø", L"10", 3, true),
        textF("stem_v_back_spacing", L"Other face spacing", L"200", 3),
        diaF("stem_h_dia", L"Stem H Ø", L"10", 3),
        textF("stem_h_spacing", L"Stem H spacing", L"200", 3),
        section(L"Base reinforcement"),
        diaF("base_l_dia", L"Base long Ø", L"12", 3),
        textF("base_l_spacing", L"Base long spacing", L"150", 3),
        diaF("base_b_dia", L"Base trans Ø", L"12", 3),
        textF("base_b_spacing", L"Base trans spacing", L"150", 3),
        section(L"Links (optional)"),
        diaF("link_dia", L"Link Ø", L"", 3, true),
        textF("link_spacing", L"Link spacing", L"", 3),
        comboF("link_legs", L"Link legs", {L"2", L"4"}, L"2", 3),
    };
    c.extraPanels = {
        {ExtraPanelKind::Fixed, L"Extra bars (fixed length)", "extra_fixed"},
    };
    c.inputCols = {{L"Mark", 50}, {L"Length", 70, true}, {L"Stem H", 70, true}, {L"Stem t", 60, true}};
    c.inputKeys = {"mark", "wall_length", "stem_h", "stem_t"};
    c.bbsCols = {{L"Mark", 80}, {L"Role", 120}, {L"Dia (mm)", 80, true}, {L"Length (mm)", 110, true}, {L"Nos", 70, true}};
    c.summaryCols = {{L"Dia (mm)", 100}, {L"Nos", 80, true}, {L"Length (m)", 110, true}, {L"Weight (kg)", 110, true}};
    c.hasChecks = true;
    c.checkTitle = L"Minimum steel check (IS 456 Cl. 26.5.2.1)";
    c.checkCols = {{L"Mark", 60}, {L"Ast stem", 80, true}, {L"Amin stem", 80, true}, {L"Stem", 120},
                   {L"Ast base", 80, true}, {L"Amin base", 80, true}, {L"Base", 100}, {L"Note", 200}};
    c.generate = computeWalls;
    c.seed = {{{"mark", "RW1"}, {"wall_length", "5000"}, {"stem_h", "3000"}, {"stem_t", "250"},
               {"heel", "1500"}, {"include_toe", "Yes"}, {"toe", "600"}, {"base_t", "400"}, {"cover", "50"},
               {"concrete_grade", "M25"}, {"steel_grade", "Fe500"}, {"tension_face", "Front"},
               {"stem_v_dia", "12"}, {"stem_v_spacing", "150"}, {"stem_v_back_dia", "10"},
               {"stem_v_back_spacing", "200"}, {"stem_h_dia", "10"}, {"stem_h_spacing", "200"},
               {"base_l_dia", "12"}, {"base_l_spacing", "150"}, {"base_b_dia", "12"}, {"base_b_spacing", "150"},
               {"link_legs", "2"}}};
    return c;
}

// ------------------------------ lifecycle ------------------------------

bool MainWindow::create(HINSTANCE inst, int nCmdShow) {
    inst_ = inst;
    dpi_ = GetDpiForSystem();
    theme().init(dpi_);

    WNDCLASSEXW wc{sizeof(wc)};
    wc.lpfnWndProc = wndProcStatic;
    wc.hInstance = inst;
    wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
    wc.hbrBackground = nullptr;
    wc.lpszClassName = kClass;
    wc.hIcon = LoadIcon(nullptr, IDI_APPLICATION);
    RegisterClassExW(&wc);

    int w = theme().dp(1360), h = theme().dp(880);
    int x = (GetSystemMetrics(SM_CXSCREEN) - w) / 2;
    int y = (GetSystemMetrics(SM_CYSCREEN) - h) / 2;
    hwnd_ = CreateWindowExW(WS_EX_CONTROLPARENT, kClass, L"BBS Studio",
                            WS_OVERLAPPEDWINDOW | WS_CLIPCHILDREN, x, y, w, h, nullptr, nullptr, inst, this);
    if (!hwnd_) return false;

    int realDpi = GetDpiForWindow(hwnd_);
    if (realDpi != dpi_) { dpi_ = realDpi; theme().reload(dpi_); }

    applyWindowBackdrop(hwnd_, theme().c.dark);
    makeBrushes();
    buildPages();
    updateTitle();

    // accelerators
    ACCEL acc[] = {
        {FCONTROL | FVIRTKEY, 'N', ID_NEW},
        {FCONTROL | FVIRTKEY, 'O', ID_OPEN},
        {FCONTROL | FVIRTKEY, 'S', ID_SAVE},
        {FCONTROL | FSHIFT | FVIRTKEY, 'S', ID_SAVEAS},
        {FCONTROL | FVIRTKEY, '1', ID_PAGE0 + 0},
        {FCONTROL | FVIRTKEY, '2', ID_PAGE0 + 1},
        {FCONTROL | FVIRTKEY, '3', ID_PAGE0 + 2},
        {FCONTROL | FVIRTKEY, '4', ID_PAGE0 + 3},
        {FCONTROL | FVIRTKEY, '5', ID_PAGE0 + 4},
        {FCONTROL | FVIRTKEY, '6', ID_PAGE0 + 5},
        {FCONTROL | FVIRTKEY, '7', ID_PAGE0 + 6},
    };
    accel_ = CreateAcceleratorTableW(acc, ARRAYSIZE(acc));

    relayout();
    for (size_t i = 0; i < pages_.size(); ++i) pages_[i]->show((int)i == active_);
    ShowWindow(hwnd_, nCmdShow);
    UpdateWindow(hwnd_);
    return true;
}

void MainWindow::buildPages() {
    ctx_.mainHwnd = hwnd_;
    ctx_.onDataChanged = [this]() {
        if (auto* d = dynamic_cast<DashboardPage*>(pages_[0].get())) d->refresh();
    };
    ctx_.markDirty = [this]() { dirty_ = true; updateTitle(); };

    pages_.push_back(std::make_unique<DashboardPage>());
    pages_.push_back(std::make_unique<ElementPage>(columnsConfig()));
    pages_.push_back(std::make_unique<ElementPage>(beamsConfig()));
    pages_.push_back(std::make_unique<ElementPage>(slabsConfig()));
    pages_.push_back(std::make_unique<ElementPage>(footingsConfig()));
    pages_.push_back(std::make_unique<ElementPage>(wallsConfig()));
    pages_.push_back(std::make_unique<SettingsPage>());
    for (auto& p : pages_) p->create(hwnd_, &ctx_);
    active_ = 0;
}

void MainWindow::makeBrushes() {
    if (controlBrush_) DeleteObject(controlBrush_);
    if (cardBrush_) DeleteObject(cardBrush_);
    controlBrush_ = CreateSolidBrush(Theme::toRef(theme().c.controlBg));
    cardBrush_ = CreateSolidBrush(Theme::toRef(theme().c.card));
}

void MainWindow::updateTitle() {
    std::wstring t = L"BBS Studio  —  " + projectName_;
    if (dirty_) t += L"  •";
    SetWindowTextW(hwnd_, t.c_str());
}

// ------------------------------ layout ------------------------------

RECT MainWindow::navRect() const {
    RECT rc; GetClientRect(hwnd_, &rc);
    int navW = navCompact_ ? theme().dp(60) : theme().dp(224);
    return {0, 0, navW, rc.bottom};
}
RECT MainWindow::contentRect() const {
    RECT rc; GetClientRect(hwnd_, &rc);
    return {navRect().right, 0, rc.right, rc.bottom};
}

void MainWindow::relayout() {
    RECT rc; GetClientRect(hwnd_, &rc);
    navCompact_ = (rc.right - rc.left) < theme().dp(960);
    auto& t = theme();
    RECT nav = navRect();

    navItemRects_.clear();
    int y = t.dp(74);
    int itemH = t.dp(44), gap = t.dp(4), mgn = t.dp(8);
    for (size_t i = 0; i < pages_.size(); ++i) {
        navItemRects_.push_back({mgn, y, nav.right - mgn, y + itemH});
        y += itemH + gap;
    }
    int bY = rc.bottom - t.dp(12) - t.dp(36);
    navSaveR_ = {mgn, bY, nav.right - mgn, bY + t.dp(36)}; bY -= t.dp(40);
    navOpenR_ = {mgn, bY, nav.right - mgn, bY + t.dp(36)}; bY -= t.dp(40);
    navNewR_  = {mgn, bY, nav.right - mgn, bY + t.dp(36)};

    if (!pages_.empty()) pages_[active_]->layout(contentRect());
}

// ------------------------------ nav paint & hit test ------------------------------

void MainWindow::paintNav(Graphics& g, RECT nav) {
    auto& t = theme();
    // rail background + right divider
    SolidBrush bg(t.c.navBg);
    g.FillRectangle(&bg, (INT)nav.left, (INT)nav.top, (INT)(nav.right - nav.left), (INT)(nav.bottom - nav.top));
    Pen div(t.c.divider, 1.0f);
    g.DrawLine(&div, (float)nav.right - 0.5f, (float)nav.top, (float)nav.right - 0.5f, (float)nav.bottom);

    // brand
    int mgn = t.dp(8);
    RectF brand((float)(nav.left + mgn + t.dp(4)), (float)t.dp(18), (float)t.dp(34), (float)t.dp(34));
    fillRound(g, brand, (float)t.dp(9), t.c.accent);
    drawText(g, L"B", brand, ts(18, FW_SEMIBOLD, t.c.textOnAccent), Align::Center, Align::Center);
    if (!navCompact_) {
        RectF bt((float)(nav.left + mgn + t.dp(46)), (float)t.dp(18), (float)(nav.right - nav.left - t.dp(54)), (float)t.dp(34));
        drawText(g, L"BBS Studio", bt, ts(16, FW_SEMIBOLD, t.c.textPrimary), Align::Near, Align::Center);
    }

    // items
    for (size_t i = 0; i < pages_.size(); ++i) {
        RECT r = navItemRects_[i];
        bool sel = ((int)i == active_), hot = (navHot_ == (int)i);
        bool kbd = navKeyboardFocus_ && ((int)i == navFocus_);
        if (sel) fillRound(g, toRectF(r), (float)t.dp(6), t.c.navSelectedFill);
        else if (hot) fillRound(g, toRectF(r), (float)t.dp(6), t.c.navItemHover);
        if (kbd) strokeRound(g, toRectF(r), (float)t.dp(6), t.c.controlBorderFocus, 2.0f);
        if (sel) {
            RectF ind((float)r.left, (float)(r.top + (r.bottom - r.top) / 2 - t.dp(9)), (float)t.dp(3), (float)t.dp(18));
            fillRound(g, ind, 1.5f, t.c.navIndicator);
        }
        Color txt = sel ? t.c.textPrimary : t.c.textSecondary;
        RectF gl((float)(r.left + t.dp(10)), (float)r.top, (float)t.dp(28), (float)(r.bottom - r.top));
        drawGlyph(g, pages_[i]->glyph(), gl, t.dp(16), sel ? t.c.accent : t.c.textSecondary, Align::Center, Align::Center);
        if (!navCompact_) {
            RectF lt((float)(r.left + t.dp(44)), (float)r.top, (float)(r.right - r.left - t.dp(52)), (float)(r.bottom - r.top));
            drawText(g, pages_[i]->navLabel(), lt, ts(13, sel ? FW_SEMIBOLD : FW_NORMAL, txt), Align::Near, Align::Center);
        }
    }

    // footer file actions
    struct FB { RECT r; const wchar_t* glyph; const wchar_t* label; int code; };
    FB fbs[] = {{navNewR_, L"", L"New", NAV_NEW}, {navOpenR_, L"", L"Open", NAV_OPEN},
                {navSaveR_, L"", L"Save", NAV_SAVE}};
    for (auto& f : fbs) {
        bool hot = (navHot_ == f.code);
        if (hot) fillRound(g, toRectF(f.r), (float)t.dp(6), t.c.navItemHover);
        RectF gl((float)(f.r.left + t.dp(10)), (float)f.r.top, (float)t.dp(24), (float)(f.r.bottom - f.r.top));
        drawGlyph(g, f.glyph, gl, t.dp(15), t.c.textSecondary, Align::Center, Align::Center);
        if (!navCompact_) {
            RectF lt((float)(f.r.left + t.dp(42)), (float)f.r.top, (float)(f.r.right - f.r.left - t.dp(50)), (float)(f.r.bottom - f.r.top));
            drawText(g, f.label, lt, ts(13, FW_NORMAL, t.c.textSecondary), Align::Near, Align::Center);
        }
    }
}

int MainWindow::navHitTest(POINT p) const {
    for (size_t i = 0; i < navItemRects_.size(); ++i)
        if (PtInRect(&navItemRects_[i], p)) return (int)i;
    if (PtInRect(&navNewR_, p)) return NAV_NEW;
    if (PtInRect(&navOpenR_, p)) return NAV_OPEN;
    if (PtInRect(&navSaveR_, p)) return NAV_SAVE;
    return -2;
}

void MainWindow::switchPage(int idx) {
    if (idx == active_ || idx < 0 || idx >= (int)pages_.size()) return;
    pages_[active_]->show(false);
    active_ = idx;
    pages_[active_]->layout(contentRect());
    pages_[active_]->show(true);
    InvalidateRect(hwnd_, nullptr, FALSE);
}

// ------------------------------ paint ------------------------------

void MainWindow::paint() {
    PAINTSTRUCT ps;
    HDC hdc = BeginPaint(hwnd_, &ps);
    RECT rc; GetClientRect(hwnd_, &rc);
    int W = rc.right, H = rc.bottom;

    HDC mem = CreateCompatibleDC(hdc);
    HBITMAP bmp = CreateCompatibleBitmap(hdc, W, H);
    HBITMAP old = (HBITMAP)SelectObject(mem, bmp);
    {
        Graphics g(mem);
        g.SetSmoothingMode(SmoothingModeAntiAlias);
        SolidBrush appbg(theme().c.appBg);
        g.FillRectangle(&appbg, 0, 0, W, H);
        pages_[active_]->paint(g, contentRect());
        paintNav(g, navRect());
    }
    BitBlt(hdc, 0, 0, W, H, mem, 0, 0, SRCCOPY);
    SelectObject(mem, old);
    DeleteObject(bmp);
    DeleteDC(mem);
    EndPaint(hwnd_, &ps);
}

// ------------------------------ project ops ------------------------------

void MainWindow::collectProject(bbs::ProjectData& pd) {
    pd.name = fromW(projectName_);
    pd.settings = ctx_.settings;
    for (auto& p : pages_) p->collect(pd);
}

void MainWindow::applyProject(const bbs::ProjectData& pd) {
    ctx_.settings = pd.settings;
    ctx_.lastColumn.clear(); ctx_.lastBeam.clear(); ctx_.lastSlab.clear();
    ctx_.lastFooting.clear(); ctx_.lastWall.clear();
    for (auto& p : pages_) p->applyData(pd);
    if (auto* d = dynamic_cast<DashboardPage*>(pages_[0].get())) d->refresh();
    if (auto* s = dynamic_cast<SettingsPage*>(pages_.back().get())) s->syncFromSettings();
    InvalidateRect(hwnd_, nullptr, FALSE);
}

void MainWindow::newProject() {
    if (dirty_ && !confirmBox(hwnd_, L"Discard unsaved changes and start a new project?")) return;
    bbs::ProjectData pd;  // defaults
    applyProject(pd);
    projectPath_.clear();
    projectName_ = L"Untitled Project";
    dirty_ = false;
    updateTitle();
}

void MainWindow::openProject() {
    std::wstring path = openFileDialog(hwnd_, L"BBS Project (*.bbsproj)\0*.bbsproj\0All files\0*.*\0");
    if (path.empty()) return;
    bbs::ProjectData pd;
    std::string err;
    if (!bbs::load_project(path, pd, err)) { errorBox(hwnd_, toW(err), L"Could not open project"); return; }
    applyProject(pd);
    projectPath_ = path;
    size_t slash = path.find_last_of(L"\\/");
    projectName_ = (slash == std::wstring::npos) ? path : path.substr(slash + 1);
    dirty_ = false;
    updateTitle();
}

void MainWindow::saveProject(bool saveAs) {
    std::wstring path = projectPath_;
    if (saveAs || path.empty()) {
        path = saveFileDialog(hwnd_, L"BBS Project (*.bbsproj)\0*.bbsproj\0", L"bbsproj", L"project");
        if (path.empty()) return;
    }
    bbs::ProjectData pd;
    collectProject(pd);
    std::string err;
    if (!bbs::save_project(path, pd, err)) { errorBox(hwnd_, toW(err), L"Save failed"); return; }
    projectPath_ = path;
    size_t slash = path.find_last_of(L"\\/");
    projectName_ = (slash == std::wstring::npos) ? path : path.substr(slash + 1);
    dirty_ = false;
    updateTitle();
    infoBox(hwnd_, L"Project saved to:\n" + path);
}

void MainWindow::exportReport() {
    bbs::ProjectData pd;
    collectProject(pd);
    auto sections = buildReportSections(pd);
    if (sections.empty()) { infoBox(hwnd_, L"Nothing to report yet. Add elements first."); return; }
    std::wstring path = saveFileDialog(hwnd_, L"HTML report (*.html)\0*.html\0", L"html",
                                       projectName_ == L"Untitled Project" ? L"bbs_report" : projectName_);
    if (path.empty()) return;
    std::string err;
    if (bbs::export_html_report(fromW(projectName_), sections, path, err))
        infoBox(hwnd_, L"Report saved to:\n" + path);
    else
        errorBox(hwnd_, toW(err));
}

void MainWindow::reloadThemeAndFonts() {
    theme().reload(dpi_);
    makeBrushes();
    EnumChildWindows(hwnd_, [](HWND child, LPARAM) -> BOOL {
        wchar_t cls[64];
        GetClassNameW(child, cls, 64);
        if (wcscmp(cls, L"BUTTON") != 0)  // owner-draw buttons paint via GDI+
            SendMessageW(child, WM_SETFONT, (WPARAM)theme().fBody, TRUE);
        SetWindowTheme(child, theme().c.dark ? L"DarkMode_Explorer" : L"Explorer", nullptr);
        return TRUE;
    }, 0);
    applyWindowBackdrop(hwnd_, theme().c.dark);
}

// ------------------------------ window proc ------------------------------

LRESULT CALLBACK MainWindow::wndProcStatic(HWND h, UINT m, WPARAM w, LPARAM l) {
    MainWindow* self = nullptr;
    if (m == WM_NCCREATE) {
        self = (MainWindow*)((CREATESTRUCTW*)l)->lpCreateParams;
        SetWindowLongPtrW(h, GWLP_USERDATA, (LONG_PTR)self);
        self->hwnd_ = h;
    } else {
        self = (MainWindow*)GetWindowLongPtrW(h, GWLP_USERDATA);
    }
    return self ? self->wndProc(m, w, l) : DefWindowProcW(h, m, w, l);
}

LRESULT MainWindow::wndProc(UINT m, WPARAM w, LPARAM l) {
    switch (m) {
        case WM_ERASEBKGND: return 1;
        case WM_PAINT: paint(); return 0;
        case WM_SIZE: relayout(); InvalidateRect(hwnd_, nullptr, FALSE); return 0;

        case WM_GETMINMAXINFO: {
            auto* mmi = (MINMAXINFO*)l;
            mmi->ptMinTrackSize.x = theme().dp(900);
            mmi->ptMinTrackSize.y = theme().dp(600);
            return 0;
        }
        case WM_MOUSEMOVE: {
            POINT p{GET_X_LPARAM(l), GET_Y_LPARAM(l)};
            int hit = navHitTest(p);
            if (hit != navHot_) {
                navHot_ = hit;
                TRACKMOUSEEVENT tme{sizeof(tme), TME_LEAVE, hwnd_, 0};
                TrackMouseEvent(&tme);
                RECT nav = navRect();
                InvalidateRect(hwnd_, &nav, FALSE);
            }
            return 0;
        }
        case WM_MOUSELEAVE:
            if (navHot_ != -2) { navHot_ = -2; InvalidateRect(hwnd_, nullptr, FALSE); }
            return 0;
        case WM_LBUTTONDOWN: {
            POINT p{GET_X_LPARAM(l), GET_Y_LPARAM(l)};
            int hit = navHitTest(p);
            if (hit >= 0 && hit < (int)pages_.size()) {
                navFocus_ = hit;
                navKeyboardFocus_ = true;
                switchPage(hit);
            }
            else if (hit == NAV_NEW) newProject();
            else if (hit == NAV_OPEN) openProject();
            else if (hit == NAV_SAVE) saveProject(false);
            return 0;
        }
        case WM_KEYDOWN: {
            // Keyboard navigation for the painted nav rail (WCAG 2.1.1 / 2.4.7).
            if (w == VK_F6) {
                navKeyboardFocus_ = !navKeyboardFocus_;
                if (navKeyboardFocus_) navFocus_ = active_;
                InvalidateRect(hwnd_, nullptr, FALSE);
                return 0;
            }
            if (!navKeyboardFocus_) break;
            if (w == VK_DOWN || w == VK_RIGHT) {
                navFocus_ = (navFocus_ + 1) % (int)pages_.size();
                InvalidateRect(hwnd_, nullptr, FALSE);
                return 0;
            }
            if (w == VK_UP || w == VK_LEFT) {
                navFocus_ = (navFocus_ - 1 + (int)pages_.size()) % (int)pages_.size();
                InvalidateRect(hwnd_, nullptr, FALSE);
                return 0;
            }
            if (w == VK_RETURN || w == VK_SPACE) {
                switchPage(navFocus_);
                return 0;
            }
            if (w == VK_ESCAPE) {
                navKeyboardFocus_ = false;
                InvalidateRect(hwnd_, nullptr, FALSE);
                return 0;
            }
            break;
        }
        case WM_COMMAND: {
            int id = LOWORD(w), code = HIWORD(w);
            HWND ctl = (HWND)l;
            if (ctl == nullptr) {  // menu / accelerator / synthetic
                switch (id) {
                    case ID_NEW: newProject(); break;
                    case ID_OPEN: openProject(); break;
                    case ID_SAVE: saveProject(false); break;
                    case ID_SAVEAS: saveProject(true); break;
                    case ID_REPORT: exportReport(); break;
                    case ID_DIAS_CHANGED:
                        for (auto& p : pages_) p->onSettingsChanged();
                        break;
                    default:
                        if (id >= ID_PAGE0 && id < ID_PAGE0 + (int)pages_.size()) switchPage(id - ID_PAGE0);
                }
                return 0;
            }
            pages_[active_]->onCommand(id, code, ctl);
            return 0;
        }
        case WM_DRAWITEM:
            paintButton((const DRAWITEMSTRUCT*)l);
            return TRUE;
        case WM_NOTIFY: {
            LRESULT res = 0;
            if (pages_[active_]->onNotify((NMHDR*)l, res)) return res;
            break;
        }
        case WM_CTLCOLOREDIT:
        case WM_CTLCOLORLISTBOX: {
            HDC dc = (HDC)w;
            SetTextColor(dc, Theme::toRef(theme().c.textPrimary));
            SetBkColor(dc, Theme::toRef(theme().c.controlBg));
            return (LRESULT)controlBrush_;
        }
        case WM_DPICHANGED: {
            dpi_ = HIWORD(w);
            reloadThemeAndFonts();
            RECT* nr = (RECT*)l;
            SetWindowPos(hwnd_, nullptr, nr->left, nr->top, nr->right - nr->left, nr->bottom - nr->top,
                         SWP_NOZORDER | SWP_NOACTIVATE);
            relayout();
            InvalidateRect(hwnd_, nullptr, TRUE);
            return 0;
        }
        case WM_CLOSE:
            if (dirty_ && !confirmBox(hwnd_, L"You have unsaved changes. Close without saving?")) return 0;
            DestroyWindow(hwnd_);
            return 0;
        case WM_DESTROY:
            if (controlBrush_) DeleteObject(controlBrush_);
            if (cardBrush_) DeleteObject(cardBrush_);
            theme().destroy();
            PostQuitMessage(0);
            return 0;
    }
    return DefWindowProcW(hwnd_, m, w, l);
}

}  // namespace ui
