# Changelog

All notable changes to **AQC-Core** are documented here.
This project adheres to [Semantic Versioning](https://semver.org/).

## [1.0.1] — 2026-07-26

### Added
- Annotated takeoff drawing in project/estimate report PDFs — imported plan
  marks for columns and other elements are drawn onto the exported page.

### Changed
- Report sketches now show steel arrangement (bar layout) instead of the plain
  measurement sketches.

## [1.0.0] — Initial release

First public **AQC-Core** (*Accelerated Quantity and Costing Core*) release,
by Human Centic Works, Hospet.

### Added
- C# **WinUI 3** desktop app (Fluent 2, Mica) over a C++ `bbs_engine.dll`.
- RCC bar bending schedules: columns (incl. pedestal), beams, slabs, footings,
  walls, stairs — with section sketches.
- Civil BOQ: masonry, plaster, PCC, earthwork, SSM, shuttering, flooring,
  painting, doors/windows, and more.
- PDF **drawing takeoff** — measure on an imported plan, then Commit to BOQ.
- Openings on a wall deduct from that masonry wall (IS 1200 ignore-small).
- DSR-style estimates with electrical / plumbing / escalation / consulting %,
  steel & materials purchase orders, and project report PDFs (QuestPDF).
- App-level versioned rate books; `.bbsproj` project files (JSON).
- Packaging: per-user **Setup.exe** (Inno Setup) and single-project **MSIX**.
- Apache-2.0 license.
