// ElementPage.cpp — the reusable card-based element page.
#include "ElementPage.h"

#include "../core/Export.h"
#include "Dialogs.h"

namespace ui {

using namespace Gdiplus;

static TextStyle ts(int px, int weight, Color c) { return TextStyle{theme().dp(px), weight, c}; }

// ------------------------------ create ------------------------------

void ElementPage::create(HWND parent, AppContext* ctx) {
    parent_ = parent;
    ctx_ = ctx;

    for (const auto& f : cfg_.fields) {
        FieldCtl fc;
        fc.key = f.key;
        fc.kind = f.kind;
        if (f.kind == FieldSpec::Combo) {
            int sel = 0;
            for (int i = 0; i < (int)f.options.size(); ++i)
                if (f.options[i] == f.def) sel = i;
            fc.hwnd = createCombo(parent, nextControlId(), f.options, sel);
        } else if (f.kind == FieldSpec::Dia) {
            auto opts = diameterOptions(ctx_->settings, f.optionalDia);
            int sel = 0;
            for (int i = 0; i < (int)opts.size(); ++i)
                if (opts[i] == f.def) sel = i;
            fc.hwnd = createCombo(parent, nextControlId(), opts, sel);
        } else {
            fc.hwnd = createEdit(parent, nextControlId(), f.def);
            SendMessageW(fc.hwnd, 0x1501 /* EM_SETCUEBANNER */, TRUE, (LPARAM)f.label.c_str());
        }
        fields_.push_back(fc);
    }

    btnAdd_ = createButton(parent, nextControlId(), L"Add to project", ButtonKind::Primary, L"");
    btnReset_ = createButton(parent, nextControlId(), L"Reset", ButtonKind::Subtle);
    btnGenerate_ = createButton(parent, nextControlId(), L"Generate BBS", ButtonKind::Primary, L"");
    btnDelete_ = createButton(parent, nextControlId(), L"Delete selected", ButtonKind::Danger, L"");

    lvInput_ = createListView(parent, nextControlId());
    setListColumns(lvInput_, cfg_.inputCols);
    lvBbs_ = createListView(parent, nextControlId());
    setListColumns(lvBbs_, cfg_.bbsCols);
    lvSummary_ = createListView(parent, nextControlId());
    setListColumns(lvSummary_, cfg_.summaryCols);
    btnExportBbs_ = createButton(parent, nextControlId(), L"Export CSV", ButtonKind::Subtle, L"");
    btnExportSum_ = createButton(parent, nextControlId(), L"Export CSV", ButtonKind::Subtle, L"");

    if (cfg_.hasChecks) {
        lvCheck_ = createListView(parent, nextControlId());
        setListColumns(lvCheck_, cfg_.checkCols);
        btnExportCheck_ = createButton(parent, nextControlId(), L"Export CSV", ButtonKind::Subtle, L"");
    }

    rows_ = cfg_.seed;
    refreshInputList();
}

// ------------------------------ form helpers ------------------------------

bbs::RawRow ElementPage::readForm() {
    bbs::RawRow row;
    for (auto& fc : fields_) {
        if (fc.kind == FieldSpec::Combo || fc.kind == FieldSpec::Dia) {
            int sel = (int)SendMessageW(fc.hwnd, CB_GETCURSEL, 0, 0);
            wchar_t buf[128] = L"";
            if (sel >= 0) SendMessageW(fc.hwnd, CB_GETLBTEXT, sel, (LPARAM)buf);
            row[fc.key] = fromW(buf);
        } else {
            row[fc.key] = fromW(getText(fc.hwnd));
        }
    }
    return row;
}

void ElementPage::setFieldDefaults() {
    for (size_t i = 0; i < fields_.size(); ++i) {
        const auto& spec = cfg_.fields[i];
        if (spec.kind == FieldSpec::Combo) {
            int sel = 0;
            for (int k = 0; k < (int)spec.options.size(); ++k)
                if (spec.options[k] == spec.def) sel = k;
            SendMessageW(fields_[i].hwnd, CB_SETCURSEL, sel, 0);
        } else if (spec.kind == FieldSpec::Dia) {
            refillCombo(fields_[i].hwnd, diameterOptions(ctx_->settings, spec.optionalDia), spec.def);
        } else {
            setText(fields_[i].hwnd, spec.def);
        }
    }
}

void ElementPage::refillDiaCombos() {
    for (size_t i = 0; i < fields_.size(); ++i) {
        if (cfg_.fields[i].kind != FieldSpec::Dia) continue;
        int sel = (int)SendMessageW(fields_[i].hwnd, CB_GETCURSEL, 0, 0);
        wchar_t buf[64] = L"";
        if (sel >= 0) SendMessageW(fields_[i].hwnd, CB_GETLBTEXT, sel, (LPARAM)buf);
        refillCombo(fields_[i].hwnd, diameterOptions(ctx_->settings, cfg_.fields[i].optionalDia), buf);
    }
}

void ElementPage::onSettingsChanged() { refillDiaCombos(); }

void ElementPage::refreshInputList() {
    std::vector<std::vector<std::wstring>> rows;
    for (const auto& r : rows_) {
        std::vector<std::wstring> cells;
        for (const auto& key : cfg_.inputKeys) {
            auto it = r.find(key);
            cells.push_back(it == r.end() ? L"" : toW(it->second));
        }
        rows.push_back(cells);
    }
    setListRows(lvInput_, rows);
}

// ------------------------------ actions ------------------------------

void ElementPage::doAdd() {
    bbs::RawRow row = readForm();
    const std::string markKey = cfg_.fields.empty() ? "mark" : cfg_.fields[0].key;
    std::string mark = row.count(markKey) ? row[markKey] : "";
    bool blank = mark.find_first_not_of(" \t") == std::string::npos;
    if (blank) {
        error_ = L"“" + cfg_.fields[0].label + L"” is required before adding.";
        InvalidateRect(parent_, &rcForm_, FALSE);
        SetFocus(fields_[0].hwnd);
        return;
    }
    rows_.push_back(row);
    error_.clear();
    refreshInputList();
    if (ctx_->markDirty) ctx_->markDirty();
    SetFocus(fields_[0].hwnd);
    SendMessageW(fields_[0].hwnd, EM_SETSEL, 0, -1);
    InvalidateRect(parent_, &content_, FALSE);
}

void ElementPage::doReset() {
    setFieldDefaults();
    error_.clear();
    InvalidateRect(parent_, &rcForm_, FALSE);
}

void ElementPage::doDelete() {
    int sel = ListView_GetNextItem(lvInput_, -1, LVNI_SELECTED);
    if (sel < 0 || sel >= (int)rows_.size()) return;
    rows_.erase(rows_.begin() + sel);
    refreshInputList();
    if (ctx_->markDirty) ctx_->markDirty();
    InvalidateRect(parent_, &rcInput_, FALSE);
}

void ElementPage::doGenerate() {
    if (rows_.empty()) {
        error_ = L"Add at least one element before generating.";
        InvalidateRect(parent_, &rcForm_, FALSE);
        return;
    }
    GenResult res = cfg_.generate(rows_, ctx_->settings);
    if (!res.error.empty()) {
        errorBox(parent_, res.error, L"Cannot generate");
        return;
    }
    lastBbs_ = res.bbsRows;
    lastSummary_ = res.summaryRows;
    lastCheck_ = res.checkRows;
    setListRows(lvBbs_, res.bbsRows);
    setListRows(lvSummary_, res.summaryRows);
    if (lvCheck_) setListRows(lvCheck_, res.checkRows);
    generated_ = true;
    error_.clear();

    if (cfg_.key == "columns") ctx_->lastColumn = res.summary;
    else if (cfg_.key == "beams") ctx_->lastBeam = res.summary;
    else if (cfg_.key == "slabs") ctx_->lastSlab = res.summary;
    else if (cfg_.key == "footings") ctx_->lastFooting = res.summary;
    else if (cfg_.key == "walls") ctx_->lastWall = res.summary;
    if (ctx_->onDataChanged) ctx_->onDataChanged();
    InvalidateRect(parent_, &content_, FALSE);
}

void ElementPage::exportRows(const std::vector<Column>& cols,
                             const std::vector<std::vector<std::wstring>>& rows,
                             const std::wstring& name) {
    if (rows.empty()) {
        infoBox(parent_, L"Generate the schedule first, then export.");
        return;
    }
    std::wstring path = saveFileDialog(parent_, L"CSV files\0*.csv\0All files\0*.*\0", L"csv", name);
    if (path.empty()) return;
    std::vector<std::string> headers;
    for (const auto& c : cols) headers.push_back(fromW(c.title));
    std::vector<std::vector<std::string>> body;
    for (const auto& r : rows) {
        std::vector<std::string> cells;
        for (const auto& c : r) cells.push_back(fromW(c));
        body.push_back(cells);
    }
    std::string err;
    if (bbs::export_table_csv(headers, body, path, err))
        infoBox(parent_, L"Saved to:\n" + path);
    else
        errorBox(parent_, toW(err));
}

// ------------------------------ command / notify ------------------------------

void ElementPage::onCommand(int, int code, HWND ctl) {
    if (ctl == btnAdd_ && code == BN_CLICKED) doAdd();
    else if (ctl == btnReset_ && code == BN_CLICKED) doReset();
    else if (ctl == btnDelete_ && code == BN_CLICKED) doDelete();
    else if (ctl == btnGenerate_ && code == BN_CLICKED) doGenerate();
    else if (ctl == btnExportBbs_ && code == BN_CLICKED)
        exportRows(cfg_.bbsCols, lastBbs_, toW(cfg_.key) + L"_bbs");
    else if (ctl == btnExportSum_ && code == BN_CLICKED)
        exportRows(cfg_.summaryCols, lastSummary_, toW(cfg_.key) + L"_summary");
    else if (ctl == btnExportCheck_ && code == BN_CLICKED)
        exportRows(cfg_.checkCols, lastCheck_, toW(cfg_.key) + L"_checks");
    else if (code == EN_SETFOCUS) {
        focusedEdit_ = ctl;
        InvalidateRect(parent_, &rcForm_, FALSE);
    } else if (code == EN_KILLFOCUS) {
        if (focusedEdit_ == ctl) focusedEdit_ = nullptr;
        InvalidateRect(parent_, &rcForm_, FALSE);
    }
}

bool ElementPage::onNotify(NMHDR* hdr, LRESULT& res) {
    if (hdr->code == NM_CUSTOMDRAW) {
        if (hdr->hwndFrom == lvInput_ || hdr->hwndFrom == lvBbs_ || hdr->hwndFrom == lvSummary_ ||
            hdr->hwndFrom == lvCheck_) {
            res = handleListCustomDraw(hdr->hwndFrom, (NMLVCUSTOMDRAW*)hdr);
            return true;
        }
        // Column headers (contrast fix).
        for (HWND lv : {lvInput_, lvBbs_, lvSummary_, lvCheck_}) {
            if (lv && hdr->hwndFrom == ListView_GetHeader(lv)) {
                res = handleHeaderCustomDraw((NMCUSTOMDRAW*)hdr);
                return true;
            }
        }
    }
    return false;
}

// ------------------------------ persistence ------------------------------

static std::vector<bbs::RawRow>& rowsFor(bbs::ProjectData& p, const std::string& key) {
    if (key == "columns") return p.columns;
    if (key == "beams") return p.beams;
    if (key == "slabs") return p.slabs;
    if (key == "walls") return p.walls;
    return p.footings;
}
static const std::vector<bbs::RawRow>& rowsFor(const bbs::ProjectData& p, const std::string& key) {
    if (key == "columns") return p.columns;
    if (key == "beams") return p.beams;
    if (key == "slabs") return p.slabs;
    if (key == "walls") return p.walls;
    return p.footings;
}

void ElementPage::collect(bbs::ProjectData& p) { rowsFor(p, cfg_.key) = rows_; }

void ElementPage::applyData(const bbs::ProjectData& p) {
    rows_ = rowsFor(p, cfg_.key);
    refreshInputList();
    generated_ = false;
    lastBbs_.clear(); lastSummary_.clear(); lastCheck_.clear();
    setListRows(lvBbs_, {});
    setListRows(lvSummary_, {});
    if (lvCheck_) setListRows(lvCheck_, {});
    InvalidateRect(parent_, &content_, FALSE);
}

// ------------------------------ layout ------------------------------

int ElementPage::flowFields(int originX, int originY, int cw, bool place) {
    auto& t = theme();
    int colGutter = t.dp(8);
    double unit = (cw - 11.0 * colGutter) / 12.0;
    int labelH = t.dp(15), gap1 = t.dp(3), ctlH = t.dp(32);
    int blockH = labelH + gap1 + ctlH, rowGap = t.dp(14);

    int x = 0, rowY = 0;
    for (size_t i = 0; i < cfg_.fields.size(); ++i) {
        int span = cfg_.fields[i].colspan;
        int fieldW = (int)(span * unit + (span - 1) * colGutter);
        if (x > 0 && x + fieldW > cw + 2) { x = 0; rowY += blockH + rowGap; }
        int bx = originX + x, by = originY + rowY;
        RECT labelR{bx, by, bx + fieldW, by + labelH};
        RECT fieldR{bx, by + labelH + gap1, bx + fieldW, by + labelH + gap1 + ctlH};
        if (place && i < fields_.size()) {
            fields_[i].labelR = labelR;
            fields_[i].fieldR = fieldR;
            HWND h = fields_[i].hwnd;
            if (fields_[i].kind == FieldSpec::Combo || fields_[i].kind == FieldSpec::Dia) {
                MoveWindow(h, fieldR.left, fieldR.top, fieldW, ctlH + t.dp(220), TRUE);
            } else {
                int eh = t.dp(20);
                MoveWindow(h, fieldR.left + t.dp(10), fieldR.top + (ctlH - eh) / 2,
                           fieldW - t.dp(20), eh, TRUE);
            }
        }
        x += fieldW + colGutter;
    }
    return rowY + blockH;
}

int ElementPage::formCardHeight(int cardW) {
    auto& t = theme();
    int pad = t.dp(20);
    int cw = cardW - 2 * pad;
    int titleH = t.dp(22);
    int Hf = flowFields(0, 0, cw, false);
    int buttonsH = t.dp(36), errorH = t.dp(18);
    return pad + titleH + t.dp(12) + Hf + t.dp(12) + errorH + t.dp(6) + buttonsH + pad;
}

void ElementPage::layout(RECT content) {
    auto& t = theme();
    content_ = content;
    int P = t.dp(24), G = t.dp(16), pad = t.dp(20), headerH = t.dp(58);
    int left0 = content.left + P;
    int innerW = (content.right - content.left) - 2 * P;
    int leftW = (int)((innerW - G) * 7.0 / 12.0);
    int rightW = innerW - G - leftW;

    int top = content.top + P;
    rcHeader_ = {left0, top, content.right - P, top + headerH};

    int y1 = top + headerH + t.dp(6);
    int formH = formCardHeight(leftW);
    rcForm_ = {left0, y1, left0 + leftW, y1 + formH};
    rcInput_ = {left0 + leftW + G, y1, content.right - P, y1 + formH};

    int y2 = y1 + formH + G;
    int bottom = content.bottom - P;
    int remaining = bottom - y2;
    int rowB = remaining, rowC = 0;
    if (cfg_.hasChecks) {
        rowB = (int)(remaining * 0.60) - G / 2;
        rowC = remaining - rowB - G;
    }
    rcBbs_ = {left0, y2, left0 + leftW, y2 + rowB};
    rcSummary_ = {left0 + leftW + G, y2, content.right - P, y2 + rowB};
    if (cfg_.hasChecks) rcCheck_ = {left0, y2 + rowB + G, content.right - P, bottom};

    // form fields + buttons
    int titleH = t.dp(22);
    flowFields(rcForm_.left + pad, rcForm_.top + pad + titleH + t.dp(12), leftW - 2 * pad, true);
    int btnH = t.dp(34);
    int btnY = rcForm_.bottom - pad - btnH;
    MoveWindow(btnAdd_, rcForm_.left + pad, btnY, t.dp(150), btnH, TRUE);
    MoveWindow(btnReset_, rcForm_.left + pad + t.dp(158), btnY, t.dp(84), btnH, TRUE);

    // input list card
    int inX = rcInput_.left + pad;
    int inTop = rcInput_.top + pad + titleH + t.dp(10);
    int inBtnY = rcInput_.bottom - pad - btnH;
    MoveWindow(lvInput_, inX, inTop, rcInput_.right - pad - inX, inBtnY - t.dp(10) - inTop, TRUE);
    MoveWindow(btnDelete_, inX, inBtnY, t.dp(150), btnH, TRUE);

    // header Generate button
    int gW = t.dp(150), gH = t.dp(36);
    MoveWindow(btnGenerate_, rcHeader_.right - gW, rcHeader_.top + (headerH - gH) / 2, gW, gH, TRUE);

    auto placeResult = [&](RECT card, HWND lv, HWND exportBtn) {
        int exW = t.dp(104), exH = t.dp(28);
        MoveWindow(exportBtn, card.right - pad - exW, card.top + pad - t.dp(2), exW, exH, TRUE);
        int lvTop = card.top + pad + titleH + t.dp(8);
        MoveWindow(lv, card.left + pad, lvTop, card.right - pad - (card.left + pad),
                   card.bottom - pad - lvTop, TRUE);
    };
    placeResult(rcBbs_, lvBbs_, btnExportBbs_);
    placeResult(rcSummary_, lvSummary_, btnExportSum_);
    if (cfg_.hasChecks) placeResult(rcCheck_, lvCheck_, btnExportCheck_);
}

// ------------------------------ paint ------------------------------

static void paintCardTitle(Graphics& g, RECT card, const std::wstring& title, int pad) {
    auto& t = theme();
    RectF r((float)(card.left + pad), (float)(card.top + pad - t.dp(2)),
            (float)(card.right - card.left - 2 * pad), (float)t.dp(24));
    drawText(g, title, r, ts(15, FW_SEMIBOLD, t.c.textPrimary), Align::Near, Align::Near);
}

void ElementPage::paint(Graphics& g, RECT content) {
    auto& t = theme();
    int pad = t.dp(20);

    // Header
    RectF titleR((float)rcHeader_.left, (float)rcHeader_.top, (float)(rcHeader_.right - rcHeader_.left - t.dp(160)), (float)t.dp(30));
    drawText(g, cfg_.title, titleR, ts(22, FW_SEMIBOLD, t.c.textPrimary), Align::Near, Align::Near);
    RectF subR((float)rcHeader_.left, (float)(rcHeader_.top + t.dp(32)),
               (float)(rcHeader_.right - rcHeader_.left - t.dp(160)), (float)t.dp(20));
    drawText(g, cfg_.subtitle, subR, ts(13, FW_NORMAL, t.c.textSecondary), Align::Near, Align::Near);

    // Cards
    drawCard(g, rcForm_);
    drawCard(g, rcInput_);
    drawCard(g, rcBbs_);
    drawCard(g, rcSummary_);
    if (cfg_.hasChecks) drawCard(g, rcCheck_);

    // Form card content
    paintCardTitle(g, rcForm_, L"Element details", pad);
    for (auto& fc : fields_) {
        size_t idx = &fc - &fields_[0];
        RectF lr = toRectF(fc.labelR);
        drawText(g, cfg_.fields[idx].label, lr, ts(12, FW_SEMIBOLD, t.c.textSecondary), Align::Near, Align::Near);
        if (fc.kind == FieldSpec::Text)
            paintFieldContainer(g, fc.fieldR, fc.hwnd == focusedEdit_);
    }
    if (!error_.empty()) {
        int btnH = t.dp(34);
        RectF er((float)(rcForm_.left + pad), (float)(rcForm_.bottom - pad - btnH - t.dp(22)),
                 (float)(rcForm_.right - rcForm_.left - 2 * pad), (float)t.dp(20));
        drawGlyph(g, L"", RectF(er.X, er.Y, (float)t.dp(16), er.Height), t.dp(13), t.c.danger,
                  Align::Near, Align::Center);
        RectF et(er.X + t.dp(20), er.Y, er.Width - t.dp(20), er.Height);
        drawText(g, error_, et, ts(12, FW_NORMAL, t.c.danger), Align::Near, Align::Center);
    }

    // Input card content
    paintCardTitle(g, rcInput_, L"Elements in project", pad);
    std::wstring count = std::to_wstring(rows_.size()) + (rows_.size() == 1 ? L" element" : L" elements");
    RectF cntR((float)(rcInput_.left + pad), (float)(rcInput_.top + pad - t.dp(2)),
               (float)(rcInput_.right - rcInput_.left - 2 * pad), (float)t.dp(24));
    drawText(g, count, cntR, ts(12, FW_NORMAL, t.c.textTertiary), Align::Far, Align::Near);

    // Result cards
    paintCardTitle(g, rcBbs_, L"Bar bending schedule", pad);
    paintCardTitle(g, rcSummary_, L"Steel summary", pad);
    if (cfg_.hasChecks) paintCardTitle(g, rcCheck_, cfg_.checkTitle, pad);

    if (!generated_) {
        // empty-state hint in the bbs card
        RectF hint((float)(rcBbs_.left + pad), (float)(rcBbs_.top + t.dp(60)),
                   (float)(rcBbs_.right - rcBbs_.left - 2 * pad), (float)t.dp(24));
        drawText(g, L"Add elements, then choose “Generate BBS” to build the schedule.", hint,
                 ts(13, FW_NORMAL, t.c.textTertiary), Align::Near, Align::Near, true);
    }
}

void ElementPage::show(bool visible) {
    int cmd = visible ? SW_SHOW : SW_HIDE;
    for (auto& fc : fields_) ShowWindow(fc.hwnd, cmd);
    HWND btns[] = {btnAdd_, btnReset_, btnGenerate_, btnDelete_, btnExportBbs_, btnExportSum_,
                   btnExportCheck_, lvInput_, lvBbs_, lvSummary_, lvCheck_};
    for (HWND h : btns) if (h) ShowWindow(h, cmd);
}

}  // namespace ui
