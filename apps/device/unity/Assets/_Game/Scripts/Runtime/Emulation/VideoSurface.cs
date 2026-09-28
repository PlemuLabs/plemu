using Plemu.Emulation.Native;
using UnityEngine;
using UnityEngine.UI;

namespace Plemu.Emulation
{
    /// <summary>
    /// Renders the emulator's video into a <see cref="RawImage"/>. Each <see cref="LateUpdate"/> it
    /// locks the shim's double-buffered front frame (a thread-safe entry point) and, only when the
    /// frame serial advanced, uploads the tightly-packed framebuffer into a <see cref="Texture2D"/>
    /// then unlocks. Format handling: RGB565 → <see cref="TextureFormat.RGB565"/>, XRGB8888 →
    /// <see cref="TextureFormat.BGRA32"/> (libretro's little-endian XRGB is B,G,R,X in memory).
    /// </summary>
    /// <remarks>
    /// libretro frames are top-down; Unity textures are bottom-up, so the target <see cref="RawImage"/>
    /// UV is V-flipped. The image is integer-scaled to the largest whole multiple that fits the
    /// display rect, preserving the core's pixel aspect (no blur, no non-uniform stretch).
    /// This is the only component that uploads to the GPU / touches the main thread for video.
    /// </remarks>
    public sealed class VideoSurface : MonoBehaviour
    {
        [SerializeField] private RawImage _target;
        [SerializeField] private RectTransform _displayArea;

        private EmulationSession _session;
        private Texture2D _texture;
        private ulong _lastSerial;
        private uint _texWidth;
        private uint _texHeight;
        private uint _texFormat = uint.MaxValue;
        private long _framesUploaded;

        /// <summary>The texture the framebuffer is uploaded into (null before the first frame).</summary>
        public Texture2D Texture => _texture;

        /// <summary>How many distinct frames have been uploaded (advances on serial change).</summary>
        public long FramesUploaded => _framesUploaded;

        /// <summary>The serial of the most recently uploaded frame.</summary>
        public ulong LastSerial => _lastSerial;

        /// <summary>Binds the surface to a loaded session. Pass null to detach.</summary>
        public void Bind(EmulationSession session)
        {
            _session = session;
            _lastSerial = 0;
        }

        /// <summary>Assigns the render targets in code (for scenes built at runtime / tests).</summary>
        public void Configure(RawImage target, RectTransform displayArea)
        {
            _target = target;
            _displayArea = displayArea;
        }

        private void LateUpdate()
        {
            if (_session == null || !_session.IsRunning)
                return;

            System.IntPtr handle = _session.Handle;
            if (handle == System.IntPtr.Zero)
                return;

            int locked = LibretroHostNative.rh_lock_frame(handle, out LibretroHostNative.Frame frame);
            if (locked != LibretroHostNative.RH_OK)
                return;

            try
            {
                if (frame.Data == System.IntPtr.Zero || frame.Width == 0 || frame.Height == 0)
                    return;
                if (frame.Serial == _lastSerial)
                    return; // no new pixels since last upload

                EnsureTexture(frame.Width, frame.Height, frame.Format);

                // Host guarantees a tight front buffer (pitch == width*bpp), so the raw bytes match
                // the tightly-packed Texture2D layout exactly.
                int byteCount = (int)(frame.Pitch * frame.Height);
                _texture.LoadRawTextureData(frame.Data, byteCount);
                _lastSerial = frame.Serial;
            }
            finally
            {
                LibretroHostNative.rh_unlock_frame(handle);
            }

            // Apply + present after unlocking so the GPU upload never holds the frame lock.
            _texture.Apply(false);
            _framesUploaded++;
            PresentScaled();
        }

        private void EnsureTexture(uint width, uint height, uint format)
        {
            if (_texture != null && width == _texWidth && height == _texHeight && format == _texFormat)
                return;

            TextureFormat texFormat = format == LibretroHostNative.RH_FORMAT_XRGB8888
                ? TextureFormat.BGRA32
                : TextureFormat.RGB565;

            _texture = new Texture2D((int)width, (int)height, texFormat, mipChain: false)
            {
                // Crisp, no-bleed retro pixels.
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "PlemuVideo",
            };
            _texWidth = width;
            _texHeight = height;
            _texFormat = format;

            if (_target != null)
            {
                _target.texture = _texture;
                // libretro is top-down; flip V so the picture is upright.
                _target.uvRect = new Rect(0f, 1f, 1f, -1f);
            }
        }

        private void PresentScaled()
        {
            if (_target == null || _texture == null)
                return;

            RectTransform targetRect = _target.rectTransform;
            Vector2 area = _displayArea != null ? _displayArea.rect.size : targetRect.rect.size;
            if (area.x <= 0f || area.y <= 0f)
                return;

            float pixelAspect = _session != null ? _session.Aspect : 0f;
            float contentW = _texture.width;
            float contentH = _texture.height;
            if (pixelAspect > 0f)
            {
                // Honor the core's declared display aspect by widening/narrowing the content box.
                contentW = contentH * pixelAspect;
            }

            // Largest integer scale that fits; never smaller than 1x.
            float scale = Mathf.Min(area.x / contentW, area.y / contentH);
            int intScale = Mathf.Max(1, Mathf.FloorToInt(scale));
            float w = contentW * intScale;
            float h = contentH * intScale;

            targetRect.sizeDelta = new Vector2(w, h);
        }

        private void OnDestroy()
        {
            if (_texture != null)
            {
                Destroy(_texture);
                _texture = null;
            }
        }
    }
}
