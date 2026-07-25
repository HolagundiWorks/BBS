# RCC Core

**RCC Core** — reinforced concrete quantity estimation by **Human Centic Works, Hospet**.

Bar bending schedules, storey levels, concrete material split by grade, and purchase orders (steel + other materials).

Quantities follow IS 456–derived conventions — always cross-check against drawings.

## Stack

| Layer | Tech |
|-------|------|
| UI | **C# WinUI 3** (Fluent 2, Mica) |
| Engine | **C++** `bbs_engine.dll` (JSON C API) |

## Requirements

- Windows 10/11
- CMake 3.20+ and MSVC
- .NET 8 SDK
- Windows App SDK / WinUI workload

## Build & run

```bat
cd BBSDesktop
cmake -S . -B build -G Ninja
cmake --build build --config Release --target bbs_engine bbs_tests
cd BBSApp
dotnet build -c Release -p:Platform=x64
dotnet run -c Release -p:Platform=x64
```

Executable: `bin\x64\Release\net8.0-windows10.0.19041.0\BOQCore.exe`

## Installer

Build a per-user **Setup.exe** (no admin) with [Inno Setup 6](https://jrsoftware.org/isdl.php):

```powershell
# Once: install Inno Setup 6 from https://jrsoftware.org/isdl.php
cd BBSDesktop
cmake -S . -B build -G Ninja
cmake --build build --config Release --target bbs_engine
powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
```

Output: `BBSDesktop\artifacts\installer\BOQCore-Setup-1.0.0.exe`  
Installs to `%LocalAppData%\Programs\BOQ Core\` with Start Menu shortcut (Desktop optional).

## Features

| Area | What you get |
|------|----------------|
| Levels | Lvl0 = plinth … LvlN; height = slab-top → slab-top; column H = ht − slab t − beam D |
| Columns | Shaft + **pedestal** (Lvl0), grade, level-linked height |
| Beams / slabs / footings / walls | BBS with extras, crank, stepped footings, etc. |
| Concrete | Volume per element + cement / sand / aggregate by grade (nominal-style split) |
| Purchase orders | Level-filtered steel PO and materials PO (CSV export) |
| Project | `.bbsproj` v3 (includes levels), HTML report |

## Branding

- Product: **BOQ Core** (aka RCC Core)
- Developer: **Human Centic Works, Hospet**
- Logo: `BBSDesktop/BBSApp/Assets/logo.png`

## Disclaimer

Estimation tool only — not a substitute for structural design or site mix design (M30+).
