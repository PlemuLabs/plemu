using System;

namespace Plemu.Core
{
    /// <summary>
    /// Abstraction over the wall clock so verification and launch policy are deterministic
    /// under test. Production wiring uses <see cref="SystemClock"/>.
    /// </summary>
    public interface IClock
    {
        /// <summary>The current instant, in UTC.</summary>
        DateTimeOffset UtcNow { get; }
    }
}
