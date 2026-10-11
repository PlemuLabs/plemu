using NUnit.Framework;
using Plemu.Emulation;
using UnityEngine;

namespace Plemu.Tests
{
    [TestFixture]
    public sealed class PSG1InputSourceTests
    {
        [Test]
        public void ToBitmask_WhenNoInput_ReturnsZero()
        {
            var source = new PSG1InputSource
            {
                ButtonProbe = _ => false,
                AxisProbe = _ => 0.0f
            };

            ushort mask = source.ToBitmask();
            Assert.That(mask, Is.EqualTo(0));
        }

        [Test]
        public void ToBitmask_DirectionalAxes_FoldIntoCorrectDpadBits()
        {
            var source = new PSG1InputSource
            {
                ButtonProbe = _ => false,
                AxisProbe = axis => axis switch
                {
                    "Horizontal" => -1.0f, // Left
                    "Vertical" => 1.0f,    // Up
                    _ => 0.0f
                }
            };

            ushort mask = source.ToBitmask();
            ushort expected = (ushort)((1u << RetroPad.Left) | (1u << RetroPad.Up));
            Assert.That(mask, Is.EqualTo(expected));
        }

        [Test]
        public void ToBitmask_ActionButtons_FoldIntoCorrectRetroPadBits()
        {
            var source = new PSG1InputSource
            {
                ButtonProbe = key => key switch
                {
                    KeyCode.JoystickButton0 => true, // A
                    KeyCode.JoystickButton7 => true, // Start
                    _ => false
                },
                AxisProbe = _ => 0.0f
            };

            ushort mask = source.ToBitmask();
            ushort expected = (ushort)((1u << RetroPad.A) | (1u << RetroPad.Start));
            Assert.That(mask, Is.EqualTo(expected));
        }

        [Test]
        public void ToBitmask_CompositeChord_MergesAxesAndButtons()
        {
            var source = new PSG1InputSource
            {
                ButtonProbe = key => key switch
                {
                    KeyCode.JoystickButton1 => true, // B
                    KeyCode.JoystickButton4 => true, // L
                    KeyCode.JoystickButton5 => true, // R
                    _ => false
                },
                AxisProbe = axis => axis == "Horizontal" ? 1.0f : 0.0f // Right
            };

            ushort mask = source.ToBitmask();
            ushort expected = (ushort)(
                (1u << RetroPad.Right) |
                (1u << RetroPad.B) |
                (1u << RetroPad.L) |
                (1u << RetroPad.R));

            Assert.That(mask, Is.EqualTo(expected));
        }
    }
}
