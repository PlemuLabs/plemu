/*
 * ring_buffer.c — fixed-capacity stereo (interleaved s16) audio ring.
 *
 * One responsibility: the ring mechanics, independent of libretro callbacks.
 * Overrun policy = DROP OLDEST: when a push would exceed capacity we advance the
 * tail to make room and increment `drops` by the number of frames discarded. A
 * full-buffer read/write consumer can therefore never block or reject audio; it
 * just loses the oldest samples, which is the correct behavior for a realtime
 * emulator feeding a UI at a slightly different rate.
 *
 * Frame == one stereo pair (2 * int16_t). All counts below are in FRAMES.
 */
#include <stdlib.h>
#include <string.h>

#include "internal.h"

int rh_ring_init(rh_ring *r, uint32_t capacity_frames)
{
    if (r == NULL || capacity_frames == 0) {
        return RH_ERR_BAD_ARG;
    }
    r->data = (int16_t *)calloc((size_t)capacity_frames * 2u, sizeof(int16_t));
    if (r->data == NULL) {
        return RH_ERR_IO;
    }
    r->capacity_frames = capacity_frames;
    r->head = 0;
    r->tail = 0;
    r->count = 0;
    r->drops = 0;
    return RH_OK;
}

void rh_ring_free(rh_ring *r)
{
    if (r == NULL) {
        return;
    }
    free(r->data);
    r->data = NULL;
    r->capacity_frames = 0;
    r->head = 0;
    r->tail = 0;
    r->count = 0;
}

void rh_ring_reset(rh_ring *r)
{
    if (r == NULL) {
        return;
    }
    r->head = 0;
    r->tail = 0;
    r->count = 0;
    /* drops is a lifetime counter; intentionally not reset here. */
}

uint32_t rh_ring_push(rh_ring *r, const int16_t *frames, uint32_t n)
{
    if (r == NULL || r->data == NULL || frames == NULL || n == 0 || r->capacity_frames == 0) {
        return 0;
    }

    const uint32_t cap = r->capacity_frames;

    /* If the incoming block alone is larger than the ring, keep only the most
     * recent `cap` frames; everything before that is dropped up front. */
    if (n >= cap) {
        const uint32_t skipped = n - cap;
        r->drops += skipped;
        frames += (size_t)skipped * 2u;
        n = cap;
        /* buffer will be fully overwritten below */
        r->head = 0;
        r->tail = 0;
        r->count = 0;
    }

    /* Make room by dropping oldest frames if needed. */
    const uint32_t free_frames = cap - r->count;
    if (n > free_frames) {
        const uint32_t need = n - free_frames;
        r->tail = (r->tail + need) % cap;
        r->count -= need;
        r->drops += need;
    }

    /* Write n frames, wrapping at capacity. */
    for (uint32_t i = 0; i < n; ++i) {
        const uint32_t idx = (r->head + i) % cap;
        r->data[(size_t)idx * 2u + 0u] = frames[(size_t)i * 2u + 0u];
        r->data[(size_t)idx * 2u + 1u] = frames[(size_t)i * 2u + 1u];
    }
    r->head = (r->head + n) % cap;
    r->count += n;
    return n;
}

uint32_t rh_ring_pop(rh_ring *r, int16_t *dst, uint32_t n)
{
    if (r == NULL || r->data == NULL || dst == NULL || n == 0 || r->capacity_frames == 0) {
        return 0;
    }

    const uint32_t cap = r->capacity_frames;
    const uint32_t avail = r->count;
    const uint32_t take = (n < avail) ? n : avail;

    for (uint32_t i = 0; i < take; ++i) {
        const uint32_t idx = (r->tail + i) % cap;
        dst[(size_t)i * 2u + 0u] = r->data[(size_t)idx * 2u + 0u];
        dst[(size_t)i * 2u + 1u] = r->data[(size_t)idx * 2u + 1u];
    }
    r->tail = (r->tail + take) % cap;
    r->count -= take;
    return take;
}
