using System;
using System.Collections.Concurrent;
using System.Threading;
using Plemu.Emulation.Native;

namespace Plemu.Emulation
{
    /// <summary>
    /// Owns the native handle after load and runs the frame loop on a dedicated thread, so
    /// <c>rh_run_frame</c> never blocks Unity's main thread. Per the ABI (libretro_host.h) every
    /// <c>rh_*</c> call for a handle must be serialized; this thread is that serialization point for
    /// everything except the two entry points the shim explicitly makes thread-safe —
    /// <c>rh_lock_frame</c>/<c>rh_unlock_frame</c> (mutex-guarded double buffer) and
    /// <c>rh_read_audio</c> (mutex-guarded ring) — which the video/audio surfaces call directly.
    /// </summary>
    /// <remarks>
    /// State operations (serialize / unserialize) and shutdown are marshalled here as commands and
    /// executed between frames, keeping the single-threaded-per-handle contract intact. Pacing is
    /// audio-master: the loop targets <c>av_info.fps</c> using a monotonic stopwatch, so it neither
    /// spins nor drifts. When paused it idles without calling the core.
    /// </remarks>
    internal sealed class EmulationThread
    {
        private readonly IntPtr _handle;
        private readonly double _framePeriodMs;
        private readonly Thread _thread;
        private readonly ConcurrentQueue<Command> _commands = new ConcurrentQueue<Command>();
        private readonly ManualResetEventSlim _resumeGate = new ManualResetEventSlim(false);

        private volatile bool _running;
        private volatile bool _paused;

        // Latest input snapshot, published by the input source, consumed each frame. Guarded by a
        // lock because the struct is larger than a word and both threads touch it.
        private readonly object _inputLock = new object();
        private LibretroHostNative.InputState _input;

        // Frame counter for diagnostics / tests (how many rh_run_frame calls have executed).
        private long _framesRun;

        public EmulationThread(IntPtr handle, double fps)
        {
            _handle = handle;
            double safeFps = (fps > 1.0 && fps < 1000.0) ? fps : 60.0;
            _framePeriodMs = 1000.0 / safeFps;
            _thread = new Thread(Run)
            {
                Name = "Plemu.EmulationThread",
                IsBackground = true,
            };
        }

        /// <summary>Frames executed so far (advances only while running and not paused).</summary>
        public long FramesRun => Interlocked.Read(ref _framesRun);

        public bool IsPaused => _paused;

        public void Start()
        {
            _running = true;
            _paused = false;
            _resumeGate.Set();
            _thread.Start();
        }

        public void Pause()
        {
            _paused = true;
            _resumeGate.Reset();
        }

        public void Resume()
        {
            _paused = false;
            _resumeGate.Set();
        }

        /// <summary>Publishes the input snapshot the next frame(s) will feed to the core.</summary>
        public void SetInput(LibretroHostNative.InputState input)
        {
            lock (_inputLock)
            {
                _input = input;
            }
        }

        /// <summary>
        /// Serializes the core state on the emu thread and returns the bytes. Blocks the caller
        /// until the command completes. Throws <see cref="EmulationException"/> on native failure.
        /// </summary>
        public byte[] SaveState()
        {
            Command cmd = Command.Save();
            Enqueue(cmd);
            cmd.Wait();
            if (cmd.Error != null)
                throw cmd.Error;
            return cmd.StateBytes;
        }

        /// <summary>
        /// Restores core state from <paramref name="state"/> on the emu thread. Blocks until done.
        /// Throws <see cref="EmulationException"/> on native failure.
        /// </summary>
        public void LoadState(byte[] state)
        {
            Command cmd = Command.Load(state);
            Enqueue(cmd);
            cmd.Wait();
            if (cmd.Error != null)
                throw cmd.Error;
        }

        /// <summary>
        /// Deterministically runs exactly <paramref name="count"/> frames on the emu thread and
        /// blocks until they complete, feeding the current input snapshot each frame. Intended for
        /// use while <see cref="Pause"/>d so no auto-run frames interleave — this is what makes
        /// savestate round-trips reproducible (mirrors the native harness's frame-exact stepping).
        /// </summary>
        public void StepFrames(int count)
        {
            if (count <= 0)
                return;
            Command cmd = Command.Step(count);
            Enqueue(cmd);
            cmd.Wait();
            if (cmd.Error != null)
                throw cmd.Error;
        }

        /// <summary>Stops the loop and joins the thread. Idempotent.</summary>
        public void Stop()
        {
            if (!_running && !_thread.IsAlive)
                return;

            _running = false;
            _resumeGate.Set(); // wake a paused loop so it can exit
            if (_thread.IsAlive)
                _thread.Join();
        }

        private void Enqueue(Command cmd)
        {
            _commands.Enqueue(cmd);
            _resumeGate.Set(); // ensure a paused loop wakes to service the command
        }

        private void Run()
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            double nextFrameMs = clock.Elapsed.TotalMilliseconds;

            while (_running)
            {
                DrainCommands();
                if (!_running)
                    break;

                if (_paused)
                {
                    // Idle without touching the core; wake on resume or a queued command.
                    _resumeGate.Wait(50);
                    nextFrameMs = clock.Elapsed.TotalMilliseconds;
                    continue;
                }

                double now = clock.Elapsed.TotalMilliseconds;
                if (now + 0.25 < nextFrameMs)
                {
                    // Ahead of schedule: sleep the remainder (coarse, then yield the tail).
                    double sleepMs = nextFrameMs - now;
                    if (sleepMs > 1.5)
                        Thread.Sleep((int)(sleepMs - 1.0));
                    else
                        Thread.Yield();
                    continue;
                }

                PushInputAndRunFrame();

                nextFrameMs += _framePeriodMs;
                // If we fell far behind (e.g. a long GC pause), resync rather than sprint.
                double drift = clock.Elapsed.TotalMilliseconds - nextFrameMs;
                if (drift > _framePeriodMs * 4.0)
                    nextFrameMs = clock.Elapsed.TotalMilliseconds + _framePeriodMs;
            }
        }

        private void PushInputAndRunFrame()
        {
            LibretroHostNative.InputState snapshot;
            lock (_inputLock)
            {
                snapshot = _input;
            }

            LibretroHostNative.rh_set_input(_handle, 0, ref snapshot);
            LibretroHostNative.rh_run_frame(_handle);
            Interlocked.Increment(ref _framesRun);
        }

        private void DrainCommands()
        {
            while (_commands.TryDequeue(out Command cmd))
            {
                try
                {
                    switch (cmd.Kind)
                    {
                        case CommandKind.Save:
                            cmd.StateBytes = DoSerialize();
                            break;
                        case CommandKind.Load:
                            DoUnserialize(cmd.StateBytes);
                            break;
                        case CommandKind.Step:
                            for (int i = 0; i < cmd.StepCount; i++)
                                PushInputAndRunFrame();
                            break;
                    }
                }
                catch (EmulationException ex)
                {
                    cmd.Error = ex;
                }
                finally
                {
                    cmd.Complete();
                }
            }
        }

        private byte[] DoSerialize()
        {
            UIntPtr size = LibretroHostNative.rh_serialize_size(_handle);
            ulong len = size.ToUInt64();
            if (len == 0)
                throw new EmulationException("rh_serialize_size returned 0 (core has no savestate)");

            byte[] buffer = new byte[len];
            int written = LibretroHostNative.rh_serialize(_handle, buffer, size);
            if (written != (int)len)
                throw new EmulationException("rh_serialize", new RhResult(written));
            return buffer;
        }

        private void DoUnserialize(byte[] state)
        {
            if (state == null || state.Length == 0)
                throw new EmulationException("LoadState called with empty state");

            int rc = LibretroHostNative.rh_unserialize(_handle, state, (UIntPtr)(ulong)state.Length);
            var result = new RhResult(rc);
            if (!result.IsOk)
                throw new EmulationException("rh_unserialize", result);
        }

        private enum CommandKind
        {
            Save,
            Load,
            Step,
        }

        private sealed class Command
        {
            private readonly ManualResetEventSlim _done = new ManualResetEventSlim(false);

            public CommandKind Kind { get; private set; }
            public byte[] StateBytes { get; set; }
            public int StepCount { get; private set; }
            public EmulationException Error { get; set; }

            public static Command Save() => new Command { Kind = CommandKind.Save };

            public static Command Load(byte[] state) =>
                new Command { Kind = CommandKind.Load, StateBytes = state };

            public static Command Step(int count) =>
                new Command { Kind = CommandKind.Step, StepCount = count };

            public void Complete() => _done.Set();

            public void Wait() => _done.Wait();
        }
    }
}
