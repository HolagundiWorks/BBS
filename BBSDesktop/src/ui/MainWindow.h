// MainWindow.h — top-level shell: Fluent NavigationView rail + hosted pages,
// message routing, theming, and project file operations.
#pragma once

#include "Page.h"
#include <memory>
#include <vector>

namespace ui {

class MainWindow {
public:
    bool create(HINSTANCE inst, int nCmdShow);
    HWND hwnd() const { return hwnd_; }
    HACCEL accel() const { return accel_; }

private:
    static LRESULT CALLBACK wndProcStatic(HWND, UINT, WPARAM, LPARAM);
    LRESULT wndProc(UINT, WPARAM, LPARAM);

    void buildPages();
    void relayout();
    void paint();
    void paintNav(Gdiplus::Graphics& g, RECT nav);
    RECT navRect() const;
    RECT contentRect() const;

    int navHitTest(POINT p) const;  // 0..N-1 page, or special codes
    void switchPage(int idx);

    void newProject();
    void openProject();
    void saveProject(bool saveAs);
    void exportReport();
    void collectProject(bbs::ProjectData&);
    void applyProject(const bbs::ProjectData&);

    void makeBrushes();
    void updateTitle();
    void reloadThemeAndFonts();

    HWND hwnd_ = nullptr;
    HINSTANCE inst_ = nullptr;
    HACCEL accel_ = nullptr;
    int dpi_ = 96;

    AppContext ctx_;
    std::vector<std::unique_ptr<Page>> pages_;
    int active_ = 0;

    std::wstring projectPath_;
    std::wstring projectName_ = L"Untitled Project";
    bool dirty_ = false;

    // nav geometry (computed in relayout)
    bool navCompact_ = false;
    std::vector<RECT> navItemRects_;
    RECT navNewR_{}, navOpenR_{}, navSaveR_{};
    int navHot_ = -2;  // -2 none; 0..N page; 100 new;101 open;102 save
    int navFocus_ = 0; // keyboard focus index into pages_ (WCAG keyboard nav)
    bool navKeyboardFocus_ = false;

    HBRUSH controlBrush_ = nullptr, cardBrush_ = nullptr;
};

}  // namespace ui
