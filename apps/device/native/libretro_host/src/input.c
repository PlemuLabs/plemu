/*
 * input.c — libretro input callback trampolines.
 *
 * One responsibility: answer retro_input_state from the last rh_set_input
 * snapshot for the requested port. input_poll is a no-op (the frontend pushes
 * state; there is nothing to poll).
 *
 * Supported devices:
 *   - RETRO_DEVICE_JOYPAD: id RETRO_DEVICE_ID_JOYPAD_MASK (256) returns the full
 *     16-bit button bitmask (when the core requested GET_INPUT_BITMASKS); any
 *     other id returns 1/0 for that single button bit.
 *   - RETRO_DEVICE_ANALOG: index LEFT/RIGHT + id X/Y -> lx/ly/rx/ry.
 *   - RETRO_DEVICE_POINTER: id X/Y/PRESSED -> ptr_x/ptr_y/ptr_pressed.
 * Everything else returns 0.
 */
#include "internal.h"

void rh_input_poll_cb(void)
{
    /* no-op: state is pushed via rh_set_input */
}

int16_t rh_input_state_cb(unsigned port, unsigned device, unsigned index, unsigned id)
{
    rh_core *c = g_active;
    if (c == NULL) {
        return 0;
    }
    const unsigned max_ports = (unsigned)(sizeof(c->input) / sizeof(c->input[0]));
    if (port >= max_ports) {
        return 0;
    }
    const rh_input_state *s = &c->input[port];

    switch (device) {
    case RETRO_DEVICE_JOYPAD:
        if (id == RETRO_DEVICE_ID_JOYPAD_MASK) {
            /* Full bitmask query (libretro's GET_INPUT_BITMASKS path). */
            return (int16_t)s->buttons;
        }
        if (id < 16u) {
            return (s->buttons & (uint16_t)(1u << id)) ? 1 : 0;
        }
        return 0;

    case RETRO_DEVICE_ANALOG:
        if (index == RETRO_DEVICE_INDEX_ANALOG_LEFT) {
            if (id == RETRO_DEVICE_ID_ANALOG_X) {
                return s->lx;
            }
            if (id == RETRO_DEVICE_ID_ANALOG_Y) {
                return s->ly;
            }
        } else if (index == RETRO_DEVICE_INDEX_ANALOG_RIGHT) {
            if (id == RETRO_DEVICE_ID_ANALOG_X) {
                return s->rx;
            }
            if (id == RETRO_DEVICE_ID_ANALOG_Y) {
                return s->ry;
            }
        } else if (index == RETRO_DEVICE_INDEX_ANALOG_BUTTON) {
            /* Analog button pressure from the matching digital bit. */
            if (id < 16u) {
                return (s->buttons & (uint16_t)(1u << id)) ? 0x7fff : 0;
            }
        }
        return 0;

    case RETRO_DEVICE_POINTER:
        switch (id) {
        case RETRO_DEVICE_ID_POINTER_X:
            return s->ptr_x;
        case RETRO_DEVICE_ID_POINTER_Y:
            return s->ptr_y;
        case RETRO_DEVICE_ID_POINTER_PRESSED:
            return s->ptr_pressed ? 1 : 0;
        default:
            return 0;
        }

    default:
        return 0;
    }
}
