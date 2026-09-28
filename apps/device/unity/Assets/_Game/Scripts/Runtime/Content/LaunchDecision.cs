namespace Plemu.Content
{
    /// <summary>
    /// The result of <see cref="LaunchGate.Authorize"/>: exactly a <see cref="LaunchTicket"/>
    /// XOR a <see cref="LaunchDenial"/>. Exactly one of <see cref="Ticket"/>/<see cref="Denial"/>
    /// is non-null; <see cref="IsAuthorized"/> discriminates.
    /// </summary>
    public sealed class LaunchDecision
    {
        /// <summary>The launch ticket when authorized; null when denied.</summary>
        public LaunchTicket Ticket { get; private set; }

        /// <summary>The denial when refused; null when authorized.</summary>
        public LaunchDenial Denial { get; private set; }

        /// <summary>True iff a ticket was issued.</summary>
        public bool IsAuthorized => Ticket != null;

        private LaunchDecision()
        {
        }

        /// <summary>Builds an authorized decision wrapping <paramref name="ticket"/>.</summary>
        public static LaunchDecision Authorized(LaunchTicket ticket)
        {
            return new LaunchDecision { Ticket = ticket };
        }

        /// <summary>Builds a denied decision with <paramref name="reason"/> and <paramref name="detail"/>.</summary>
        public static LaunchDecision Denied(DenialReason reason, string detail)
        {
            return new LaunchDecision { Denial = new LaunchDenial(reason, detail) };
        }
    }
}
