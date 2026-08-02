# Changelog

All notable changes to **AQC-Core** are documented here.
This project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- **AI assistant (opt-in)** — a built-in copilot on the command bar that drives the same code
  paths a user would. It can navigate the app, summarise the project, run the deterministic C++
  engine for RCC checks (read-only), and — each behind an explicit confirmation dialog — add RCC
  member rows, change covers/markups, draft office correspondence, commit drawing-takeoff openings
  to the BOQ, read the loaded takeoff PDF, add storeys/levels, and author/apply linked-item
  derivation rules. The C++
  engine stays the single source of truth for all numbers. Inert without an Anthropic API key;
  the key is entered in-app and stored encrypted (Windows DPAPI, per-user).
- **Item links — data-driven linked-item derivation** — derive one trade's quantity from another
  (e.g. plaster = 2 × masonry face area, painting = plaster area, skirting = flooring perimeter).
  Editable rules with a standard India-practice library, a chained derivation preview, and
  Apply-to-sheets to materialise the derived quantities into the take-off so they flow into the
  BOQ and estimate (idempotent, undoable via Clear applied). The preview and Apply dialog now also
  **price the derived quantities** against the active rate book (rate/amount per line, a total cost,
  and any unpriced codes), using the same canonical rate codes as the estimate so applied link rows
  price consistently. A rule can **pin a specific rate code** or a **manual unit rate** for its
  derived lines (otherwise the target trade's canonical code is used); a manual rate prices the
  derived quantity even with no rate-book entry. The AI `list_link_rules` tool reports the derived
  cost too, and `add_link_rule` accepts the optional rate code and manual rate.

### Changed
- Project file (`.bbsproj`) format bumped to **v17** — adds the linked-item derivation rules
  (`link_rules`). Older files load unchanged and seed the standard rule library.
- **Relicensed** from Apache-2.0 to a **dual license**: GNU AGPL v3 (Community Edition) or a
  commercial license from Human Centric Works. Free for individuals, research and internal
  business use; a commercial license is required to resell, embed in a proprietary product,
  offer as SaaS without AGPL compliance, remove the branding, or ship closed-source
  modifications. See [LICENSING.md](LICENSING.md). Prior releases (1.0.x, 1.1.0) remain Apache-2.0.

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
by Human Centric Works, Hospet.

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
