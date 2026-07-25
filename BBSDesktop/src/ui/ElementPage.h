// ElementPage.h — configurable element page with hierarchical native form controls,
// type-dependent visibility, and dynamic N-entry extra-bar lists.
#pragma once

#include "Page.h"
#include "Widgets.h"
#include <functional>

namespace ui {

struct FieldSpec {
    std::string key;
    std::wstring label;
    enum Kind { Text, Combo, Dia, Section } kind = Text;
    std::vector<std::wstring> options;
    std::wstring def;
    int colspan = 3;
    std::wstring hint;
    bool optionalDia = false;
    // Show only when showWhenKey's combo value is in showWhenValues (empty = always).
    std::string showWhenKey;
    std::vector<std::wstring> showWhenValues;
};

enum class ExtraPanelKind { Fixed, SpanFrac, Mesh };

struct ExtraPanelSpec {
    ExtraPanelKind kind = ExtraPanelKind::Fixed;
    std::wstring title;
    std::string storeKey;  // RawRow key: extra_fixed / extra_span / extra_mesh
};

struct GenResult {
    std::vector<std::vector<std::wstring>> bbsRows;
    std::vector<std::vector<std::wstring>> summaryRows;
    std::vector<std::vector<std::wstring>> checkRows;
    std::vector<bbs::SummaryRow> summary;
    std::wstring error;
};

struct ElementConfig {
    std::wstring navLabel, glyph, title, subtitle;
    std::string key;
    std::vector<FieldSpec> fields;
    std::vector<ExtraPanelSpec> extraPanels;
    std::string typeKey;  // footing_type / slab_type — triggers visibility refresh
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
        HWND hwnd = nullptr;      // edit/combo (null for Section)
        HWND labelHwnd = nullptr; // native STATIC
        RECT labelR{}, fieldR{};
        bool visible = true;
    };

    struct ExtraPanel {
        ExtraPanelSpec spec;
        HWND titleHwnd = nullptr;
        HWND lv = nullptr;
        HWND dia = nullptr, a = nullptr, b = nullptr;  // dia + two value edits
        HWND lblDia = nullptr, lblA = nullptr, lblB = nullptr;
        HWND btnAdd = nullptr, btnRemove = nullptr;
        std::vector<std::vector<std::wstring>> rows;
        RECT rc{};
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
    void updateVisibility();
    void syncExtrasFromRow(const bbs::RawRow& row);
    void writeExtrasIntoRow(bbs::RawRow& row) const;
    bbs::RawRow readForm();
    std::wstring fieldValue(const std::string& key) const;
    void addExtraRow(ExtraPanel& p);
    void removeExtraRow(ExtraPanel& p);

    int flowFields(int originX, int originY, int contentW, bool place);
    int extrasBlockHeight() const;
    int formCardHeight(int contentW);

    ElementConfig cfg_;
    AppContext* ctx_ = nullptr;
    HWND parent_ = nullptr;

    std::vector<FieldCtl> fields_;
    std::vector<ExtraPanel> extras_;
    std::vector<bbs::RawRow> rows_;
    std::wstring error_;

    HWND btnAdd_ = nullptr, btnReset_ = nullptr, btnDelete_ = nullptr, btnGenerate_ = nullptr;
    HWND btnExportBbs_ = nullptr, btnExportSum_ = nullptr, btnExportCheck_ = nullptr;
    HWND lvInput_ = nullptr, lvBbs_ = nullptr, lvSummary_ = nullptr, lvCheck_ = nullptr;
    bool generated_ = false;

    RECT content_{}, rcHeader_{}, rcForm_{}, rcInput_{}, rcBbs_{}, rcSummary_{}, rcCheck_{};
    std::vector<std::vector<std::wstring>> lastBbs_, lastSummary_, lastCheck_;
};

// Helpers used by MainWindow configs.
FieldSpec section(const wchar_t* title, int span = 12);
FieldSpec textF(const char* key, const wchar_t* label, const wchar_t* def, int span);
FieldSpec comboF(const char* key, const wchar_t* label, std::vector<std::wstring> opts,
                 const wchar_t* def, int span);
FieldSpec diaF(const char* key, const wchar_t* label, const wchar_t* def, int span,
               bool optional = false);
FieldSpec when(FieldSpec f, const char* key, std::initializer_list<const wchar_t*> values);

}  // namespace ui
