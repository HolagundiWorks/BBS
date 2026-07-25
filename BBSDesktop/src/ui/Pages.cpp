// Pages.cpp — Dashboard and Settings page implementations.
#include "Pages.h"

#include "../core/Export.h"
#include "../core/Parse.h"
#include "Dialogs.h"

#include <sstream>

namespace ui {

using namespace Gdiplus;
static TextStyle ts(int px, int weight, Color c) { return TextStyle{theme().dp(px), weight, c}; }

// ============================================================ Dashboard

void DashboardPage::create(HWND parent, AppContext* ctx) {
    parent_ = parent;
    ctx_ = ctx;
    lvSummary_ = createListView(parent, nextControlId());
    setListColumns(lvSummary_, {{L"Diameter (mm)", 130}, {L"Bars (nos)", 110, true},
                                {L"Length (m)", 120, true}, {L"Weight (kg)", 120, true}});
    btnReport_ = createButton(parent, nextControlId(), L"Export report", ButtonKind::Primary, L"");
    btnCsv_ = createButton(parent, nextControlId(), L"Export CSV", ButtonKind::Default, L"");
    refresh();
}

void DashboardPage::refresh() {
    std::vector<std::vector<bbs::SummaryRow>> lists;
    for (auto* s : {&ctx_->lastColumn, &ctx_->lastBeam, &ctx_->lastSlab, &ctx_->lastFooting, &ctx_->lastWall})
        if (!s->empty()) lists.push_back(*s);
    merged_ = lists.empty() ? std::vector<bbs::SummaryRow>{} : bbs::merge_summaries(lists);

    summaryRows_.clear();
    for (const auto& r : merged_)
        summaryRows_.push_back({toW(r.dia), std::to_wstring(r.nos),
                                toW(bbs::format_num(r.total_length_m)),
                                toW(bbs::format_num(r.weight_kg))});
    if (lvSummary_) setListRows(lvSummary_, summaryRows_);

    // KPIs from the TOTAL row.
    double weight = 0, length = 0;
    int bars = 0, dias = (int)merged_.size() > 0 ? (int)merged_.size() - 1 : 0;
    if (!merged_.empty()) {
        const auto& tot = merged_.back();
        weight = tot.weight_kg;
        length = tot.total_length_m;
        bars = tot.nos;
    }
    kpis_[0] = {L"", toW(bbs::format_num(weight)), L"Steel weight (kg)"};
    kpis_[1] = {L"", std::to_wstring(bars), L"Total bars"};
    kpis_[2] = {L"", toW(bbs::format_num(length)), L"Cutting length (m)"};
    kpis_[3] = {L"", std::to_wstring(dias), L"Bar diameters"};

    if (parent_) InvalidateRect(parent_, &content_, FALSE);
}

void DashboardPage::onCommand(int, int code, HWND ctl) {
    if (code != BN_CLICKED) return;
    if (ctl == btnCsv_) {
        if (summaryRows_.empty()) { infoBox(parent_, L"Generate at least one element first."); return; }
        std::wstring path = saveFileDialog(parent_, L"CSV files\0*.csv\0", L"csv", L"project_summary");
        if (path.empty()) return;
        std::vector<std::string> headers{"Dia (mm)", "Nos", "Total Length (m)", "Weight (kg)"};
        std::vector<std::vector<std::string>> body;
        for (const auto& r : summaryRows_) {
            std::vector<std::string> c;
            for (const auto& cell : r) c.push_back(fromW(cell));
            body.push_back(c);
        }
        std::string err;
        if (bbs::export_table_csv(headers, body, path, err)) infoBox(parent_, L"Saved to:\n" + path);
        else errorBox(parent_, toW(err));
    } else if (ctl == btnReport_) {
        // MainWindow owns full-report export; signal via WM_COMMAND id.
        SendMessageW(ctx_->mainHwnd, WM_COMMAND, MAKEWPARAM(0xB001, 0), 0);
    }
}

bool DashboardPage::onNotify(NMHDR* hdr, LRESULT& res) {
    if (hdr->code == NM_CUSTOMDRAW) {
        if (hdr->hwndFrom == lvSummary_) {
            res = handleListCustomDraw(lvSummary_, (NMLVCUSTOMDRAW*)hdr);
            return true;
        }
        if (lvSummary_ && hdr->hwndFrom == ListView_GetHeader(lvSummary_)) {
            res = handleHeaderCustomDraw((NMCUSTOMDRAW*)hdr);
            return true;
        }
    }
    return false;
}

void DashboardPage::layout(RECT content) {
    auto& t = theme();
    content_ = content;
    int P = t.dp(24), G = t.dp(16), pad = t.dp(20), headerH = t.dp(58);
    int left0 = content.left + P;
    int innerW = (content.right - content.left) - 2 * P;
    int top = content.top + P;
    rcHeader_ = {left0, top, content.right - P, top + headerH};

    int kpiY = rcHeader_.bottom + t.dp(6);
    int kpiH = t.dp(104);
    int kpiW = (innerW - 3 * G) / 4;
    for (int i = 0; i < 4; ++i) {
        int x = left0 + i * (kpiW + G);
        rcKpi_[i] = {x, kpiY, x + kpiW, kpiY + kpiH};
    }

    int rowY = kpiY + kpiH + G;
    int bottom = content.bottom - P;
    int sumW = (int)((innerW - G) * 8.0 / 12.0);
    rcSummary_ = {left0, rowY, left0 + sumW, bottom};
    rcActions_ = {left0 + sumW + G, rowY, content.right - P, bottom};

    int lvTop = rcSummary_.top + pad + t.dp(22) + t.dp(8);
    MoveWindow(lvSummary_, rcSummary_.left + pad, lvTop, rcSummary_.right - pad - (rcSummary_.left + pad),
               rcSummary_.bottom - pad - lvTop, TRUE);

    int bw = rcActions_.right - rcActions_.left - 2 * pad;
    int bx = rcActions_.left + pad, by = rcActions_.top + pad + t.dp(22) + t.dp(14);
    MoveWindow(btnReport_, bx, by, bw, t.dp(38), TRUE);
    MoveWindow(btnCsv_, bx, by + t.dp(46), bw, t.dp(36), TRUE);
}

void DashboardPage::paint(Graphics& g, RECT content) {
    auto& t = theme();
    int pad = t.dp(20);

    drawText(g, title(), RectF((float)rcHeader_.left, (float)rcHeader_.top, (float)(content.right - rcHeader_.left - t.dp(24)), (float)t.dp(30)),
             ts(22, FW_SEMIBOLD, t.c.textPrimary));
    drawText(g, subtitle(), RectF((float)rcHeader_.left, (float)(rcHeader_.top + t.dp(32)), (float)(content.right - rcHeader_.left - t.dp(24)), (float)t.dp(20)),
             ts(13, FW_NORMAL, t.c.textSecondary));

    // KPI cards — accent tab + large value + label (no icon-font dependency).
    Color accents[4] = {t.c.accent, t.c.success, t.c.accent, t.c.textSecondary};
    for (int i = 0; i < 4; ++i) {
        drawCard(g, rcKpi_[i]);
        RECT r = rcKpi_[i];
        RectF tab((float)(r.left + pad), (float)(r.top + pad), (float)t.dp(28), (float)t.dp(4));
        fillRound(g, tab, 2.0f, accents[i]);
        RectF val((float)(r.left + pad), (float)(r.top + pad + t.dp(16)), (float)(r.right - r.left - 2 * pad), (float)t.dp(40));
        drawText(g, kpis_[i].value, val, ts(30, FW_SEMIBOLD, t.c.textPrimary), Align::Near, Align::Near);
        RectF lab((float)(r.left + pad), (float)(r.top + pad + t.dp(62)), (float)(r.right - r.left - 2 * pad), (float)t.dp(18));
        drawText(g, kpis_[i].label, lab, ts(12, FW_NORMAL, t.c.textSecondary));
    }

    drawCard(g, rcSummary_);
    drawText(g, L"Project steel summary by diameter",
             RectF((float)(rcSummary_.left + pad), (float)(rcSummary_.top + pad - t.dp(2)), (float)(rcSummary_.right - rcSummary_.left - 2 * pad), (float)t.dp(24)),
             ts(15, FW_SEMIBOLD, t.c.textPrimary));

    drawCard(g, rcActions_);
    drawText(g, L"Deliverables",
             RectF((float)(rcActions_.left + pad), (float)(rcActions_.top + pad - t.dp(2)), (float)(rcActions_.right - rcActions_.left - 2 * pad), (float)t.dp(24)),
             ts(15, FW_SEMIBOLD, t.c.textPrimary));

    int noteY = rcActions_.top + pad + t.dp(22) + t.dp(14) + t.dp(46) + t.dp(36) + t.dp(18);
    RectF note((float)(rcActions_.left + pad), (float)noteY, (float)(rcActions_.right - rcActions_.left - 2 * pad), (float)(rcActions_.bottom - noteY - pad));
    std::wstring msg =
        L"Export a client-ready HTML report combining every schedule, or a consolidated CSV of the "
        L"project steel total.\n\nAll quantities are IS 456-derived estimates — cross-check against "
        L"structural drawings before construction.";
    drawText(g, msg, note, ts(12, FW_NORMAL, t.c.textTertiary), Align::Near, Align::Near, true);

    if (merged_.empty()) {
        RectF hint((float)(rcSummary_.left + pad), (float)(rcSummary_.top + t.dp(70)), (float)(rcSummary_.right - rcSummary_.left - 2 * pad), (float)t.dp(40));
        drawText(g, L"No schedules generated yet. Open an element tab, add rows, and generate its BBS.",
                 hint, ts(13, FW_NORMAL, t.c.textTertiary), Align::Near, Align::Near, true);
    }
}

void DashboardPage::show(bool v) {
    int cmd = v ? SW_SHOW : SW_HIDE;
    for (HWND h : {lvSummary_, btnReport_, btnCsv_}) if (h) ShowWindow(h, cmd);
}

// ============================================================ Settings

void SettingsPage::create(HWND parent, AppContext* ctx) {
    parent_ = parent;
    ctx_ = ctx;
    editDia_ = createEdit(parent, nextControlId(), L"");
    btnApply_ = createButton(parent, nextControlId(), L"Apply", ButtonKind::Primary, L"");
    syncFromSettings();
}

void SettingsPage::syncFromSettings() {
    std::wstring s;
    for (size_t i = 0; i < ctx_->settings.diameters.size(); ++i) {
        if (i) s += L", ";
        s += std::to_wstring(ctx_->settings.diameters[i]);
    }
    if (editDia_) setText(editDia_, s);
}

void SettingsPage::onCommand(int, int code, HWND ctl) {
    if (ctl == btnApply_ && code == BN_CLICKED) {
        std::string text = fromW(getText(editDia_));
        std::vector<int> dias;
        std::stringstream ss(text);
        std::string tok;
        while (std::getline(ss, tok, ',')) {
            int v = std::atoi(tok.c_str());
            if (v > 0) dias.push_back(v);
        }
        if (dias.empty()) { errorBox(parent_, L"Enter a comma-separated list of diameters, e.g. 8, 10, 12, 16."); return; }
        ctx_->settings.diameters = dias;
        if (ctx_->markDirty) ctx_->markDirty();
        // Refresh diameter dropdowns on every element page.
        if (ctx_->mainHwnd) SendMessageW(ctx_->mainHwnd, WM_COMMAND, MAKEWPARAM(0xB010, 0), 0);
        infoBox(parent_, L"Diameter list updated.");
    } else if (ctl == editDia_ && code == EN_SETFOCUS) {
        diaFocused_ = true; InvalidateRect(parent_, &rcDia_, FALSE);
    } else if (ctl == editDia_ && code == EN_KILLFOCUS) {
        diaFocused_ = false; InvalidateRect(parent_, &rcDia_, FALSE);
    }
}

void SettingsPage::layout(RECT content) {
    auto& t = theme();
    content_ = content;
    int P = t.dp(24), G = t.dp(16), pad = t.dp(20), headerH = t.dp(58);
    int left0 = content.left + P;
    int innerW = (content.right - content.left) - 2 * P;
    int top = content.top + P;
    rcHeader_ = {left0, top, content.right - P, top + headerH};

    int rowY = rcHeader_.bottom + t.dp(6);
    int colW = (innerW - G) / 2;
    rcDia_ = {left0, rowY, left0 + colW, rowY + t.dp(180)};
    rcRef_ = {left0 + colW + G, rowY, content.right - P, rowY + t.dp(260)};
    rcAbout_ = {left0, rcDia_.bottom + G, left0 + colW, rcDia_.bottom + G + t.dp(180)};

    int fx = rcDia_.left + pad, fy = rcDia_.top + pad + t.dp(24) + t.dp(30) + t.dp(6);
    int fw = rcDia_.right - pad - fx;
    diaFieldR_ = {fx, fy, fx + fw, fy + t.dp(32)};
    int eh = t.dp(20);
    MoveWindow(editDia_, fx + t.dp(10), fy + (t.dp(32) - eh) / 2, fw - t.dp(20), eh, TRUE);
    MoveWindow(btnApply_, fx, fy + t.dp(44), t.dp(120), t.dp(34), TRUE);
}

static void refLine(Graphics& g, float x, float& y, float w, const std::wstring& k, const std::wstring& v) {
    auto& t = theme();
    drawText(g, k, RectF(x, y, w * 0.6f, (float)t.dp(20)), ts(12, FW_NORMAL, t.c.textSecondary));
    drawText(g, v, RectF(x + w * 0.6f, y, w * 0.4f, (float)t.dp(20)), ts(12, FW_SEMIBOLD, t.c.textPrimary));
    y += t.dp(22);
}

void SettingsPage::paint(Graphics& g, RECT content) {
    auto& t = theme();
    int pad = t.dp(20);
    drawText(g, title(), RectF((float)rcHeader_.left, (float)rcHeader_.top, (float)(content.right - rcHeader_.left - t.dp(24)), (float)t.dp(30)),
             ts(22, FW_SEMIBOLD, t.c.textPrimary));
    drawText(g, subtitle(), RectF((float)rcHeader_.left, (float)(rcHeader_.top + t.dp(32)), (float)(content.right - rcHeader_.left - t.dp(24)), (float)t.dp(20)),
             ts(13, FW_NORMAL, t.c.textSecondary));

    // Diameters card
    drawCard(g, rcDia_);
    drawText(g, L"Bar diameters in use",
             RectF((float)(rcDia_.left + pad), (float)(rcDia_.top + pad - t.dp(2)), (float)(rcDia_.right - rcDia_.left - 2 * pad), (float)t.dp(24)),
             ts(15, FW_SEMIBOLD, t.c.textPrimary));
    drawText(g, L"Comma-separated list, e.g. 8, 10, 12, 16, 20, 25",
             RectF((float)(rcDia_.left + pad), (float)(rcDia_.top + pad + t.dp(26)), (float)(rcDia_.right - rcDia_.left - 2 * pad), (float)t.dp(18)),
             ts(12, FW_NORMAL, t.c.textTertiary));
    paintFieldContainer(g, diaFieldR_, diaFocused_);

    // Reference card
    drawCard(g, rcRef_);
    drawText(g, L"Engine reference values",
             RectF((float)(rcRef_.left + pad), (float)(rcRef_.top + pad - t.dp(2)), (float)(rcRef_.right - rcRef_.left - 2 * pad), (float)t.dp(24)),
             ts(15, FW_SEMIBOLD, t.c.textPrimary));
    float x = (float)(rcRef_.left + pad), y = (float)(rcRef_.top + pad + t.dp(30)), w = (float)(rcRef_.right - rcRef_.left - 2 * pad);
    drawText(g, L"Hook allowance (× dia)", RectF(x, y, w, (float)t.dp(18)), ts(12, FW_SEMIBOLD, t.c.accent)); y += t.dp(22);
    for (auto& kv : ctx_->settings.hook_allowance)
        refLine(g, x, y, w, std::to_wstring(kv.first) + L"°", toW(bbs::format_num(kv.second)) + L"d");
    y += t.dp(6);
    drawText(g, L"Bond stress τbd (N/mm²)", RectF(x, y, w, (float)t.dp(18)), ts(12, FW_SEMIBOLD, t.c.accent)); y += t.dp(22);
    for (auto& kv : ctx_->settings.tau_bd)
        refLine(g, x, y, w, toW(kv.first), toW(bbs::format_num(kv.second)));

    // About card
    drawCard(g, rcAbout_);
    drawText(g, L"About BBS Studio",
             RectF((float)(rcAbout_.left + pad), (float)(rcAbout_.top + pad - t.dp(2)), (float)(rcAbout_.right - rcAbout_.left - 2 * pad), (float)t.dp(24)),
             ts(15, FW_SEMIBOLD, t.c.textPrimary));
    RectF ab((float)(rcAbout_.left + pad), (float)(rcAbout_.top + pad + t.dp(28)), (float)(rcAbout_.right - rcAbout_.left - 2 * pad), (float)(rcAbout_.bottom - rcAbout_.top - pad - t.dp(28)));
    drawText(g, L"Bar Bending Schedule generator for columns, beams, slabs, footings and retaining walls. "
                L"Native C++ · Fluent 2 · IS 456-derived estimation.\n\nSteel unit weight = d²/162 kg/m. "
                L"Always verify against project drawings before construction.",
             ab, ts(12, FW_NORMAL, t.c.textTertiary), Align::Near, Align::Near, true);
}

void SettingsPage::show(bool v) {
    int cmd = v ? SW_SHOW : SW_HIDE;
    for (HWND h : {editDia_, btnApply_}) if (h) ShowWindow(h, cmd);
}

}  // namespace ui
