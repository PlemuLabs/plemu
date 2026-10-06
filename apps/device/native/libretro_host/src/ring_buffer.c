/*
 * ring_buffer.c — lock-free SPSC stereo (interleaved s16) audio ring buffer.
 *
 * One responsibility: the ring mechanics, independent of libretro callbacks.
 * Overrun policy = DROP OLDEST: when a push would exceed capacity we advance the
 * tail to make room and increment `drops` by the number of frames discarded.
 *
 * Single producer (emulation thread), single consumer (audio thread).
 * Occupancy is derived as `head - tail` using unsigned wraparound, eliminating
 * the read-modify-write data race on a shared `count` field.
 *
 * Frame == one stereo pair (2 * int16_t). All counts below are in FRAMES.
 */
#include <stdlib.h>
#include <string.h>
#include <stdatomic.h>

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
    atomic_init(&r->head, 0);
    atomic_init(&r->tail, 0);
    atomic_init(&r->drops, 0);
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
    atomic_store_explicit(&r->head, 0, memory_order_relaxed);
    atomic_store_explicit(&r->tail, 0, memory_order_relaxed);
}

void rh_ring_reset(rh_ring *r)
{
    if (r == NULL) {
        return;
    }
    atomic_store_explicit(&r->head, 0, memory_order_relaxed);
    atomic_store_explicit(&r->tail, 0, memory_order_relaxed);
    /* drops is a lifetime counter; intentionally not reset here. */
}

uint32_t rh_ring_push(rh_ring *r, const int16_t *frames, uint32_t n)
{
    if (r == NULL || r->data == NULL || frames == NULL || n == 0 || r->capacity_frames == 0) {
        return 0;
    }

    const uint32_t cap = r->capacity_frames;
    uint32_t head = atomic_load_explicit(&r->head, memory_order_relaxed);
    uint32_t tail = atomic_load_explicit(&r->tail, memory_order_acquire);

    /* If the incoming block alone is larger than the ring, keep only the most
     * recent `cap` frames; everything before that is dropped up front. */
    if (n >= cap) {
        const uint32_t skipped = n - cap;
        atomic_fetch_add_explicit(&r->drops, skipped, memory_order_relaxed);
        frames += (size_t)skipped * 2u;
        n = cap;
        tail = head;
        atomic_store_explicit(&r->tail, tail, memory_order_release);
    }

    /* Make room by dropping oldest frames if needed. */
    uint32_t occupancy = head - tail;
    uint32_t free_frames = (occupancy >= cap) ? 0 : (cap - occupancy);
    if (n > free_frames) {
        const uint32_t need = n - free_frames;
        tail += need;
        atomic_store_explicit(&r->tail, tail, memory_order_release);
        atomic_fetch_add_explicit(&r->drops, need, memory_order_relaxed);
    }

    /* Write n frames, wrapping at capacity. */
    for (uint32_t i = 0; i < n; ++i) {
        const uint32_t idx = (head + i) % cap;
        r->data[(size_t)idx * 2u + 0u] = frames[(size_t)i * 2u + 0u];
        r->data[(size_t)idx * 2u + 1u] = frames[(size_t)i * 2u + 1u];
    }

    atomic_store_explicit(&r->head, head + n, memory_order_release);
    return n;
}

uint32_t rh_ring_pop(rh_ring *r, int16_t *dst, uint32_t n)
{
    if (r == NULL || r->data == NULL || dst == NULL || n == 0 || r->capacity_frames == 0) {
        return 0;
    }

    const uint32_t cap = r->capacity_frames;
    uint32_t head = atomic_load_explicit(&r->head, memory_order_acquire);
    uint32_t tail = atomic_load_explicit(&r->tail, memory_order_relaxed);

    const uint32_t avail = head - tail;
    const uint32_t take = (n < avail) ? n : avail;

    for (uint32_t i = 0; i < take; ++i) {
        const uint32_t idx = (tail + i) % cap;
        dst[(size_t)i * 2u + 0u] = r->data[(size_t)idx * 2u + 0u];
        dst[(size_t)i * 2u + 1u] = r->data[(size_t)idx * 2u + 1u];
    }

    atomic_store_explicit(&r->tail, tail + take, memory_order_release);
    return take;
}
