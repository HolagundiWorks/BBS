# Accessibility notes for AQC-Core (WinUI 3)

The product UI is **WinUI 3** with system Fluent / Mica theming and stock controls
(`MenuBar`, ribbon commands, `TextBox`, `ComboBox`, `NumberBox`, `Button`, `DataGrid`,
`InfoBar`, `ScrollViewer`, `Pivot`).

## Built-in WinUI affordances

- Keyboard: Tab order follows visual tree; menus and ribbon support keyboard access
- High contrast: follows Windows theme via WinUI resource brushes
- DPI: Per-Monitor V2 via application manifest
- Screen readers: Automation peers / `AutomationProperties.Name` on primary actions

## Project actions

- **File:** New / Open / Save / Save As, Project / Engineering / Cost % settings, Exit
- Element pages: datasheet entry, Generate BBS / civil qty, sketches where applicable
- Outputs: Quantities, Purchase orders, Estimate, Rate book, Report
- File pickers for `.bbsproj`, PDF, CSV

Prefer keeping custom chrome out of the UI so accessibility stays with the platform.
