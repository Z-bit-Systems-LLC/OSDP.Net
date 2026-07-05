using System;

namespace OSDP.Net;

/// <summary>
/// Shared connection-timing settings used by both the ACU (<see cref="DeviceProxy"/>) and PD
/// (<see cref="Device"/>) sides.
/// </summary>
/// <remarks>
/// <see cref="OfflineThreshold"/> follows OSDP 2.2.2 Section 6.1: a device is considered offline
/// after 8 seconds without valid communication. It is internal and mutable purely so the test
/// suite can shorten the wall-clock wait for offline-detection tests; production keeps the 8-second
/// default.
/// </remarks>
internal static class ConnectionTiming
{
    /// <summary>The default OSDP offline threshold (8 seconds).</summary>
    internal static readonly TimeSpan DefaultOfflineThreshold = TimeSpan.FromSeconds(8);

    /// <summary>
    /// The elapsed time without valid communication after which a device is considered offline.
    /// Defaults to <see cref="DefaultOfflineThreshold"/>.
    /// </summary>
    internal static TimeSpan OfflineThreshold { get; set; } = DefaultOfflineThreshold;
}
