namespace Plemu.Content
{
    /// <summary>
    /// Supplies the last-verified manifest bundle to <see cref="LaunchGate"/>. Implementations
    /// persist and return the raw bytes the device previously accepted; the gate re-verifies
    /// them anyway.
    /// </summary>
    public interface IManifestSource
    {
        /// <summary>
        /// Returns the current manifest bundle, or <c>null</c> if none is available (e.g. no
        /// manifest has ever been accepted).
        /// </summary>
        ManifestBundle GetVerifiedManifest();
    }
}
