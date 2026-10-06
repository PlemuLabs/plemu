using System;

namespace Plemu.Content
{
    /// <summary>
    /// Local store of cryptographic entitlement tickets, verified offline by <see cref="LaunchGate"/>.
    /// </summary>
    public interface IEntitlementStore
    {
        /// <summary>
        /// Checks if a valid, unexpired entitlement ticket exists locally for the given artifact SHA-256.
        /// </summary>
        bool HasValidTicket(string artifactSha256, DateTimeOffset currentTime);
    }
}
