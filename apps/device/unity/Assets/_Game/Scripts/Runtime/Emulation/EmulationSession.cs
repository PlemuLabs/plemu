using System;
using System.IO;
using Plemu.Content;
using Plemu.Emulation.Native;
using UnityEngine;

namespace Plemu.Emulation
{
    /// <summary>
    /// The emulation facade. Its <b>only</b> entry point is a <see cref="LaunchTicket"/> — there is
    /// no other way to reach a core, so every play path must pass through <see cref="LaunchGate"/>
    /// first. On <see cref="Load"/> it resolves the ticket's core to a dylib, creates the shim
    /// handle, loads the core and the verified artifact, then starts the frame loop on a dedicated
    /// <see cref="EmulationThread"/>.
    /// </summary>
    /// <remarks>
    /// The native handle is exposed only as an opaque <see cref="IntPtr"/> to the video/audio
    /// surfaces so they can call the two thread-safe read entry points (<c>rh_lock_frame</c> /
    /// <c>rh_read_audio</c>); all mutating calls go through this facade onto the emu thread.
    /// </remarks>
    public sealed class EmulationSession : IDisposable
    {
        private IntPtr _handle = IntPtr.Zero;
        private EmulationThread _thread;
        private GCHandleScope _dirs;
        private bool _disposed;

        // --- audio-thread / teardown synchronization ---------------------------------------------
        // The audio thread reads rh_read_audio while the main thread may tear the handle down
        // (rh_unload_game + rh_destroy frees the ring the read dereferences). This lock makes the
        // audio read and the destroy mutually exclusive, and _handleAlive (guarded by the lock) lets
        // a read that arrives after teardown began bail to silence instead of touching freed memory.
        // Honors the shim's "caller serializes rh_* per handle" contract across the two threads.
        private readonly object _handleLock = new object();
        private bool _handleAlive; // guarded by _handleLock

        private LibretroHostNative.AvInfo _av;

        /// <summary>Core video refresh rate (Hz), reported after the game loaded.</summary>
        public double Fps => _av.Fps;

        /// <summary>Core audio sample rate (Hz).</summary>
        public double SampleRate => _av.SampleRate;

        /// <summary>Core base framebuffer width in pixels.</summary>
        public uint BaseWidth => _av.BaseW;

        /// <summary>Core base framebuffer height in pixels.</summary>
        public uint BaseHeight => _av.BaseH;

        /// <summary>Declared display aspect ratio (0 if the core did not report one).</summary>
        public float Aspect => _av.Aspect;

        /// <summary>The system profile resolved from the ticket's system.</summary>
        public SystemProfile Profile { get; private set; }

        /// <summary>The launch ticket this session was loaded from.</summary>
        public LaunchTicket Ticket { get; private set; }

        /// <summary>True once a core + game are loaded and the frame loop is running.</summary>
        public bool IsRunning => _thread != null;

        /// <summary>Frames executed by the emu thread so far.</summary>
        public long FramesRun => _thread?.FramesRun ?? 0;

        public bool IsPaused => _thread?.IsPaused ?? false;

        /// <summary>
        /// The opaque native handle, for <see cref="VideoSurface"/> to call the shim's
        /// <c>rh_lock_frame</c>/<c>rh_unlock_frame</c> on the Unity main thread (serialized with
        /// <see cref="Stop"/> by the main loop). <see cref="IntPtr.Zero"/> until loaded. The audio
        /// thread does NOT use this — it reads via <see cref="TryReadAudio"/> under the teardown
        /// lock to avoid a use-after-free on stop.
        /// </summary>
        internal IntPtr Handle => _handle;

        /// <summary>
        /// Loads the core and verified artifact named by <paramref name="ticket"/> and starts the
        /// frame loop. <paramref name="corePathOverride"/> lets callers (and tests) supply the core
        /// dylib path directly; when null it is resolved from the ticket's core id via
        /// <see cref="CoreRegistry"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">ticket is null.</exception>
        /// <exception cref="EmulationException">any native load step failed.</exception>
        public void Load(LaunchTicket ticket, string corePathOverride = null)
        {
            if (ticket == null)
                throw new ArgumentNullException(nameof(ticket));
            if (IsRunning)
                throw new EmulationException("EmulationSession already loaded; Stop() first.");

            Ticket = ticket;
            Profile = SystemProfile.ForSystem(ticket.System);
            if (Profile == null)
                throw new EmulationException($"Unsupported system '{ticket.System}' (no SystemProfile).");

            string corePath = corePathOverride ?? CoreRegistry.ResolveCorePath(ticket.CoreId);
            if (string.IsNullOrEmpty(corePath))
                throw new EmulationException($"Could not resolve core path for coreId '{ticket.CoreId}'.");
            if (!File.Exists(corePath))
                throw new EmulationException($"Core dylib not found at '{corePath}'. Run stage-editor-plugins.sh.");
            if (!File.Exists(ticket.ContentPath))
                throw new EmulationException($"Artifact not found at '{ticket.ContentPath}'.");

            int abi = LibretroHostNative.rh_abi_version();
            if (abi != 1)
                throw new EmulationException($"libretro_host ABI mismatch: expected 1, got {abi}.");

            // System / save scratch dirs the shim hands to the core. Keep them process-unique.
            string sysDir = CreateScratchDir("plemu_sys");
            string saveDir = CreateScratchDir("plemu_save");
            _dirs = new GCHandleScope(sysDir, saveDir);

            LibretroHostNative.CreateDesc desc = new LibretroHostNative.CreateDesc
            {
                SystemDir = _dirs.SystemDirPtr,
                SaveDir = _dirs.SaveDirPtr,
                Log = IntPtr.Zero, // core/host logs are silenced in the editor runtime
                HwMode = 0,
            };

            _handle = LibretroHostNative.rh_create(ref desc);
            if (_handle == IntPtr.Zero)
                throw Fail("rh_create returned NULL");

            Check("rh_load_core", LibretroHostNative.rh_load_core(_handle, corePath));
            Check("rh_load_game", LibretroHostNative.rh_load_game(_handle, ticket.ContentPath));

            LibretroHostNative.rh_get_av_info(_handle, out LibretroHostNative.AvInfo av);
            _av = av;

            _thread = new EmulationThread(_handle, av.Fps);
            _thread.Start();

            // Publish the handle as alive under the lock (memory barrier) only after it is fully
            // created + loaded, so the audio thread never reads into a half-initialized handle.
            lock (_handleLock)
            {
                _handleAlive = true;
            }
        }

        /// <summary>
        /// Reads up to <paramref name="frames"/> interleaved-stereo s16 frames into
        /// <paramref name="dst"/> via <c>rh_read_audio</c>, holding the teardown lock so the read
        /// cannot overlap <see cref="Stop"/> freeing the handle. Returns the frame count read, or 0
        /// (silence) if the session is torn down / not yet alive. Called from Unity's audio thread.
        /// </summary>
        internal int TryReadAudio(IntPtr dst, int frames)
        {
            // Fast bail without the lock is NOT safe (TOCTOU), so take the lock every call. The
            // critical section is a single native ring read (~microseconds), cheap enough for the
            // ~10 ms audio callback; no frame work is done under it.
            lock (_handleLock)
            {
                if (!_handleAlive || _handle == IntPtr.Zero)
                    return 0;

                int got = LibretroHostNative.rh_read_audio(_handle, dst, frames);
                return got < 0 ? 0 : got;
            }
        }

        /// <summary>Pauses the frame loop (the core stops advancing until <see cref="Resume"/>).</summary>
        public void Pause() => _thread?.Pause();

        /// <summary>Resumes a paused frame loop.</summary>
        public void Resume() => _thread?.Resume();

        /// <summary>Publishes the RetroPad input snapshot for the next frame(s).</summary>
        public void SetInput(ushort buttons)
        {
            if (_thread == null)
                return;
            LibretroHostNative.InputState input = default;
            input.Buttons = buttons;
            _thread.SetInput(input);
        }

        /// <summary>
        /// Captures the current core state (serialized on the emu thread). <paramref name="slot"/>
        /// is a caller-managed label; this facade returns the bytes and lets the caller persist
        /// them. Returns the state bytes.
        /// </summary>
        public byte[] SaveState(int slot = 0)
        {
            EnsureRunning();
            return _thread.SaveState();
        }

        /// <summary>Restores core state previously produced by <see cref="SaveState"/>.</summary>
        public void LoadState(byte[] state, int slot = 0)
        {
            EnsureRunning();
            _thread.LoadState(state);
        }

        /// <summary>
        /// Deterministically advances the core by exactly <paramref name="count"/> frames on the emu
        /// thread and blocks until done. Call <see cref="Pause"/> first so no auto-run frames
        /// interleave; used by the savestate round-trip proof.
        /// </summary>
        public void StepFrames(int count)
        {
            EnsureRunning();
            _thread.StepFrames(count);
        }

        /// <summary>Stops the frame loop, unloads the game, and destroys the handle.</summary>
        /// <remarks>
        /// Order matters for memory safety: (1) join the emu thread so no more mutating <c>rh_*</c>
        /// calls run; (2) under <see cref="_handleLock"/> mark the handle dead and free it. Taking
        /// the lock waits for any in-flight audio read to finish before <c>rh_destroy</c>, and the
        /// dead flag makes every later audio read bail to silence — no use-after-free on the audio
        /// thread. Idempotent and safe on a half-initialized session.
        /// </remarks>
        public void Stop()
        {
            _thread?.Stop();
            _thread = null;

            // Serialize the destroy against the audio thread's rh_read_audio.
            lock (_handleLock)
            {
                _handleAlive = false;
                if (_handle != IntPtr.Zero)
                {
                    LibretroHostNative.rh_unload_game(_handle);
                    LibretroHostNative.rh_destroy(_handle);
                    _handle = IntPtr.Zero;
                }
            }

            _dirs?.Dispose();
            _dirs = null;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Stop();
        }

        private void EnsureRunning()
        {
            if (_thread == null)
                throw new EmulationException("EmulationSession is not running.");
        }

        private void Check(string operation, int code)
        {
            var result = new RhResult(code);
            if (!result.IsOk)
            {
                Stop();
                throw new EmulationException(operation, result);
            }
        }

        private EmulationException Fail(string message)
        {
            Stop();
            return new EmulationException(message);
        }

        private static string CreateScratchDir(string prefix)
        {
            string dir = Path.Combine(Path.GetTempPath(), $"{prefix}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>
        /// Holds the marshalled UTF-8 pointers for the system/save dir strings for the lifetime of
        /// the native handle (the shim keeps the pointers), and cleans up the scratch dirs on
        /// dispose.
        /// </summary>
        private sealed class GCHandleScope : IDisposable
        {
            private readonly string _systemDir;
            private readonly string _saveDir;

            public IntPtr SystemDirPtr { get; }
            public IntPtr SaveDirPtr { get; }

            public GCHandleScope(string systemDir, string saveDir)
            {
                _systemDir = systemDir;
                _saveDir = saveDir;
                SystemDirPtr = System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi(systemDir);
                SaveDirPtr = System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi(saveDir);
            }

            public void Dispose()
            {
                if (SystemDirPtr != IntPtr.Zero)
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(SystemDirPtr);
                if (SaveDirPtr != IntPtr.Zero)
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(SaveDirPtr);
                TryDelete(_systemDir);
                TryDelete(_saveDir);
            }

            private static void TryDelete(string dir)
            {
                try
                {
                    if (Directory.Exists(dir))
                        Directory.Delete(dir, recursive: true);
                }
                catch (IOException)
                {
                    // Scratch dir cleanup is best-effort.
                }
            }
        }
    }
}
