using UnityEngine;

namespace Plemu.Emulation
{
    /// <summary>
    /// Polls the editor keyboard each <see cref="Update"/>, folds the pressed keys into a RetroPad
    /// bitmask via the session's <see cref="SystemProfile.DefaultMapping"/>, and publishes it to the
    /// emulation session (which feeds it to the core on the emu thread). Uses the classic
    /// <see cref="Input"/> API — the simplest path that works in the editor.
    /// </summary>
    /// <remarks>
    /// The key-state probe is injectable (<see cref="KeyProbe"/>) so headless tests can drive input
    /// without a real keyboard. Default probe is <see cref="Input.GetKey(KeyCode)"/>.
    /// </remarks>
    public sealed class EmulationInputSource : MonoBehaviour
    {
        private EmulationSession _session;
        private RetroPad _mapping;

        /// <summary>Probe for whether a key is currently held. Overridable for tests.</summary>
        public System.Func<KeyCode, bool> KeyProbe { get; set; } = DefaultKeyProbe;

        /// <summary>The most recently published bitmask (diagnostic).</summary>
        public ushort LastMask { get; private set; }

        /// <summary>Binds to a loaded session, taking its system's default mapping.</summary>
        public void Bind(EmulationSession session)
        {
            _session = session;
            _mapping = session?.Profile?.DefaultMapping ?? RetroPad.DefaultGameBoyMapping;
        }

        private void Update()
        {
            if (_session == null || !_session.IsRunning)
                return;

            ushort mask = _mapping.ToBitmask(KeyProbe);
            LastMask = mask;
            _session.SetInput(mask);
        }

        private static bool DefaultKeyProbe(KeyCode key)
        {
            return Input.GetKey(key);
        }
    }
}
