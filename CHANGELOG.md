# Changelog

All notable changes to **AQC-Core** are documented here.
This project adheres to [Semantic Versioning](https://semver.org/).

## [1.1.0] — 2026-07-27

Project-management & back-office modules, and dual-persona (Project Manager / Contractor) support.

### Added
- **Personas (Project Manager / Contractor)** — one project now serves both sides of a contract.
  Declared under **File → Project settings**, each persona carries its own letterhead identity
  (company, signatory, GSTIN/PAN) and numbering prefix. Every letter, contract, RA bill and cash
  entry is tagged with the issuing persona, numbered in that persona's own financial-year series
  (e.g. `PMC/LTR/2026-27/001` vs `CON/RA/2026-27/001`), and printed on that persona's letterhead.
  A read-only badge in the ribbon shows the active persona.
- **Schedule** — CPM / PERT project scheduling with an activity network and Gantt chart.
- **Office → Correspondence** — letters, memos, notices, circulars, certificates, declarations,
  site instructions, work-order notes and interim payment certificates, with financial-year
  auto-numbering and per-persona PDF letterheads.
- **Contracts** — item-rate / lump-sum work orders and tenders, a schedule of rates, and a reusable
  standard-terms library, with retention and auto-numbering.
- **Accounts** — running-account (RA) bills with retention and **Indian statutory deductions
  (GST, TDS 194C, labour welfare cess, GST-TDS)**, certify-and-number, a cash/bank book, and a
  per-contractor ledger (order value, certified, paid, retention held, balance payable).
- **Interim Payment Certificate** — generate a PM-issued IPC from a certified RA bill; it lands in
  Correspondence referencing the bill number with its gross → deductions → net breakdown.
- **Stores** — procurement & inventory: purchase orders, goods-receipt notes, stock/inventory and
  supplier / warehouse masters.
- **HR & resources** — sites, resources, employees, attendance and payroll (with payroll register PDF).
- **Duplicate row** on the element datasheets (RCC & civil) — copy a row's fields into a new row
  below (Ctrl+D); speeds up near-identical members such as retaining-wall segments.

### Changed
- Project file (`.bbsproj`) format bumped to **v16** — adds the personas block plus the schedule,
  office, contracts, accounts, stores and HR data. Older files load unchanged: the PM persona seeds
  from the existing project company and legacy document counters fold into the PM series.

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
