/*
 * video.c — libretro video_refresh trampoline + double buffer.
 *
 * One responsibility: capture each finished frame into a stable, tightly-packed
 * front buffer the UI can read.
 *
 * Design (correctness over cleverness, per brief):
 *   - Two heap buffers sized max_w*max_h*4 (fits RGB565 or XRGB8888).
 *   - video_refresh writes row-by-row into the BACK buffer, collapsing the
 *     source pitch to a tight destination pitch (width * bpp). The UI therefore
 *     always sees pitch == width*bpp, regardless of core-side row padding.
 *   - On completion we take the mutex, flip `front`, publish geometry + bump
 *     `serial`, then release. rh_lock_frame reads the front buffer under the
 *     same mutex, keeping swap + serial atomic. Callers already serialize per
 *     handle (see libretro_host.h); the mutex only protects the publish.
 *
 * NULL / dupe handling: libretro allows video_refresh(NULL, ...) to mean "dupe
 * the previous frame" (we return GET_CAN_DUPE=true). We honor it by leaving the
 * front buffer as-is but still bumping serial so the UI knows a frame elapsed.
 */
#include <stdlib.h>
#include <string.h>

#include "internal.h"

static uint32_t rh_bpp_for_format(uint32_t host_format)
{
    return (host_format == RH_FORMAT_XRGB8888) ? 4u : 2u;
}

int rh_video_init(rh_video *v, uint32_t max_w, uint32_t max_h)
{
    if (v == NULL || max_w == 0 || max_h == 0) {
        return RH_ERR_BAD_ARG;
    }
    /* size for the widest format (XRGB8888 = 4 bpp) */
    const size_t cap = (size_t)max_w * (size_t)max_h * 4u;
    v->buf[0] = (uint8_t *)calloc(1, cap);
    v->buf[1] = (uint8_t *)calloc(1, cap);
    if (v->buf[0] == NULL || v->buf[1] == NULL) {
        free(v->buf[0]);
        free(v->buf[1]);
        v->buf[0] = NULL;
        v->buf[1] = NULL;
        return RH_ERR_IO;
    }
    v->cap = cap;
    v->front = 0;
    v->width = 0;
    v->height = 0;
    v->pitch = 0;
    v->format = RH_FORMAT_RGB565;
    v->serial = 0;
    v->have_frame = false;
    v->locked = false;
    if (pthread_mutex_init(&v->mtx, NULL) != 0) {
        free(v->buf[0]);
        free(v->buf[1]);
        v->buf[0] = NULL;
        v->buf[1] = NULL;
        v->cap = 0;
        return RH_ERR_IO;
    }
    return RH_OK;
}

void rh_video_free(rh_video *v)
{
    if (v == NULL) {
        return;
    }
    if (v->buf[0] != NULL || v->buf[1] != NULL) {
        if (v->locked) {
            v->locked = false;
            pthread_mutex_unlock(&v->mtx);
        }
        pthread_mutex_destroy(&v->mtx);
    }
    free(v->buf[0]);
    free(v->buf[1]);
    v->buf[0] = NULL;
    v->buf[1] = NULL;
    v->cap = 0;
    v->have_frame = false;
}

void rh_video_refresh_cb(const void *data, unsigned width, unsigned height, size_t pitch)
{
    rh_core *c = g_active;
    if (c == NULL) {
        return;
    }
    rh_video *v = &c->video;
    if (v->buf[0] == NULL) {
        return;
    }

    pthread_mutex_lock(&v->mtx);

    /* Dupe request: no new pixels, just advance serial (GET_CAN_DUPE). */
    if (data == NULL) {
        v->serial += 1u;
        v->have_frame = true; /* previous front buffer still valid */
        pthread_mutex_unlock(&v->mtx);
        return;
    }

    const uint32_t bpp = rh_bpp_for_format(c->host_format);
    const size_t dst_pitch = (size_t)width * bpp;

    /* Validate frame geometry bounds against allocation and pitch. */
    if (width > v->max_w || height > v->max_h || pitch < dst_pitch) {
        rh_log(c, RETRO_LOG_WARN, "video: invalid frame geometry %ux%u (pitch %zu, dst_pitch %zu)",
               width, height, pitch, dst_pitch);
        pthread_mutex_unlock(&v->mtx);
        return;
    }

    /* Clamp row count to our allocation. A misbehaving core reporting geometry
     * larger than max_w/max_h must never overrun the heap — truncate instead. */
    size_t rows = height;
    const size_t max_rows = (dst_pitch == 0) ? 0 : (v->cap / dst_pitch);
    if (rows > max_rows) {
        rh_log(c, RETRO_LOG_WARN, "video: frame %ux%u exceeds max buffer; truncating to %zu rows",
               width, height, max_rows);
        rows = max_rows;
    }

    const int back = v->front ^ 1;
    uint8_t *dst = v->buf[back];
    const uint8_t *src = (const uint8_t *)data;
    for (size_t y = 0; y < rows; ++y) {
        memcpy(dst + y * dst_pitch, src + (size_t)y * pitch, dst_pitch);
    }

    v->front = back;
    v->width = width;
    v->height = (uint32_t)rows;
    v->pitch = (uint32_t)dst_pitch;
    v->format = c->host_format;
    v->serial += 1u;
    v->have_frame = true;

    pthread_mutex_unlock(&v->mtx);
}
