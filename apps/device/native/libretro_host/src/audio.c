/*
 * audio.c — libretro audio callback trampolines.
 *
 * One responsibility: bridge retro_audio_sample (single) and
 * retro_audio_sample_batch (block) into the active core's fixed ring
 * (ring_buffer.c). Both callbacks are process-global and route through g_active.
 *
 * Capacity: >= 256 ms at 48 kHz stereo. 48000 * 0.256 = 12288 frames; we round
 * up to 16384 frames (~341 ms) so a UI that drains a bit late never underflows
 * the buffer during normal 60 fps operation (800 frames/run for the stub core).
 */
#include "internal.h"

#define RH_AUDIO_RING_FRAMES 16384u /* ~341 ms @ 48 kHz stereo */

int rh_audio_ring_init(rh_core *c)
{
    if (c == NULL) {
        return RH_ERR_NULL_HANDLE;
    }
    return rh_ring_init(&c->audio, RH_AUDIO_RING_FRAMES);
}

void rh_audio_sample_cb(int16_t left, int16_t right)
{
    rh_core *c = g_active;
    if (c == NULL) {
        return;
    }
    const int16_t pair[2] = {left, right};
    (void)rh_ring_push(&c->audio, pair, 1u);
}

size_t rh_audio_sample_batch_cb(const int16_t *data, size_t frames)
{
    rh_core *c = g_active;
    if (c == NULL || data == NULL || frames == 0) {
        return 0;
    }
    /* libretro passes frame counts; the ring works in frames too.
     * Guard the uint32 cast for absurd block sizes. */
    uint32_t n = (frames > 0xFFFFFFFFu) ? 0xFFFFFFFFu : (uint32_t)frames;
    (void)rh_ring_push(&c->audio, data, n);
    /* Contract: return the number of frames "consumed" — we always accept the
     * whole block (drop-oldest happens internally), so report `frames`. */
    return frames;
}
