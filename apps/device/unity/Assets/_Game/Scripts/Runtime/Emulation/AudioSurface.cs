using UnityEngine;

namespace Plemu.Emulation
{
    /// <summary>
    /// Pulls emulator audio from the shim's ring buffer on Unity's audio thread. Each
    /// <see cref="OnAudioFilterRead"/> reads up to the requested number of stereo frames from
    /// <c>rh_read_audio</c> (interleaved s16) and converts them to interleaved float. Underrun
    /// (ring emptier than requested) is filled with silence so playback never clicks or repeats.
    /// </summary>
    /// <remarks>
    /// The read goes through <see cref="EmulationSession.TryReadAudio"/>, which holds the session's
    /// teardown lock so an in-flight read cannot overlap <c>rh_destroy</c> freeing the ring — this
    /// closes the audio-thread use-after-free on stop. Requires an <see cref="AudioSource"/> on the
    /// same GameObject (with Play() called) for Unity to drive the filter callback.
    /// </remarks>
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioSurface : MonoBehaviour
    {
        private const float S16ToFloat = 1f / 32768f;

        private EmulationSession _session;
        private short[] _scratch = new short[8192]; // interleaved s16 (frames*2)
        private long _framesRead;
        private long _nonZeroSamples;

        /// <summary>Total audio frames pulled from the ring since binding (diagnostic).</summary>
        public long FramesRead => System.Threading.Interlocked.Read(ref _framesRead);

        /// <summary>Count of non-zero samples observed (proves the core produced sound).</summary>
        public long NonZeroSamples => System.Threading.Interlocked.Read(ref _nonZeroSamples);

        /// <summary>Binds the surface to a loaded session. Pass null to detach (emits silence).</summary>
        public void Bind(EmulationSession session)
        {
            _session = session;
        }

        /// <summary>
        /// Runs the exact production pull+convert path against a caller-provided buffer, for tests
        /// that must observe audio deterministically without Unity's DSP thread (which does not run
        /// reliably in batchmode). Returns the buffer so the caller can inspect the samples.
        /// </summary>
        public float[] PumpForTest(int frames, int channels = 2)
        {
            float[] buffer = new float[frames * channels];
            OnAudioFilterRead(buffer, channels);
            return buffer;
        }

        private unsafe void OnAudioFilterRead(float[] data, int channels)
        {
            EmulationSession session = _session;
            if (session == null || channels <= 0)
            {
                System.Array.Clear(data, 0, data.Length);
                return;
            }

            int frames = data.Length / channels;
            EnsureScratch(frames * 2);

            // The session performs the actual rh_read_audio under its teardown lock and returns 0
            // (silence) if the handle has been / is being destroyed — no use-after-free here.
            int got;
            fixed (short* dst = _scratch)
            {
                got = session.TryReadAudio((System.IntPtr)dst, frames);
            }

            long nonZero = 0;
            for (int f = 0; f < frames; f++)
            {
                float left = 0f;
                float right = 0f;
                if (f < got)
                {
                    short sl = _scratch[f * 2];
                    short sr = _scratch[f * 2 + 1];
                    left = sl * S16ToFloat;
                    right = sr * S16ToFloat;
                    if (sl != 0 || sr != 0)
                        nonZero++;
                }

                int baseIdx = f * channels;
                if (channels == 1)
                {
                    data[baseIdx] = (left + right) * 0.5f;
                }
                else
                {
                    data[baseIdx] = left;
                    data[baseIdx + 1] = right;
                    for (int c = 2; c < channels; c++)
                        data[baseIdx + c] = 0f;
                }
            }

            if (got > 0)
                System.Threading.Interlocked.Add(ref _framesRead, got);
            if (nonZero > 0)
                System.Threading.Interlocked.Add(ref _nonZeroSamples, nonZero);
        }

        private void EnsureScratch(int neededShorts)
        {
            if (_scratch.Length < neededShorts)
                _scratch = new short[neededShorts];
        }
    }
}
