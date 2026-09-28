using System;

namespace Plemu.Core
{
    /// <summary>Production <see cref="IClock"/> backed by the system UTC clock.</summary>
    public sealed class SystemClock : IClock
    {
        /// <inheritdoc/>
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
