/*
 * libretro_host.c — public ABI implementation + core lifecycle.
 *
 * One responsibility: own the rh_core handle lifecycle and the public rh_*
 * surface, wiring the environment/video/audio/input trampolines into a dlopen'd
 * core. See libretro_host.h for the frozen ABI and the threading / one-live-core
 * contract; see internal.h for the shared state.
 */
#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "internal.h"

/* The single live core. libretro callbacks are process-global, so only one core
 * may be live at a time; g_active is the routing target for every trampoline. */
rh_core *g_active = NULL;

/* ---- logging --------------------------------------------------------------- */
void rh_log(rh_core *c, int level, const char *fmt, ...)
{
    if (c == NULL || c->log == NULL || fmt == NULL) {
        return;
    }
    char msg[1024];
    va_list ap;
    va_start(ap, fmt);
    int n = vsnprintf(msg, sizeof(msg), fmt, ap);
    va_end(ap);
    if (n < 0) {
        return;
    }
    c->log(level, msg);
}

/* ---- small helpers --------------------------------------------------------- */
static char *rh_strdup_or_null(const char *s)
{
    return (s != NULL) ? strdup(s) : NULL;
}

/* map libretro RETRO_PIXEL_FORMAT_* -> host RH_FORMAT_* (only for the default) */
static uint32_t rh_host_format_default(void)
{
    /* libretro's documented default before SET_PIXEL_FORMAT is 0RGB1555, which
     * we don't support; default our view to RGB565 and rely on the core calling
     * SET_PIXEL_FORMAT (the stub core does). */
    return RH_FORMAT_RGB565;
}

/* ---- abi version ----------------------------------------------------------- */
int32_t rh_abi_version(void)
{
    return 1;
}

/* ---- create / destroy ------------------------------------------------------ */
rh_handle rh_create(const rh_create_desc *desc)
{
    if (desc == NULL) {
        return NULL;
    }
    rh_core *c = (rh_core *)calloc(1, sizeof(rh_core));
    if (c == NULL) {
        return NULL;
    }
    c->log = desc->log;
    c->system_dir = rh_strdup_or_null(desc->system_dir);
    c->save_dir = rh_strdup_or_null(desc->save_dir);
    c->hw_mode = desc->hw_mode;
    c->host_format = rh_host_format_default();
    c->core_loaded = false;
    c->game_loaded = false;
    c->inited = false;
    c->input_bitmasks = false;
    c->var_dirty = false;

    if (desc->hw_mode != 0) {
        rh_log(c, RETRO_LOG_WARN,
               "rh_create: hw_mode=%d requested but hw_render not implemented in v0; "
               "falling back to software",
               desc->hw_mode);
    }
    return (rh_handle)c;
}

void rh_destroy(rh_handle h)
{
    rh_core *c = (rh_core *)h;
    if (c == NULL) {
        return; /* NULL-safe */
    }

    /* Tear down game + core if still loaded (safe on half-initialized handles:
     * every step checks its own precondition). */
    if (c->game_loaded && c->fn.unload_game != NULL) {
        c->fn.unload_game();
    }
    c->game_loaded = false;

    if (c->inited && c->fn.deinit != NULL) {
        c->fn.deinit();
    }
    c->inited = false;

    /* This core is releasing the process-global slot. */
    if (g_active == c) {
        g_active = NULL;
    }

    rh_dylib_close(c);
    c->core_loaded = false;

    rh_video_free(&c->video);
    rh_ring_free(&c->audio);

    for (int i = 0; i < c->var_count; ++i) {
        free(c->vars[i].key);
        free(c->vars[i].value);
    }
    free(c->system_dir);
    free(c->save_dir);
    free(c);
}

/* ---- load core ------------------------------------------------------------- */
int32_t rh_load_core(rh_handle h, const char *core_path)
{
    rh_core *c = (rh_core *)h;
    if (c == NULL) {
        return RH_ERR_NULL_HANDLE;
    }
    if (core_path == NULL) {
        return RH_ERR_BAD_ARG;
    }
    if (c->core_loaded) {
        rh_log(c, RETRO_LOG_ERROR, "rh_load_core: this handle already has a core");
        return RH_ERR_BUSY;
    }
    /* One-live-core guard: another handle already owns the global slot. */
    if (g_active != NULL && g_active != c) {
        rh_log(c, RETRO_LOG_ERROR,
               "rh_load_core: another core is already live in this process (v0 allows one)");
        return RH_ERR_BUSY;
    }

    int rc = rh_dylib_open(c, core_path);
    if (rc != RH_OK) {
        return rc;
    }

    unsigned api = c->fn.api_version();
    if (api != RETRO_API_VERSION) {
        rh_log(c, RETRO_LOG_WARN, "rh_load_core: core api_version %u != host %u", api,
               (unsigned)RETRO_API_VERSION);
    }

    /* Claim the global slot BEFORE installing callbacks so the trampolines
     * (which read g_active) resolve to this core during retro_init. */
    g_active = c;

    c->fn.set_environment(rh_environment_cb);
    c->fn.set_video_refresh(rh_video_refresh_cb);
    c->fn.set_audio_sample(rh_audio_sample_cb);
    c->fn.set_audio_sample_batch(rh_audio_sample_batch_cb);
    c->fn.set_input_poll(rh_input_poll_cb);
    c->fn.set_input_state(rh_input_state_cb);

    c->fn.init();
    c->inited = true;
    c->core_loaded = true;

    /* Capture need_fullpath now (retro_get_system_info is valid post-init). */
    struct retro_system_info si;
    memset(&si, 0, sizeof(si));
    c->fn.get_system_info(&si);
    c->need_fullpath = si.need_fullpath;

    rh_log(c, RETRO_LOG_INFO, "rh_load_core: loaded '%s' (%s %s, need_fullpath=%d)", core_path,
           si.library_name ? si.library_name : "?", si.library_version ? si.library_version : "?",
           (int)si.need_fullpath);
    return RH_OK;
}

/* ---- load / unload game ---------------------------------------------------- */
static int rh_read_file(rh_core *c, const char *path, void **out_data, size_t *out_size)
{
    *out_data = NULL;
    *out_size = 0;
    FILE *f = fopen(path, "rb");
    if (f == NULL) {
        rh_log(c, RETRO_LOG_ERROR, "rh_load_game: cannot open '%s'", path);
        return RH_ERR_IO;
    }
    if (fseek(f, 0, SEEK_END) != 0) {
        fclose(f);
        return RH_ERR_IO;
    }
    long sz = ftell(f);
    if (sz < 0) {
        fclose(f);
        return RH_ERR_IO;
    }
    if (fseek(f, 0, SEEK_SET) != 0) {
        fclose(f);
        return RH_ERR_IO;
    }
    /* Allocate at least 1 byte so a valid non-NULL pointer is handed to the core
     * even for empty content (e.g. /dev/null). */
    size_t alloc = (sz > 0) ? (size_t)sz : 1u;
    void *buf = malloc(alloc);
    if (buf == NULL) {
        fclose(f);
        return RH_ERR_IO;
    }
    size_t rd = (sz > 0) ? fread(buf, 1, (size_t)sz, f) : 0u;
    fclose(f);
    if (sz > 0 && rd != (size_t)sz) {
        free(buf);
        return RH_ERR_IO;
    }
    *out_data = buf;
    *out_size = (size_t)sz;
    return RH_OK;
}

int32_t rh_load_game(rh_handle h, const char *path)
{
    rh_core *c = (rh_core *)h;
    if (c == NULL) {
        return RH_ERR_NULL_HANDLE;
    }
    if (!c->core_loaded) {
        return RH_ERR_NO_CORE;
    }
    if (c->game_loaded) {
        rh_log(c, RETRO_LOG_ERROR, "rh_load_game: a game is already loaded");
        return RH_ERR_BUSY;
    }

    struct retro_game_info gi;
    memset(&gi, 0, sizeof(gi));
    gi.path = path;

    void *filedata = NULL;
    size_t filesize = 0;

    if (!c->need_fullpath) {
        if (path == NULL) {
            rh_log(c, RETRO_LOG_ERROR, "rh_load_game: need_fullpath=false but path is NULL");
            return RH_ERR_BAD_ARG;
        }
        int rc = rh_read_file(c, path, &filedata, &filesize);
        if (rc != RH_OK) {
            return rc;
        }
        gi.data = filedata;
        gi.size = filesize;
    }

    /* Allocate video + audio buffers from the AV info sized to max geometry.
     * retro_get_system_av_info is valid after retro_init (pre-load) per libretro;
     * we call it here so SET_PIXEL_FORMAT (done in retro_load_game by many cores)
     * has already set host_format by the time frames arrive. */
    memset(&c->av, 0, sizeof(c->av));
    c->fn.get_system_av_info(&c->av);

    uint32_t max_w = c->av.geometry.max_width ? c->av.geometry.max_width : c->av.geometry.base_width;
    uint32_t max_h =
        c->av.geometry.max_height ? c->av.geometry.max_height : c->av.geometry.base_height;
    if (max_w == 0 || max_h == 0) {
        free(filedata);
        rh_log(c, RETRO_LOG_ERROR, "rh_load_game: core reported zero geometry");
        return RH_ERR_LOAD_GAME;
    }

    int rc = rh_video_init(&c->video, max_w, max_h);
    if (rc != RH_OK) {
        free(filedata);
        return rc;
    }
    rc = rh_audio_ring_init(c);
    if (rc != RH_OK) {
        rh_video_free(&c->video);
        free(filedata);
        return rc;
    }

    bool ok = c->fn.load_game(&gi);

    /* The core copies what it needs from gi.data during load_game; free our copy. */
    free(filedata);

    if (!ok) {
        rh_video_free(&c->video);
        rh_ring_free(&c->audio);
        rh_log(c, RETRO_LOG_ERROR, "rh_load_game: retro_load_game returned false");
        return RH_ERR_LOAD_GAME;
    }

    /* Re-read AV info: load_game may have refined geometry/timing. */
    c->fn.get_system_av_info(&c->av);
    c->game_loaded = true;
    rh_log(c, RETRO_LOG_INFO, "rh_load_game: '%s' loaded (%.0fx%.0f @ %.2ffps)",
           path ? path : "(null)", (double)c->av.geometry.base_width,
           (double)c->av.geometry.base_height, c->av.timing.fps);
    return RH_OK;
}

void rh_unload_game(rh_handle h)
{
    rh_core *c = (rh_core *)h;
    if (c == NULL || !c->game_loaded) {
        return;
    }
    if (c->fn.unload_game != NULL) {
        c->fn.unload_game();
    }
    c->game_loaded = false;
    rh_video_free(&c->video);
    rh_ring_free(&c->audio);
}

/* ---- av info / run --------------------------------------------------------- */
void rh_get_av_info(rh_handle h, rh_av_info *out)
{
    if (out == NULL) {
        return;
    }
    memset(out, 0, sizeof(*out));
    rh_core *c = (rh_core *)h;
    if (c == NULL || !c->game_loaded) {
        return;
    }
    out->fps = c->av.timing.fps;
    out->sample_rate = c->av.timing.sample_rate;
    out->base_w = c->av.geometry.base_width;
    out->base_h = c->av.geometry.base_height;
    out->max_w = c->av.geometry.max_width;
    out->max_h = c->av.geometry.max_height;
    out->aspect = c->av.geometry.aspect_ratio;
}

void rh_run_frame(rh_handle h)
{
    rh_core *c = (rh_core *)h;
    if (c == NULL || !c->game_loaded || c->fn.run == NULL) {
        return;
    }
    /* Route callbacks to this core for the duration of the run. Under the
     * one-live-core contract g_active already == c, but set it defensively. */
    g_active = c;
    c->fn.run();
}

/* ---- video lock ------------------------------------------------------------ */
int32_t rh_lock_frame(rh_handle h, rh_frame *out)
{
    rh_core *c = (rh_core *)h;
    if (c == NULL) {
        return RH_ERR_NULL_HANDLE;
    }
    if (out == NULL) {
        return RH_ERR_BAD_ARG;
    }
    if (!c->game_loaded) {
        return RH_ERR_NO_GAME;
    }
    rh_video *v = &c->video;
    pthread_mutex_lock(&v->mtx);
    if (!v->have_frame) {
        pthread_mutex_unlock(&v->mtx);
        return RH_ERR_NO_FRAME;
    }
    v->locked = true;
    out->data = v->buf[v->front];
    out->width = v->width;
    out->height = v->height;
    out->pitch = v->pitch;
    out->format = v->format;
    out->serial = v->serial;
    /* Hold the mutex until rh_unlock_frame so the front buffer stays stable. */
    return RH_OK;
}

void rh_unlock_frame(rh_handle h)
{
    rh_core *c = (rh_core *)h;
    if (c == NULL || !c->game_loaded) {
        return;
    }
    rh_video *v = &c->video;
    if (v->locked) {
        v->locked = false;
        pthread_mutex_unlock(&v->mtx);
    }
}

/* ---- audio ----------------------------------------------------------------- */
int32_t rh_read_audio(rh_handle h, int16_t *dst, int32_t max_frames)
{
    rh_core *c = (rh_core *)h;
    if (c == NULL) {
        return RH_ERR_NULL_HANDLE;
    }
    if (dst == NULL || max_frames <= 0) {
        return RH_ERR_BAD_ARG;
    }
    if (!c->game_loaded) {
        return RH_ERR_NO_GAME;
    }
    uint32_t got = rh_ring_pop(&c->audio, dst, (uint32_t)max_frames);
    return (int32_t)got;
}

float rh_audio_fill(rh_handle h)
{
    rh_core *c = (rh_core *)h;
    if (c == NULL || !c->game_loaded || c->audio.capacity_frames == 0) {
        return 0.0f;
    }
    return (float)c->audio.count / (float)c->audio.capacity_frames;
}

/* ---- input ----------------------------------------------------------------- */
void rh_set_input(rh_handle h, uint32_t port, const rh_input_state *in)
{
    rh_core *c = (rh_core *)h;
    if (c == NULL || in == NULL) {
        return;
    }
    const uint32_t max_ports = (uint32_t)(sizeof(c->input) / sizeof(c->input[0]));
    if (port >= max_ports) {
        return;
    }
    c->input[port] = *in;
}

/* ---- savestates ------------------------------------------------------------ */
size_t rh_serialize_size(rh_handle h)
{
    rh_core *c = (rh_core *)h;
    if (c == NULL || !c->game_loaded || c->fn.serialize_size == NULL) {
        return 0;
    }
    return c->fn.serialize_size();
}

int32_t rh_serialize(rh_handle h, uint8_t *dst, size_t cap)
{
    rh_core *c = (rh_core *)h;
    if (c == NULL) {
        return RH_ERR_NULL_HANDLE;
    }
    if (dst == NULL) {
        return RH_ERR_BAD_ARG;
    }
    if (!c->game_loaded || c->fn.serialize == NULL) {
        return RH_ERR_NO_GAME;
    }
    size_t need = c->fn.serialize_size();
    if (cap < need) {
        rh_log(c, RETRO_LOG_ERROR, "rh_serialize: buffer %zu < required %zu", cap, need);
        return RH_ERR_BAD_ARG;
    }
    if (!c->fn.serialize(dst, need)) {
        return RH_ERR_STATE;
    }
    return (int32_t)need;
}

int32_t rh_unserialize(rh_handle h, const uint8_t *src, size_t len)
{
    rh_core *c = (rh_core *)h;
    if (c == NULL) {
        return RH_ERR_NULL_HANDLE;
    }
    if (src == NULL) {
        return RH_ERR_BAD_ARG;
    }
    if (!c->game_loaded || c->fn.unserialize == NULL) {
        return RH_ERR_NO_GAME;
    }
    if (!c->fn.unserialize(src, len)) {
        return RH_ERR_STATE;
    }
    return RH_OK;
}

/* ---- memory regions -------------------------------------------------------- */
size_t rh_memory_size(rh_handle h, uint32_t id)
{
    rh_core *c = (rh_core *)h;
    if (c == NULL || !c->game_loaded || c->fn.get_memory_size == NULL) {
        return 0;
    }
    return c->fn.get_memory_size(id);
}

int32_t rh_memory_read(rh_handle h, uint32_t id, uint8_t *dst, size_t cap)
{
    rh_core *c = (rh_core *)h;
    if (c == NULL) {
        return RH_ERR_NULL_HANDLE;
    }
    if (dst == NULL) {
        return RH_ERR_BAD_ARG;
    }
    if (!c->game_loaded || c->fn.get_memory_data == NULL || c->fn.get_memory_size == NULL) {
        return RH_ERR_NO_GAME;
    }
    size_t sz = c->fn.get_memory_size(id);
    void *mem = c->fn.get_memory_data(id);
    if (sz == 0 || mem == NULL) {
        return 0; /* region not present */
    }
    size_t n = (cap < sz) ? cap : sz;
    memcpy(dst, mem, n);
    return (int32_t)n;
}

int32_t rh_memory_write(rh_handle h, uint32_t id, const uint8_t *src, size_t len)
{
    rh_core *c = (rh_core *)h;
    if (c == NULL) {
        return RH_ERR_NULL_HANDLE;
    }
    if (src == NULL) {
        return RH_ERR_BAD_ARG;
    }
    if (!c->game_loaded || c->fn.get_memory_data == NULL || c->fn.get_memory_size == NULL) {
        return RH_ERR_NO_GAME;
    }
    size_t sz = c->fn.get_memory_size(id);
    void *mem = c->fn.get_memory_data(id);
    if (sz == 0 || mem == NULL) {
        return 0;
    }
    size_t n = (len < sz) ? len : sz;
    memcpy(mem, src, n);
    return (int32_t)n;
}

/* ---- HW-render stubs (v0) -------------------------------------------------- */
int32_t rh_hw_requested(rh_handle h)
{
    rh_core *c = (rh_core *)h;
    if (c == NULL) {
        return RH_ERR_NULL_HANDLE;
    }
    /* Software path only in v0. */
    return 0;
}

uint32_t rh_hw_color_texture(rh_handle h)
{
    rh_core *c = (rh_core *)h;
    if (c != NULL) {
        rh_log(c, RETRO_LOG_DEBUG, "rh_hw_color_texture: hw_render not implemented in v0");
    }
    return 0;
}

void *rh_render_event_fn(void)
{
    /* No GL render callback in v0; a later task provides one. */
    return NULL;
}
