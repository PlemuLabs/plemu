using System;

namespace Plemu.Content
{
    /// <summary>Manifest freshness state (SPEC §2.3 step 6).</summary>
    public enum Freshness
    {
        /// <summary><c>now ≤ expiresAt</c>: play and download allowed.</summary>
        Fresh,

        /// <summary><c>expiresAt &lt; now ≤ expiresAt + 7d</c>: installed titles play; downloads blocked.</summary>
        Grace,

        /// <summary>Beyond grace: CDN-installed titles blocked (bundled starter-pack exemption is a later task).</summary>
        Expired,
    }

    /// <summary>
    /// Evaluates <see cref="Freshness"/> per SPEC §2.3 step 6. The grace window is exactly
    /// 7 days and is inclusive of its upper boundary (<c>now == expiresAt + 7d</c> ⇒ Grace).
    /// </summary>
    public static class FreshnessPolicy
    {
        /// <summary>The grace period following manifest expiry (SPEC §2.3 step 6).</summary>
        public static readonly TimeSpan GracePeriod = TimeSpan.FromDays(7);

        /// <summary>
        /// Classifies <paramref name="now"/> relative to <paramref name="expiresAt"/>.
        /// Both boundaries are inclusive on the earlier side: <c>now == expiresAt</c> is Fresh,
        /// <c>now == expiresAt + 7d</c> is Grace.
        /// </summary>
        public static Freshness Evaluate(DateTimeOffset expiresAt, DateTimeOffset now)
        {
            if (now <= expiresAt)
                return Freshness.Fresh;

            if (now <= expiresAt + GracePeriod)
                return Freshness.Grace;

            return Freshness.Expired;
        }
    }
}
