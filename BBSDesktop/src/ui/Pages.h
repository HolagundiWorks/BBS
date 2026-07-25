// Pages.h — Dashboard (project overview + KPIs) and Settings pages.
#pragma once

#include "Page.h"
#include "Widgets.h"

namespace ui {

class DashboardPage : public Page {
public:
    std::wstring navLabel() const override { return L"Dashboard"; }
    std::wstring glyph() const override { return L""; }  // Home
    std::wstring title() const override { return L"Project overview"; }
    std::wstring subtitle() const override {
        return L"Live steel totals across every element you have generated.";
    }
    void create(HWND parent, AppContext* ctx) override;
    void layout(RECT content) override;
    void paint(Gdiplus::Graphics& g, RECT content) override;
    void show(bool visible) override;
    void onCommand(int id, int code, HWND ctl) override;
    bool onNotify(NMHDR* hdr, LRESULT& res) override;
    void refresh();  // recompute KPIs + project summary

private:
    AppContext* ctx_ = nullptr;
    HWND parent_ = nullptr;
    HWND lvSummary_ = nullptr, btnReport_ = nullptr, btnCsv_ = nullptr;

    RECT content_{}, rcHeader_{}, rcKpi_[4]{}, rcSummary_{}, rcActions_{};
    struct Kpi { std::wstring glyph, value, label; };
    Kpi kpis_[4];
    std::vector<std::vector<std::wstring>> summaryRows_;
    std::vector<bbs::SummaryRow> merged_;
};

class SettingsPage : public Page {
public:
    std::wstring navLabel() const override { return L"Settings"; }
    std::wstring glyph() const override { return L""; }  // Settings gear
    std::wstring title() const override { return L"Settings"; }
    std::wstring subtitle() const override {
        return L"Design constants used by the IS 456 estimation engine.";
    }
    void create(HWND parent, AppContext* ctx) override;
    void layout(RECT content) override;
    void paint(Gdiplus::Graphics& g, RECT content) override;
    void show(bool visible) override;
    void onCommand(int id, int code, HWND ctl) override;
    void syncFromSettings();

private:
    AppContext* ctx_ = nullptr;
    HWND parent_ = nullptr;
    HWND editDia_ = nullptr, btnApply_ = nullptr;
    bool diaFocused_ = false;

    RECT content_{}, rcHeader_{}, rcDia_{}, rcRef_{}, rcAbout_{};
    RECT diaFieldR_{};
};

}  // namespace ui
