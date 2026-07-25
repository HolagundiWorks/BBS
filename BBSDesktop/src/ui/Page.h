// Page.h — page interface and the shared application context passed to pages.
#pragma once

#include "../core/Engine.h"
#include "../core/Project.h"
#include "Draw.h"

#include <functional>
#include <string>
#include <vector>

namespace ui {

// Shared state the pages read/write. Owned by MainWindow.
struct AppContext {
    bbs::Settings settings;
    // Most recent per-element summaries (fed to the Dashboard / project total).
    std::vector<bbs::SummaryRow> lastColumn, lastBeam, lastSlab, lastFooting, lastWall;

    HWND mainHwnd = nullptr;
    std::function<void()> onDataChanged;  // notify dashboard to refresh
    std::function<void()> markDirty;      // project has unsaved changes
};

class Page {
public:
    virtual ~Page() = default;

    virtual std::wstring navLabel() const = 0;
    virtual std::wstring glyph() const = 0;   // Segoe Fluent Icons glyph
    virtual std::wstring title() const = 0;
    virtual std::wstring subtitle() const = 0;

    virtual void create(HWND parent, AppContext* ctx) = 0;
    virtual void layout(RECT content) = 0;                    // position child controls
    virtual void paint(Gdiplus::Graphics& g, RECT content) = 0;  // draw cards/labels
    virtual void show(bool visible) = 0;

    virtual void onCommand(int id, int code, HWND ctl) {}
    virtual bool onNotify(NMHDR* hdr, LRESULT& res) { return false; }

    // Persistence hooks.
    virtual void collect(bbs::ProjectData&) {}
    virtual void applyData(const bbs::ProjectData&) {}
    virtual void onSettingsChanged() {}
};

// Simple process-wide control-id allocator.
int nextControlId();

}  // namespace ui
