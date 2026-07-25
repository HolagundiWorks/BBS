// App.cpp — entry point. Starts GDI+, common controls, and the main window,
// then runs a message loop with accelerator + dialog (Tab) key handling.
#include "ui/MainWindow.h"
#include "ui/Theme.h"

#include <commctrl.h>

int WINAPI wWinMain(HINSTANCE inst, HINSTANCE, PWSTR, int nCmdShow) {
    INITCOMMONCONTROLSEX icc{sizeof(icc),
                            ICC_LISTVIEW_CLASSES | ICC_STANDARD_CLASSES | ICC_BAR_CLASSES};
    InitCommonControlsEx(&icc);
    ui::gdiplusStartup();

    ui::MainWindow win;
    if (!win.create(inst, nCmdShow)) {
        ui::gdiplusShutdown();
        return 1;
    }

    MSG msg{};
    while (GetMessageW(&msg, nullptr, 0, 0) > 0) {
        if (win.accel() && TranslateAcceleratorW(win.hwnd(), win.accel(), &msg)) continue;
        if (IsDialogMessageW(win.hwnd(), &msg)) continue;  // Tab / arrow navigation
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    ui::gdiplusShutdown();
    return (int)msg.wParam;
}
