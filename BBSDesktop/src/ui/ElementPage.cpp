// ElementPage.cpp — hierarchical native form + dynamic extra-bar panels.
#include "ElementPage.h"

#include "../core/Export.h"
#include "Dialogs.h"

#include <algorithm>
#include <sstream>

namespace ui {

using namespace Gdiplus;

static TextStyle ts(int px, int weight, Color c) { return TextStyle{theme().dp(px), weight, c}; }

// ------------------------------ field helpers ------------------------------

FieldSpec section(const wchar_t* title, int span) {
    FieldSpec f;
    f.key = std::string("_sec_") + fromW(title);
    f.label = title;
    f.kind = FieldSpec::Section;
    f.colspan = span;
    return f;
}

FieldSpec textF(const char* key, const wchar_t* label, const wchar_t* def, int span) {
    FieldSpec f;
    f.key = key; f.label = label; f.kind = FieldSpec::Text; f.def = def; f.colspan = span;
    return f;
}

FieldSpec comboF(const char* key, const wchar_t* label, std::vector<std::wstring> opts,
                 const wchar_t* def, int span) {
    FieldSpec f;
    f.key = key; f.label = label; f.kind = FieldSpec::Combo; f.options = std::move(opts);
    f.def = def; f.colspan = span;
    return f;
}

FieldSpec diaF(const char* key, const wchar_t* label, const wchar_t* def, int span, bool optional) {
    FieldSpec f;
    f.key = key; f.label = label; f.kind = FieldSpec::Dia; f.def = def; f.colspan = span;
    f.optionalDia = optional;
    return f;
}

FieldSpec when(FieldSpec f, const char* key, std::initializer_list<const wchar_t*> values) {
    f.showWhenKey = key;
    for (auto* v : values) f.showWhenValues.emplace_back(v);
    return f;
}

static std::wstring serializeExtras(ExtraPanelKind kind, const std::vector<std::vector<std::wstring>>& rows) {
    std::wostringstream oss;
    bool first = true;
    for (const auto& r : rows) {
        if (r.size() < 3) continue;
        if (r[0].empty() || r[1].empty() || r[2].empty()) continue;
        if (!first) oss << L", ";
        first = false;
        oss << r[0] << L":" << r[1] << L":" << r[2];
    }
    return oss.str();
}

static std::vector<std::vector<std::wstring>> parseExtrasStored(const std::string& text) {
    std::vector<std::vector<std::wstring>> out;
    if (text.empty()) return out;
    std::string cur;
    auto flush = [&](const std::string& part) {
        if (part.empty()) return;
        // dia:a:b
        size_t p1 = part.find(':');
        size_t p2 = p1 == std::string::npos ? std::string::npos : part.find(':', p1 + 1);
        if (p1 == std::string::npos || p2 == std::string::npos) return;
        out.push_back({toW(part.substr(0, p1)), toW(part.substr(p1 + 1, p2 - p1 - 1)),
                       toW(part.substr(p2 + 1))});
    };
    for (char ch : text) {
        if (ch == ',') { flush(cur); cur.clear(); }
        else if (ch != ' ') cur.push_back(ch);
    }
    flush(cur);
    return out;
}

// ------------------------------ create ------------------------------

void ElementPage::create(HWND parent, AppContext* ctx) {
    parent_ = parent;
    ctx_ = ctx;

    for (const auto& f : cfg_.fields) {
        FieldCtl fc;
        fc.key = f.key;
        fc.kind = f.kind;
        fc.labelHwnd = createStatic(parent, nextControlId(), f.label, f.kind == FieldSpec::Section);
        if (f.kind == FieldSpec::Section) {
            fc.hwnd = nullptr;
        } else if (f.kind == FieldSpec::Combo) {
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

    for (const auto& spec : cfg_.extraPanels) {
        ExtraPanel p;
        p.spec = spec;
        p.titleHwnd = createStatic(parent, nextControlId(), spec.title, true);
        p.lv = createListView(parent, nextControlId());
        std::vector<Column> cols;
        if (spec.kind == ExtraPanelKind::Fixed)
            cols = {{L"Ø (mm)", 70, true}, {L"Nos", 60, true}, {L"Length (mm)", 100, true}};
        else if (spec.kind == ExtraPanelKind::SpanFrac)
            cols = {{L"Ø (mm)", 70, true}, {L"Nos", 60, true}, {L"Frac of span", 100, true}};
        else
            cols = {{L"Ø (mm)", 70, true}, {L"Length (mm)", 100, true}, {L"Spacing (mm)", 100, true}};
        setListColumns(p.lv, cols);

        p.lblDia = createStatic(parent, nextControlId(), L"Ø");
        p.dia = createCombo(parent, nextControlId(), diameterOptions(ctx_->settings, true), 0);
        if (spec.kind == ExtraPanelKind::Fixed) {
            p.lblA = createStatic(parent, nextControlId(), L"Nos");
            p.lblB = createStatic(parent, nextControlId(), L"Length");
        } else if (spec.kind == ExtraPanelKind::SpanFrac) {
            p.lblA = createStatic(parent, nextControlId(), L"Nos");
            p.lblB = createStatic(parent, nextControlId(), L"Frac");
        } else {
            p.lblA = createStatic(parent, nextControlId(), L"Length");
            p.lblB = createStatic(parent, nextControlId(), L"Spacing");
        }
        p.a = createEdit(parent, nextControlId(), L"");
        p.b = createEdit(parent, nextControlId(), L"");
        p.btnAdd = createButton(parent, nextControlId(), L"Add", ButtonKind::Default);
        p.btnRemove = createButton(parent, nextControlId(), L"Remove", ButtonKind::Subtle);
        extras_.push_back(std::move(p));
    }

    btnAdd_ = createButton(parent, nextControlId(), L"Add to project", ButtonKind::Primary);
    btnReset_ = createButton(parent, nextControlId(), L"Reset", ButtonKind::Subtle);
    btnGenerate_ = createButton(parent, nextControlId(), L"Generate BBS", ButtonKind::Primary);
    btnDelete_ = createButton(parent, nextControlId(), L"Delete selected", ButtonKind::Danger);

    lvInput_ = createListView(parent, nextControlId());
    setListColumns(lvInput_, cfg_.inputCols);
    lvBbs_ = createListView(parent, nextControlId());
    setListColumns(lvBbs_, cfg_.bbsCols);
    lvSummary_ = createListView(parent, nextControlId());
    setListColumns(lvSummary_, cfg_.summaryCols);
    btnExportBbs_ = createButton(parent, nextControlId(), L"Export CSV", ButtonKind::Subtle);
    btnExportSum_ = createButton(parent, nextControlId(), L"Export CSV", ButtonKind::Subtle);

    if (cfg_.hasChecks) {
        lvCheck_ = createListView(parent, nextControlId());
        setListColumns(lvCheck_, cfg_.checkCols);
        btnExportCheck_ = createButton(parent, nextControlId(), L"Export CSV", ButtonKind::Subtle);
    }

    rows_ = cfg_.seed;
    refreshInputList();
    updateVisibility();
}

// ------------------------------ form helpers ------------------------------

std::wstring ElementPage::fieldValue(const std::string& key) const {
    for (size_t i = 0; i < fields_.size(); ++i) {
        if (fields_[i].key != key) continue;
        if (!fields_[i].hwnd) return L"";
        if (fields_[i].kind == FieldSpec::Combo || fields_[i].kind == FieldSpec::Dia) {
            int sel = (int)SendMessageW(fields_[i].hwnd, CB_GETCURSEL, 0, 0);
            wchar_t buf[128] = L"";
            if (sel >= 0) SendMessageW(fields_[i].hwnd, CB_GETLBTEXT, sel, (LPARAM)buf);
            return buf;
        }
        return getText(fields_[i].hwnd);
    }
    return L"";
}

void ElementPage::writeExtrasIntoRow(bbs::RawRow& row) const {
    for (const auto& p : extras_) {
        row[p.spec.storeKey] = fromW(serializeExtras(p.spec.kind, p.rows));
    }
}

void ElementPage::syncExtrasFromRow(const bbs::RawRow& row) {
    for (auto& p : extras_) {
        auto it = row.find(p.spec.storeKey);
        p.rows = it == row.end() ? std::vector<std::vector<std::wstring>>{}
                                 : parseExtrasStored(it->second);
        setListRows(p.lv, p.rows);
    }
}

bbs::RawRow ElementPage::readForm() {
    bbs::RawRow row;
    for (size_t i = 0; i < fields_.size(); ++i) {
        auto& fc = fields_[i];
        if (fc.kind == FieldSpec::Section || !fc.visible || !fc.hwnd) continue;
        if (fc.kind == FieldSpec::Combo || fc.kind == FieldSpec::Dia) {
            int sel = (int)SendMessageW(fc.hwnd, CB_GETCURSEL, 0, 0);
            wchar_t buf[128] = L"";
            if (sel >= 0) SendMessageW(fc.hwnd, CB_GETLBTEXT, sel, (LPARAM)buf);
            row[fc.key] = fromW(buf);
        } else {
            row[fc.key] = fromW(getText(fc.hwnd));
        }
    }
    // Keep type keys even if somehow hidden.
    if (!cfg_.typeKey.empty()) row[cfg_.typeKey] = fromW(fieldValue(cfg_.typeKey));
    writeExtrasIntoRow(row);
    return row;
}

void ElementPage::setFieldDefaults() {
    for (size_t i = 0; i < fields_.size(); ++i) {
        const auto& spec = cfg_.fields[i];
        if (spec.kind == FieldSpec::Section || !fields_[i].hwnd) continue;
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
    for (auto& p : extras_) {
        p.rows.clear();
        setListRows(p.lv, {});
        setText(p.a, L"");
        setText(p.b, L"");
        SendMessageW(p.dia, CB_SETCURSEL, 0, 0);
    }
    updateVisibility();
}

void ElementPage::refillDiaCombos() {
    for (size_t i = 0; i < fields_.size(); ++i) {
        if (cfg_.fields[i].kind != FieldSpec::Dia || !fields_[i].hwnd) continue;
        int sel = (int)SendMessageW(fields_[i].hwnd, CB_GETCURSEL, 0, 0);
        wchar_t buf[64] = L"";
        if (sel >= 0) SendMessageW(fields_[i].hwnd, CB_GETLBTEXT, sel, (LPARAM)buf);
        refillCombo(fields_[i].hwnd, diameterOptions(ctx_->settings, cfg_.fields[i].optionalDia), buf);
    }
    for (auto& p : extras_) {
        int sel = (int)SendMessageW(p.dia, CB_GETCURSEL, 0, 0);
        wchar_t buf[64] = L"";
        if (sel >= 0) SendMessageW(p.dia, CB_GETLBTEXT, sel, (LPARAM)buf);
        refillCombo(p.dia, diameterOptions(ctx_->settings, true), buf);
    }
}

void ElementPage::onSettingsChanged() { refillDiaCombos(); }

void ElementPage::updateVisibility() {
    std::wstring typeVal = cfg_.typeKey.empty() ? L"" : fieldValue(cfg_.typeKey);
    for (size_t i = 0; i < fields_.size(); ++i) {
        const auto& spec = cfg_.fields[i];
        bool vis = true;
        if (!spec.showWhenKey.empty()) {
            std::wstring gate = fieldValue(spec.showWhenKey);
            vis = false;
            for (const auto& v : spec.showWhenValues)
                if (v == gate) { vis = true; break; }
        }
        fields_[i].visible = vis;
        int cmd = vis ? SW_SHOW : SW_HIDE;
        if (fields_[i].labelHwnd) ShowWindow(fields_[i].labelHwnd, cmd);
        if (fields_[i].hwnd) ShowWindow(fields_[i].hwnd, cmd);
    }
    (void)typeVal;
    if (parent_) {
        layout(content_);
        InvalidateRect(parent_, &rcForm_, FALSE);
    }
}

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

void ElementPage::addExtraRow(ExtraPanel& p) {
    int sel = (int)SendMessageW(p.dia, CB_GETCURSEL, 0, 0);
    wchar_t diaBuf[64] = L"";
    if (sel >= 0) SendMessageW(p.dia, CB_GETLBTEXT, sel, (LPARAM)diaBuf);
    std::wstring a = getText(p.a), b = getText(p.b);
    if (wcslen(diaBuf) == 0 || a.empty() || b.empty()) {
        error_ = L"Enter diameter and both values before adding an extra bar.";
        InvalidateRect(parent_, &rcForm_, FALSE);
        return;
    }
    p.rows.push_back({diaBuf, a, b});
    setListRows(p.lv, p.rows);
    setText(p.a, L"");
    setText(p.b, L"");
    error_.clear();
    InvalidateRect(parent_, &rcForm_, FALSE);
}

void ElementPage::removeExtraRow(ExtraPanel& p) {
    int sel = ListView_GetNextItem(p.lv, -1, LVNI_SELECTED);
    if (sel < 0 || sel >= (int)p.rows.size()) return;
    p.rows.erase(p.rows.begin() + sel);
    setListRows(p.lv, p.rows);
}

// ------------------------------ actions ------------------------------

void ElementPage::doAdd() {
    bbs::RawRow row = readForm();
    std::string markKey = "mark";
    for (const auto& f : cfg_.fields)
        if (f.kind != FieldSpec::Section) { markKey = f.key; break; }
    std::string mark = row.count(markKey) ? row[markKey] : "";
    bool blank = mark.find_first_not_of(" \t") == std::string::npos;
    if (blank) {
        error_ = L"Mark is required before adding.";
        InvalidateRect(parent_, &rcForm_, FALSE);
        for (auto& fc : fields_)
            if (fc.key == markKey && fc.hwnd) { SetFocus(fc.hwnd); break; }
        return;
    }
    rows_.push_back(row);
    error_.clear();
    refreshInputList();
    if (ctx_->markDirty) ctx_->markDirty();
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
    else {
        for (auto& p : extras_) {
            if (ctl == p.btnAdd && code == BN_CLICKED) { addExtraRow(p); return; }
            if (ctl == p.btnRemove && code == BN_CLICKED) { removeExtraRow(p); return; }
        }
        // Conditional field gate changed → refresh visibility.
        if (code == CBN_SELCHANGE) {
            for (auto& fc : fields_) {
                if (fc.hwnd != ctl) continue;
                bool gate = false;
                for (const auto& spec : cfg_.fields)
                    if (spec.showWhenKey == fc.key) { gate = true; break; }
                if (gate) updateVisibility();
                return;
            }
        }
    }
}

bool ElementPage::onNotify(NMHDR* hdr, LRESULT& res) {
    if (hdr->code == NM_CUSTOMDRAW) {
        if (hdr->hwndFrom == lvInput_ || hdr->hwndFrom == lvBbs_ || hdr->hwndFrom == lvSummary_ ||
            hdr->hwndFrom == lvCheck_) {
            res = handleListCustomDraw(hdr->hwndFrom, (NMLVCUSTOMDRAW*)hdr);
            return true;
        }
        for (auto& p : extras_) {
            if (hdr->hwndFrom == p.lv) {
                res = handleListCustomDraw(p.lv, (NMLVCUSTOMDRAW*)hdr);
                return true;
            }
            if (p.lv && hdr->hwndFrom == ListView_GetHeader(p.lv)) {
                res = handleHeaderCustomDraw((NMCUSTOMDRAW*)hdr);
                return true;
            }
        }
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

int ElementPage::extrasBlockHeight() const {
    if (extras_.empty()) return 0;
    auto& t = theme();
    // title + list + input row + buttons per panel
    int per = t.dp(22) + t.dp(8) + t.dp(110) + t.dp(8) + t.dp(28) + t.dp(8) + t.dp(28) + t.dp(12);
    return (int)extras_.size() * per;
}

int ElementPage::flowFields(int originX, int originY, int cw, bool place) {
    auto& t = theme();
    int colGutter = t.dp(8);
    double unit = (cw - 11.0 * colGutter) / 12.0;
    int labelH = t.dp(16), gap1 = t.dp(4), ctlH = t.dp(28);
    int blockH = labelH + gap1 + ctlH, rowGap = t.dp(12);
    int sectionH = t.dp(22), sectionGap = t.dp(10);

    int x = 0, rowY = 0;
    auto advanceRow = [&]() {
        if (x > 0) { x = 0; rowY += blockH + rowGap; }
    };

    for (size_t i = 0; i < cfg_.fields.size(); ++i) {
        if (i < fields_.size() && !fields_[i].visible) continue;
        const auto& spec = cfg_.fields[i];

        if (spec.kind == FieldSpec::Section) {
            advanceRow();
            if (rowY > 0) rowY += t.dp(4);
            int fieldW = cw;
            RECT labelR{originX, originY + rowY, originX + fieldW, originY + rowY + sectionH};
            if (place && i < fields_.size()) {
                fields_[i].labelR = labelR;
                fields_[i].fieldR = {0, 0, 0, 0};
                MoveWindow(fields_[i].labelHwnd, labelR.left, labelR.top, fieldW, sectionH, TRUE);
            }
            rowY += sectionH + sectionGap;
            x = 0;
            continue;
        }

        int span = std::clamp(spec.colspan, 1, 12);
        int fieldW = (int)(span * unit + (span - 1) * colGutter);
        if (x > 0 && x + fieldW > cw + 2) { x = 0; rowY += blockH + rowGap; }
        int bx = originX + x, by = originY + rowY;
        RECT labelR{bx, by, bx + fieldW, by + labelH};
        RECT fieldR{bx, by + labelH + gap1, bx + fieldW, by + labelH + gap1 + ctlH};
        if (place && i < fields_.size()) {
            fields_[i].labelR = labelR;
            fields_[i].fieldR = fieldR;
            MoveWindow(fields_[i].labelHwnd, labelR.left, labelR.top, fieldW, labelH, TRUE);
            HWND h = fields_[i].hwnd;
            if (!h) { /* skip */ }
            else if (fields_[i].kind == FieldSpec::Combo || fields_[i].kind == FieldSpec::Dia) {
                MoveWindow(h, fieldR.left, fieldR.top, fieldW, ctlH + t.dp(220), TRUE);
            } else {
                MoveWindow(h, fieldR.left, fieldR.top, fieldW, ctlH, TRUE);
            }
        }
        x += fieldW + colGutter;
    }
    if (x > 0) rowY += blockH;
    return rowY;
}

int ElementPage::formCardHeight(int cardW) {
    auto& t = theme();
    int pad = t.dp(16);
    int cw = cardW - 2 * pad;
    int titleH = t.dp(22);
    int Hf = flowFields(0, 0, cw, false);
    int He = extrasBlockHeight();
    int buttonsH = t.dp(36), errorH = t.dp(18);
    return pad + titleH + t.dp(10) + Hf + t.dp(8) + He + errorH + t.dp(6) + buttonsH + pad;
}

void ElementPage::layout(RECT content) {
    if (content.right <= content.left) return;
    auto& t = theme();
    content_ = content;
    int P = t.dp(24), G = t.dp(16), pad = t.dp(16), headerH = t.dp(58);
    int left0 = content.left + P;
    int innerW = (content.right - content.left) - 2 * P;
    int leftW = (int)((innerW - G) * 7.0 / 12.0);
    int rightW = innerW - G - leftW;
    (void)rightW;

    int top = content.top + P;
    rcHeader_ = {left0, top, content.right - P, top + headerH};

    int y1 = top + headerH + t.dp(6);
    int formH = formCardHeight(leftW);
    // Cap form so results still have room; enable internal scroll later if needed.
    int maxForm = (content.bottom - content.top) / 2;
    if (formH > maxForm) formH = maxForm;
    rcForm_ = {left0, y1, left0 + leftW, y1 + formH};
    rcInput_ = {left0 + leftW + G, y1, content.right - P, y1 + formH};

    int y2 = y1 + formH + G;
    int bottom = content.bottom - P;
    int remaining = bottom - y2;
    int rowB = remaining, rowC = 0;
    if (cfg_.hasChecks) {
        rowB = (int)(remaining * 0.60) - G / 2;
        rowC = remaining - rowB - G;
        (void)rowC;
    }
    rcBbs_ = {left0, y2, left0 + leftW, y2 + rowB};
    rcSummary_ = {left0 + leftW + G, y2, content.right - P, y2 + rowB};
    if (cfg_.hasChecks) rcCheck_ = {left0, y2 + rowB + G, content.right - P, bottom};

    int titleH = t.dp(22);
    int cw = leftW - 2 * pad;
    int fieldsH = flowFields(rcForm_.left + pad, rcForm_.top + pad + titleH + t.dp(10), cw, true);

    // Extra panels stacked under fields.
    int exY = rcForm_.top + pad + titleH + t.dp(10) + fieldsH + t.dp(8);
    int btnH = t.dp(28);
    for (auto& p : extras_) {
        int panelTop = exY;
        MoveWindow(p.titleHwnd, rcForm_.left + pad, panelTop, cw, t.dp(20), TRUE);
        int lvTop = panelTop + t.dp(24);
        int lvH = t.dp(100);
        MoveWindow(p.lv, rcForm_.left + pad, lvTop, cw, lvH, TRUE);
        int rowY = lvTop + lvH + t.dp(6);
        int colW = (cw - t.dp(16)) / 4;
        MoveWindow(p.lblDia, rcForm_.left + pad, rowY, colW, t.dp(16), TRUE);
        MoveWindow(p.lblA, rcForm_.left + pad + colW + t.dp(4), rowY, colW, t.dp(16), TRUE);
        MoveWindow(p.lblB, rcForm_.left + pad + 2 * (colW + t.dp(4)), rowY, colW, t.dp(16), TRUE);
        int editY = rowY + t.dp(16);
        MoveWindow(p.dia, rcForm_.left + pad, editY, colW, btnH + t.dp(180), TRUE);
        MoveWindow(p.a, rcForm_.left + pad + colW + t.dp(4), editY, colW, btnH, TRUE);
        MoveWindow(p.b, rcForm_.left + pad + 2 * (colW + t.dp(4)), editY, colW, btnH, TRUE);
        int bY = editY + btnH + t.dp(4);
        MoveWindow(p.btnAdd, rcForm_.left + pad, bY, t.dp(72), btnH, TRUE);
        MoveWindow(p.btnRemove, rcForm_.left + pad + t.dp(80), bY, t.dp(80), btnH, TRUE);
        p.rc = {rcForm_.left + pad, panelTop, rcForm_.right - pad, bY + btnH};
        exY = bY + btnH + t.dp(12);
    }

    int actionH = t.dp(34);
    int btnY = rcForm_.bottom - pad - actionH;
    MoveWindow(btnAdd_, rcForm_.left + pad, btnY, t.dp(140), actionH, TRUE);
    MoveWindow(btnReset_, rcForm_.left + pad + t.dp(148), btnY, t.dp(80), actionH, TRUE);

    int inX = rcInput_.left + pad;
    int inTop = rcInput_.top + pad + titleH + t.dp(10);
    int inBtnY = rcInput_.bottom - pad - actionH;
    MoveWindow(lvInput_, inX, inTop, rcInput_.right - pad - inX, inBtnY - t.dp(10) - inTop, TRUE);
    MoveWindow(btnDelete_, inX, inBtnY, t.dp(140), actionH, TRUE);

    int gW = t.dp(140), gH = t.dp(36);
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
    (void)content;
    auto& t = theme();
    int pad = t.dp(16);

    RectF titleR((float)rcHeader_.left, (float)rcHeader_.top,
                 (float)(rcHeader_.right - rcHeader_.left - t.dp(160)), (float)t.dp(30));
    drawText(g, cfg_.title, titleR, ts(22, FW_SEMIBOLD, t.c.textPrimary), Align::Near, Align::Near);
    RectF subR((float)rcHeader_.left, (float)(rcHeader_.top + t.dp(32)),
               (float)(rcHeader_.right - rcHeader_.left - t.dp(160)), (float)t.dp(20));
    drawText(g, cfg_.subtitle, subR, ts(13, FW_NORMAL, t.c.textSecondary), Align::Near, Align::Near);

    drawCard(g, rcForm_);
    drawCard(g, rcInput_);
    drawCard(g, rcBbs_);
    drawCard(g, rcSummary_);
    if (cfg_.hasChecks) drawCard(g, rcCheck_);

    paintCardTitle(g, rcForm_, L"Element details", pad);

    if (!error_.empty()) {
        int btnH = t.dp(34);
        RectF er((float)(rcForm_.left + pad), (float)(rcForm_.bottom - pad - btnH - t.dp(22)),
                 (float)(rcForm_.right - rcForm_.left - 2 * pad), (float)t.dp(20));
        drawText(g, error_, er, ts(12, FW_NORMAL, t.c.danger), Align::Near, Align::Center);
    }

    paintCardTitle(g, rcInput_, L"Elements in project", pad);
    std::wstring count = std::to_wstring(rows_.size()) + (rows_.size() == 1 ? L" element" : L" elements");
    RectF cntR((float)(rcInput_.left + pad), (float)(rcInput_.top + pad - t.dp(2)),
               (float)(rcInput_.right - rcInput_.left - 2 * pad), (float)t.dp(24));
    drawText(g, count, cntR, ts(12, FW_NORMAL, t.c.textTertiary), Align::Far, Align::Near);

    paintCardTitle(g, rcBbs_, L"Bar bending schedule", pad);
    paintCardTitle(g, rcSummary_, L"Steel summary", pad);
    if (cfg_.hasChecks) paintCardTitle(g, rcCheck_, cfg_.checkTitle, pad);

    if (!generated_) {
        RectF hint((float)(rcBbs_.left + pad), (float)(rcBbs_.top + t.dp(60)),
                   (float)(rcBbs_.right - rcBbs_.left - 2 * pad), (float)t.dp(24));
        drawText(g, L"Add elements, then choose “Generate BBS” to build the schedule.", hint,
                 ts(13, FW_NORMAL, t.c.textTertiary), Align::Near, Align::Near, true);
    }
}

void ElementPage::show(bool visible) {
    int cmd = visible ? SW_SHOW : SW_HIDE;
    for (auto& fc : fields_) {
        if (fc.labelHwnd) ShowWindow(fc.labelHwnd, visible && fc.visible ? SW_SHOW : SW_HIDE);
        if (fc.hwnd) ShowWindow(fc.hwnd, visible && fc.visible ? SW_SHOW : SW_HIDE);
    }
    for (auto& p : extras_) {
        HWND hs[] = {p.titleHwnd, p.lv, p.dia, p.a, p.b, p.lblDia, p.lblA, p.lblB, p.btnAdd, p.btnRemove};
        for (HWND h : hs) if (h) ShowWindow(h, cmd);
    }
    HWND btns[] = {btnAdd_, btnReset_, btnGenerate_, btnDelete_, btnExportBbs_, btnExportSum_,
                   btnExportCheck_, lvInput_, lvBbs_, lvSummary_, lvCheck_};
    for (HWND h : btns) if (h) ShowWindow(h, cmd);
}

}  // namespace ui
