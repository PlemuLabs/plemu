using System;
using Plemu.Core;

namespace Plemu.Tests.PlayMode
{
    /// <summary>
    /// A deterministic <see cref="IClock"/> that always reports a fixed instant, so PlayMode tests
    /// never depend on the wall clock. The money-shot test pins its clock to the staged manifest's
    /// own <c>generatedAt</c> window (see <c>EmulationSmokeTests.FreshClockForStagedManifest</c>),
    /// which keeps the real 72h-expiry production manifest shape under test while making the
    /// freshness gate resolve to <c>Fresh</c> regardless of when <c>stage-content.sh</c> last ran.
    /// </summary>
    public sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset now) => UtcNow = now;

        public DateTimeOffset UtcNow { get; }
    }
}
