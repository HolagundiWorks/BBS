// ElementPage.h — one configurable page reused for Columns/Beams/Slabs/Footings.
// A card-based layout: an input form card + an "in project" list card on top,
// then result cards (BBS detail + steel summary) and an optional checks card.
#pragma once

#include "Page.h"
#include "Widgets.h"

namespace ui {

struct FieldSpec {
    std::string key;
    std::wstring label;
    enum Kind { Text, Combo, Dia } kind = Text;  // Dia = diameter dropdown from settings
    std::vector<std::wstring> options;  // combo choices (ignored for Dia)
    std::wstring def;                   // default text / selected option
    int colspan = 3;                    // out of a 12-col form grid
    std::wstring hint;                  // optional helper text under the control
    bool optionalDia = false;           // Dia kind: include blank "(none)" option
};

struct GenResult {
    std::vector<std::vector<std::wstring>> bbsRows;
    std::vector<std::vector<std::wstring>> summaryRows;
    std::vector<std::vector<std::wstring>> checkRows;
    std::vector<bbs::SummaryRow> summary;  // for the dashboard / project total
    std::wstring error;
};

struct ElementConfig {
    std::wstring navLabel, glyph, title, subtitle;
    std::string key;  // "columns" / "beams" / "slabs" / "footings"
    std::vector<FieldSpec> fields;
    std::vector<Column> inputCols;
    std::vector<std::string> inputKeys;
    std::vector<Column> bbsCols;
    std::vector<Column> summaryCols;
    bool hasChecks = false;
    std::wstring checkTitle;
    std::vector<Column> checkCols;
    std::function<GenResult(const std::vector<bbs::RawRow>&, const bbs::Settings&)> generate;
    std::vector<bbs::RawRow> seed;
};

class ElementPage : public Page {
public:
    explicit ElementPage(ElementConfig cfg) : cfg_(std::move(cfg)) {}

    std::wstring navLabel() const override { return cfg_.navLabel; }
    std::wstring glyph() const override { return cfg_.glyph; }
    std::wstring title() const override { return cfg_.title; }
    std::wstring subtitle() const override { return cfg_.subtitle; }

    void create(HWND parent, AppContext* ctx) override;
    void layout(RECT content) override;
    void paint(Gdiplus::Graphics& g, RECT content) override;
    void show(bool visible) override;
    void onCommand(int id, int code, HWND ctl) override;
    bool onNotify(NMHDR* hdr, LRESULT& res) override;
    void collect(bbs::ProjectData&) override;
    void applyData(const bbs::ProjectData&) override;
    void onSettingsChanged() override;

private:
    struct FieldCtl {
        std::string key;
        FieldSpec::Kind kind;
        HWND hwnd = nullptr;
        RECT labelR{};
        RECT fieldR{};
    };

    void doAdd();
    void doReset();
    void doDelete();
    void doGenerate();
    void exportRows(const std::vector<Column>& cols,
                    const std::vector<std::vector<std::wstring>>& rows, const std::wstring& name);
    void refreshInputList();
    void setFieldDefaults();
    void refillDiaCombos();
    bbs::RawRow readForm();

    int flowFields(int originX, int originY, int contentW, bool place);
    int formCardHeight(int contentW);

    ElementConfig cfg_;
    AppContext* ctx_ = nullptr;
    HWND parent_ = nullptr;

    std::vector<FieldCtl> fields_;
    std::vector<bbs::RawRow> rows_;
    std::wstring error_;

    HWND btnAdd_ = nullptr, btnReset_ = nullptr, btnDelete_ = nullptr, btnGenerate_ = nullptr;
    HWND btnExportBbs_ = nullptr, btnExportSum_ = nullptr, btnExportCheck_ = nullptr;
    HWND lvInput_ = nullptr, lvBbs_ = nullptr, lvSummary_ = nullptr, lvCheck_ = nullptr;
    HWND focusedEdit_ = nullptr;
    bool generated_ = false;

    RECT content_{}, rcHeader_{}, rcForm_{}, rcInput_{}, rcBbs_{}, rcSummary_{}, rcCheck_{};

    std::vector<std::vector<std::wstring>> lastBbs_, lastSummary_, lastCheck_;
};

}  // namespace ui
