using System;
using System.Runtime.InteropServices;

namespace Plemu.Emulation.Native
{
    /// <summary>
    /// P/Invoke bindings for the <c>libretro_host</c> C shim (ABI v1, frozen in
    /// <c>libretro_host/include/libretro_host.h</c>). This is the only C# type that touches the
    /// native surface; the emulation facade drives it and all libretro callbacks stay native-side.
    /// </summary>
    /// <remarks>
    /// The library name <c>"libretro_host"</c> resolves to <c>libretro_host.dylib</c> in
    /// <c>Assets/Plugins/macOS</c> in the editor (see the plugin <c>.meta</c>). The threading
    /// contract from the header holds: the caller MUST serialize every <c>rh_*</c> call for a
    /// given handle. <see cref="Plemu.Emulation.EmulationThread"/> owns the handle after load, so
    /// all calls are serialized onto that one thread.
    /// TODO (M5, device): on Android the shared object is <c>libretro_host.so</c>; the same
    /// entry-point name resolves via the Android plugin import settings, no code change here.
    /// </remarks>
    internal static class LibretroHostNative
    {
        private const string Lib = "libretro_host";

        // --- ABI enums / error codes (mirror libretro_host.h exactly) ---------------------------
        public const int RH_OK = 0;
        public const int RH_ERR_NULL_HANDLE = -1;
        public const int RH_ERR_NO_CORE = -2;
        public const int RH_ERR_NO_GAME = -3;
        public const int RH_ERR_DLOPEN = -4;
        public const int RH_ERR_LOAD_GAME = -5;
        public const int RH_ERR_BUSY = -6;
        public const int RH_ERR_BAD_ARG = -7;
        public const int RH_ERR_IO = -8;
        public const int RH_ERR_STATE = -9;
        public const int RH_ERR_UNSUPPORTED = -10;
        public const int RH_ERR_NO_FRAME = -11;

        public const uint RH_FORMAT_RGB565 = 0u;
        public const uint RH_FORMAT_XRGB8888 = 1u;

        // --- ABI structs (blittable, layout must match the C header) ----------------------------

        /// <summary>Mirror of C <c>rh_create_desc</c>.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct CreateDesc
        {
            public IntPtr SystemDir; // const char*
            public IntPtr SaveDir; // const char*
            public IntPtr Log; // rh_log_fn (NULL: no logging)
            public int HwMode; // 0 = software
        }

        /// <summary>Mirror of C <c>rh_frame</c>. <see cref="Data"/> is owned by the host.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct Frame
        {
            public IntPtr Data; // const void* into the host front buffer
            public uint Width;
            public uint Height;
            public uint Pitch;
            public uint Format; // RH_FORMAT_*
            public ulong Serial;
        }

        /// <summary>Mirror of C <c>rh_input_state</c>.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct InputState
        {
            public ushort Buttons; // bit N = RETRO_DEVICE_ID_JOYPAD_N pressed
            public short Lx;
            public short Ly;
            public short Rx;
            public short Ry;
            public short PtrX;
            public short PtrY;
            public byte PtrPressed;
        }

        /// <summary>Mirror of C <c>rh_av_info</c>.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct AvInfo
        {
            public double Fps;
            public double SampleRate;
            public uint BaseW;
            public uint BaseH;
            public uint MaxW;
            public uint MaxH;
            public float Aspect;
        }

        // --- Lifecycle --------------------------------------------------------------------------
        [DllImport(Lib)]
        public static extern int rh_abi_version();

        [DllImport(Lib)]
        public static extern IntPtr rh_create(ref CreateDesc desc);

        [DllImport(Lib)]
        public static extern void rh_destroy(IntPtr h);

        [DllImport(Lib, CharSet = CharSet.Ansi)]
        public static extern int rh_load_core(IntPtr h, string corePath);

        [DllImport(Lib, CharSet = CharSet.Ansi)]
        public static extern int rh_load_game(IntPtr h, string path);

        [DllImport(Lib)]
        public static extern void rh_unload_game(IntPtr h);

        // --- AV / run ---------------------------------------------------------------------------
        [DllImport(Lib)]
        public static extern void rh_get_av_info(IntPtr h, out AvInfo outInfo);

        [DllImport(Lib)]
        public static extern void rh_run_frame(IntPtr h);

        // --- Video ------------------------------------------------------------------------------
        [DllImport(Lib)]
        public static extern int rh_lock_frame(IntPtr h, out Frame outFrame);

        [DllImport(Lib)]
        public static extern void rh_unlock_frame(IntPtr h);

        // --- Audio ------------------------------------------------------------------------------
        [DllImport(Lib)]
        public static extern int rh_read_audio(IntPtr h, IntPtr dst, int maxFrames);

        [DllImport(Lib)]
        public static extern float rh_audio_fill(IntPtr h);

        // --- Input ------------------------------------------------------------------------------
        [DllImport(Lib)]
        public static extern void rh_set_input(IntPtr h, uint port, ref InputState input);

        // --- Savestates -------------------------------------------------------------------------
        [DllImport(Lib)]
        public static extern UIntPtr rh_serialize_size(IntPtr h);

        [DllImport(Lib)]
        public static extern int rh_serialize(IntPtr h, byte[] dst, UIntPtr cap);

        [DllImport(Lib)]
        public static extern int rh_unserialize(IntPtr h, byte[] src, UIntPtr len);
    }
}
