// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

namespace Aorms.Bridge;

/// <summary>Hub + licence endpoints for a desktop node (see esti PORTAL-SYNC-BRIDGE).</summary>
public sealed class BridgeOptions
{
    /// <summary>e.g. https://aorms.in/platform</summary>
    public string LicenseApiUrl { get; set; } = "";

    /// <summary>e.g. https://aorms.in (no /platform suffix)</summary>
    public string HubUrl { get; set; } = "";

    /// <summary>Product API key for /platform/v1/*</summary>
    public string ProductApiKey { get; set; } = "";

    /// <summary>Stable device id (INSTALL_ID).</summary>
    public string DeviceId { get; set; } = "";

    public string DeviceName { get; set; } = Environment.MachineName;
}
