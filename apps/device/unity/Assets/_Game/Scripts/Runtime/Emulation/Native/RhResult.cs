using Plemu.Emulation.Native;

namespace Plemu.Emulation
{
    /// <summary>
    /// A typed wrapper over the <c>rh_*</c> integer return codes so callers cannot silently ignore
    /// a native failure. <see cref="Code"/> is the raw ABI code (0 = success, negative = error).
    /// </summary>
    public readonly struct RhResult
    {
        public int Code { get; }

        public RhResult(int code) => Code = code;

        public bool IsOk => Code == LibretroHostNative.RH_OK;

        /// <summary>A short human-readable name for the ABI code, for logs and exceptions.</summary>
        public string Describe()
        {
            switch (Code)
            {
                case LibretroHostNative.RH_OK: return "RH_OK";
                case LibretroHostNative.RH_ERR_NULL_HANDLE: return "RH_ERR_NULL_HANDLE";
                case LibretroHostNative.RH_ERR_NO_CORE: return "RH_ERR_NO_CORE";
                case LibretroHostNative.RH_ERR_NO_GAME: return "RH_ERR_NO_GAME";
                case LibretroHostNative.RH_ERR_DLOPEN: return "RH_ERR_DLOPEN";
                case LibretroHostNative.RH_ERR_LOAD_GAME: return "RH_ERR_LOAD_GAME";
                case LibretroHostNative.RH_ERR_BUSY: return "RH_ERR_BUSY";
                case LibretroHostNative.RH_ERR_BAD_ARG: return "RH_ERR_BAD_ARG";
                case LibretroHostNative.RH_ERR_IO: return "RH_ERR_IO";
                case LibretroHostNative.RH_ERR_STATE: return "RH_ERR_STATE";
                case LibretroHostNative.RH_ERR_UNSUPPORTED: return "RH_ERR_UNSUPPORTED";
                case LibretroHostNative.RH_ERR_NO_FRAME: return "RH_ERR_NO_FRAME";
                default: return $"RH_ERR_UNKNOWN({Code})";
            }
        }

        public override string ToString() => Describe();
    }

    /// <summary>Thrown when a native <c>rh_*</c> call the session cannot recover from fails.</summary>
    public sealed class EmulationException : System.Exception
    {
        public RhResult Result { get; }

        public EmulationException(string operation, RhResult result)
            : base($"{operation} failed: {result.Describe()}")
        {
            Result = result;
        }

        public EmulationException(string message)
            : base(message)
        {
        }
    }
}
