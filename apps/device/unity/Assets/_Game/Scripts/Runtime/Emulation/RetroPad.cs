using System.Collections.Generic;

namespace Plemu.Emulation
{
    /// <summary>
    /// The libretro RetroPad button bit layout (RETRO_DEVICE_ID_JOYPAD_*), plus a keyboard mapping
    /// that turns pressed <see cref="UnityEngine.KeyCode"/>s into the 16-bit bitmask the shim's
    /// <c>rh_set_input</c> expects (<c>bit N = RETRO_DEVICE_ID_JOYPAD_N pressed</c>).
    /// </summary>
    public sealed class RetroPad
    {
        // RetroPad button bit indices (libretro.h RETRO_DEVICE_ID_JOYPAD_*).
        public const int B = 0;
        public const int Y = 1;
        public const int Select = 2;
        public const int Start = 3;
        public const int Up = 4;
        public const int Down = 5;
        public const int Left = 6;
        public const int Right = 7;
        public const int A = 8;
        public const int X = 9;
        public const int L = 10;
        public const int R = 11;

        private readonly IReadOnlyList<KeyBinding> _bindings;

        public RetroPad(IReadOnlyList<KeyBinding> bindings)
        {
            _bindings = bindings;
        }

        /// <summary>The bindings that make up this mapping.</summary>
        public IReadOnlyList<KeyBinding> Bindings => _bindings;

        /// <summary>
        /// The default editor keyboard mapping for a Game Boy (per the M4 brief): arrows = D-Pad,
        /// Z = A, X = B, Enter = Start, Right-Shift / Backspace = Select.
        /// </summary>
        public static RetroPad DefaultGameBoyMapping => new RetroPad(new[]
        {
            new KeyBinding(UnityEngine.KeyCode.UpArrow, Up),
            new KeyBinding(UnityEngine.KeyCode.DownArrow, Down),
            new KeyBinding(UnityEngine.KeyCode.LeftArrow, Left),
            new KeyBinding(UnityEngine.KeyCode.RightArrow, Right),
            new KeyBinding(UnityEngine.KeyCode.Z, A),
            new KeyBinding(UnityEngine.KeyCode.X, B),
            new KeyBinding(UnityEngine.KeyCode.Return, Start),
            new KeyBinding(UnityEngine.KeyCode.KeypadEnter, Start),
            new KeyBinding(UnityEngine.KeyCode.RightShift, Select),
            new KeyBinding(UnityEngine.KeyCode.Backspace, Select),
        });

        /// <summary>
        /// The default physical gamepad mapping for the PlaySolana PSG1 handheld.
        /// Maps standard joystick buttons: 0=A, 1=B, 2=X, 3=Y, 4=L, 5=R, 6=Select, 7=Start.
        /// </summary>
        public static RetroPad DefaultPsg1GamepadMapping => new RetroPad(new[]
        {
            new KeyBinding(UnityEngine.KeyCode.JoystickButton0, A),
            new KeyBinding(UnityEngine.KeyCode.JoystickButton1, B),
            new KeyBinding(UnityEngine.KeyCode.JoystickButton2, X),
            new KeyBinding(UnityEngine.KeyCode.JoystickButton3, Y),
            new KeyBinding(UnityEngine.KeyCode.JoystickButton4, L),
            new KeyBinding(UnityEngine.KeyCode.JoystickButton5, R),
            new KeyBinding(UnityEngine.KeyCode.JoystickButton6, Select),
            new KeyBinding(UnityEngine.KeyCode.JoystickButton7, Start),
        });

        /// <summary>
        /// Folds a set of currently-pressed keys into the RetroPad bitmask. <paramref name="isDown"/>
        /// reports whether a key is held (injectable so tests need no real keyboard).
        /// </summary>
        public ushort ToBitmask(System.Func<UnityEngine.KeyCode, bool> isDown)
        {
            ushort mask = 0;
            for (int i = 0; i < _bindings.Count; i++)
            {
                KeyBinding binding = _bindings[i];
                if (isDown(binding.Key))
                    mask |= (ushort)(1u << binding.Button);
            }
            return mask;
        }

        /// <summary>A single keyboard-key → RetroPad-button binding.</summary>
        public readonly struct KeyBinding
        {
            public UnityEngine.KeyCode Key { get; }
            public int Button { get; }

            public KeyBinding(UnityEngine.KeyCode key, int button)
            {
                Key = key;
                Button = button;
            }
        }
    }
}
