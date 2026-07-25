# BBS Studio

Native Windows desktop app for generating **Bar Bending Schedules** (columns,
beams, slabs, footings, retaining walls) with CSV and HTML export. Quantities
follow IS 456–derived conventions for estimation — always cross-check against
structural drawings before construction.

## Requirements

- Windows 10/11
- [CMake](https://cmake.org/) 3.20+
- MSVC (Visual Studio 2019+ with C++ desktop workload), or a compatible C++17 toolchain

No third-party package manager dependencies — Win32, GDI+, Common Controls only.

## Build & run

```bat
cd BBSDesktop
cmake -S . -B build
cmake --build build --config Release
.\build\Release\BBSStudio.exe
```

(If your generator is single-config, the exe may be at `.\build\BBSStudio.exe`.)

Engine smoke tests:

```bat
cmake --build build --config Release --target bbs_tests
.\build\Release\bbs_tests.exe
```

## Features

| Area | What you get |
|------|----------------|
| Columns | Ties (closed / double / circular / spiral) + longitudinal bars |
| Beams | Support/mid stirrups, top/bottom bars, **extra fixed** (`dia:nos:length`), **extra span-%** (`dia:nos:frac`), **skin** (IS 456 Cl. 26.5.1.3) |
| Slabs | One-way / two-way mesh, extras fixed or mesh (`dia:length:spacing`), min-steel check |
| Footings | **Isolated / Double / Strip / Raft** — bottom (± top) mesh, extras, anchorage + min-steel |
| Retaining walls | Stem V/H, base mesh, optional links/extras, min-steel |
| Project | `.bbsproj` JSON save/load, dashboard totals, HTML report, CSV |

**BBS tables** list one row per identical bar group: Mark, Role, Dia, Cutting length, **Nos**.

## Keyboard / accessibility

- **Ctrl+N / O / S** — New / Open / Save; **Ctrl+Shift+S** — Save As
- **Ctrl+1…7** — jump to Dashboard … Settings
- **F6** — focus the navigation rail; **Arrow keys** + **Enter** to switch pages; **Esc** to leave nav focus
- Forms use native Tab order (`IsDialogMessage`)

See [BBSDesktop/docs/WCAG-AUDIT.md](BBSDesktop/docs/WCAG-AUDIT.md).

## Disclaimer

This is a quantity-estimation tool. Development length, stirrup zones, and
minimum-steel checks are simplified IS 456 forms — not a substitute for design
or detailing drawings.
