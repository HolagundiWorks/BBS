# AQC-Core

**AQC-Core** (*Accelerated Quantity and Costing Core*) — quantity take-off and costing by **Human Centic Works, Hospet**.

RCC bar bending schedules, civil BOQ, drawing takeoff, rate books, DSR-style estimates (with electrical / plumbing / escalation / consulting %), and purchase orders.

Quantities follow IS 456 / IS 1200–derived conventions — always cross-check against drawings.

**License:** [Apache License 2.0](LICENSE) · see also [NOTICE](NOTICE)

## Stack

| Layer | Tech |
|-------|------|
| UI | **C# WinUI 3** (Fluent 2, Mica), single-project **MSIX** |
| Engine | **C++** `bbs_engine.dll` (JSON C API) |
| Reports | QuestPDF (estimate / BOQ / PO) |

Executable name: `AQCCore.exe`

## Requirements

- Windows 10/11
- CMake 3.20+ and MSVC (for `bbs_engine`)
- .NET 8 SDK
- Windows App SDK / WinUI workload
- [Inno Setup 6](https://jrsoftware.org/isdl.php) — optional, for Setup.exe
- Developer Mode — optional, for MSIX sideload

## Build & run

```bat
cd BBSDesktop
cmake -S . -B build -G Ninja
cmake --build build --config Release --target bbs_engine bbs_tests
cd BBSApp
dotnet build -c Release -p:Platform=x64
dotnet run -c Release -p:Platform=x64
```

Or launch: `BBSApp\bin\x64\Release\net8.0-windows10.0.19041.0\AQCCore.exe`

## Installer (Setup.exe)

Per-user installer (no admin):

```powershell
cd BBSDesktop
powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
```

Output: `BBSDesktop\artifacts\installer\AQCCore-Setup-1.0.0.exe`  
Installs to `%LocalAppData%\Programs\AQC-Core\`

## MSIX (Store / sideload)

```powershell
cd BBSDesktop
powershell -ExecutionPolicy Bypass -File installer\build-msix.ps1
```

Output: `BBSDesktop\artifacts\msix\*.msix`

- **Microsoft Store:** upload the `.msix` in Partner Center (Microsoft re-signs). Match `Package.appxmanifest` `Identity` Name/Publisher to Partner Center.
- **Sideload:** trust the local cert from `build-msix.ps1`, then `Add-AppxPackage -Path …`.

## Features

| Area | What you get |
|------|----------------|
| Project | Name, location, client, prepared-by (Engineer/Architect/PMC), company, logo — **File → Project settings** |
| Engineering | Diameters, hooks, covers, civil yields — **File → Engineering settings** |
| Cost % | Electrical, plumbing, escalation, consulting fees on estimate base — **File → Cost percentages** |
| Levels | Lvl0 = plinth … LvlN; column clear height from storey geometry |
| RCC | Columns (incl. pedestal), beams, slabs, footings, walls, stairs — BBS + section sketches |
| Civil BOQ | Masonry, plaster, PCC, earthwork, SSM, shuttering, flooring, paint, doors/windows, etc. |
| Openings | Doors/windows **On wall** mark → deduct from that masonry wall (IS 1200 ignore-small) |
| Takeoff | PDF drawing measure → Commit to BOQ |
| Rate book | App-level versioned rates (`%LocalAppData%\AQCCore\`) |
| Estimate | DSR abstract (L/B/H, area, volume) + % markups → grand total; PDF sketches appendix |
| PO / report | Steel & materials PO; project PDF with BOQ tables + sketches |
| Project file | `.bbsproj` (JSON, v9 includes project info + estimate markups) |

## Settings & data locations

| Data | Path |
|------|------|
| Rate books / logos | `%LocalAppData%\AQCCore\` (reads legacy `%LocalAppData%\BOQCore\` if present) |
| Project | User-chosen `.bbsproj` |

## Branding

- Product: **AQC-Core** — Accelerated Quantity and Costing Core  
- Developer: **Human Centic Works, Hospet**  
- Logo: `BBSDesktop/BBSApp/Assets/logo.png`

## Disclaimer

Estimation tool only — not a substitute for structural design or site mix design (M30+).
