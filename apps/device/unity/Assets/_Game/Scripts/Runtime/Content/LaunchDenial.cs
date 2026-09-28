namespace Plemu.Content
{
    /// <summary>A refusal to launch, carrying a typed <see cref="DenialReason"/> and detail text.</summary>
    public sealed class LaunchDenial
    {
        /// <summary>The typed reason the launch was denied.</summary>
        public DenialReason Reason { get; }

        /// <summary>Human-readable detail for logs/UI (never a control-flow signal).</summary>
        public string Detail { get; }

        public LaunchDenial(DenialReason reason, string detail)
        {
            Reason = reason;
            Detail = detail;
        }
    }
}
