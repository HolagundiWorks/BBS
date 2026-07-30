// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

namespace BBSApp.Services.Ai;

/// <summary>
/// External entry point for driving the app's page navigation without depending
/// on <see cref="BBSApp.MainWindow"/> internals. Wraps the ribbon's navigation switch so
/// callers (e.g. a future assistant service) route through one validated choke
/// point instead of reaching into the window.
/// </summary>
public interface IAppCommandBus
{
    /// <summary>
    /// Navigate to a ribbon command tag (e.g. "columns", "masonry", "estimate").
    /// Returns <c>false</c> and does nothing for an unknown tag, so a caller can
    /// validate a request rather than land on a silent fallback page.
    /// </summary>
    bool Navigate(string tag);

    /// <summary>The canonical navigable tags, in ribbon order. Derived from the ribbon model.</summary>
    IReadOnlyList<string> KnownTags { get; }
}
