# Accessibility notes for BBS Studio (WinUI 3)

The product UI is **WinUI 3** with system Fluent / Mica theming and stock controls
(`NavigationView`, `TextBox`, `ComboBox`, `Button`, `ListView`, `InfoBar`, `ScrollViewer`).

## Built-in WinUI affordances

- Keyboard: Tab order follows visual tree; NavigationView supports arrow keys
- High contrast: follows Windows theme via WinUI resource brushes
- DPI: Per-Monitor V2 via application manifest
- Screen readers: Automation peers from stock controls

## Project actions

- New / Open / Save / Save As on the command bar
- Element pages: Add / Reset / Delete / Generate BBS
- File pickers for `.bbsproj`, CSV, and HTML export

Prefer keeping custom chrome out of the UI so accessibility stays with the platform.
