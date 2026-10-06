using System;

namespace Plemu.Core.Abstractions;

/// <summary>
/// Local store of cryptographic entitlement tickets, verified offline for game launches.
/// </summary>
public interface IEntitlementStore
{
    /// <summary>
    /// Checks if a valid, unexpired entitlement ticket exists locally for the given artifact SHA-256.
    /// </summary>
    bool HasValidTicket(string artifactSha256, DateTimeOffset currentTime);
}
