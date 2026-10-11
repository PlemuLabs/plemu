using System;
using UnityEngine;

namespace Plemu.Emulation
{
    /// <summary>
    /// Input provider for the PlaySolana PSG1 handheld console and standard gamepads.
    /// Polls physical gamepad buttons and D-Pad / analog axes, mapping them to the
    /// 16-bit RetroPad bitmask expected by libretro cores.
    /// </summary>
    public sealed class PSG1InputSource
    {
        public const float DefaultDeadZone = 0.5f;

        private readonly RetroPad _gamepadPad;
        private readonly float _deadZone;

        /// <summary>Injectable probe for whether a button is currently pressed. Defaults to Input.GetKey.</summary>
        public Func<KeyCode, bool> ButtonProbe { get; set; } = DefaultButtonProbe;

        /// <summary>Injectable probe for analog / D-Pad axis values. Defaults to Input.GetAxisRaw.</summary>
        public Func<string, float> AxisProbe { get; set; } = DefaultAxisProbe;

        public PSG1InputSource(RetroPad gamepadPad = null, float deadZone = DefaultDeadZone)
        {
            _gamepadPad = gamepadPad ?? RetroPad.DefaultPsg1GamepadMapping;
            _deadZone = deadZone;
        }

        /// <summary>
        /// Reads both directional axes and physical buttons, returning the folded 16-bit RetroPad bitmask.
        /// </summary>
        public ushort ToBitmask()
        {
            ushort mask = _gamepadPad.ToBitmask(ButtonProbe);

            // Read directional axes (D-Pad / Left Stick)
            float horiz = AxisProbe("Horizontal");
            float vert = AxisProbe("Vertical");

            if (horiz < -_deadZone)
                mask |= (ushort)(1u << RetroPad.Left);
            else if (horiz > _deadZone)
                mask |= (ushort)(1u << RetroPad.Right);

            if (vert > _deadZone)
                mask |= (ushort)(1u << RetroPad.Up);
            else if (vert < -_deadZone)
                mask |= (ushort)(1u << RetroPad.Down);

            return mask;
        }

        private static bool DefaultButtonProbe(KeyCode key)
        {
            return Input.GetKey(key);
        }

        private static float DefaultAxisProbe(string axisName)
        {
            try
            {
                return Input.GetAxisRaw(axisName);
            }
            catch
            {
                // In headless tests or if axis is unmapped in InputManager, return 0
                return 0.0f;
            }
        }
    }
}
