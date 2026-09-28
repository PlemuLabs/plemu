/*
 * libretro_host.h — stable C ABI between the Unity (IL2CPP/C#) frontend and
 * dlopen'd libretro emulator cores on the PlaySolana PSG1 (Android arm64).
 *
 * This header is the FROZEN contract. The C# layer only ever P/Invokes the
 * rh_* functions declared here; all libretro callbacks stay native-side.
 *
 * ---------------------------------------------------------------------------
 * THREADING / LIFECYCLE CONTRACT (read this)
 * ---------------------------------------------------------------------------
 * 1. Single-threaded per handle: the caller MUST serialize every rh_* call for
 *    a given handle. The host does not lock the public API surface. (The video
 *    double-buffer is internally mutex-protected only so rh_lock_frame can hand
 *    out a stable pointer; see video.c.)
 *
 * 2. EXACTLY ONE live core per process (v0 limitation). libretro cores install
 *    process-global callbacks via retro_set_environment / retro_set_video_refresh
 *    / etc., so two cores in one process would clobber each other's routing.
 *    The host enforces this: a second rh_load_core() while another handle already
 *    has a core loaded returns RH_ERR_BUSY. rh_unload_game() + rh_destroy() (or a
 *    fresh rh_load_core after rh_destroy) release the slot. A later task may lift
 *    this by moving cores into subprocesses.
 *
 * 3. Every function is NULL-safe on the handle: passing NULL never dereferences,
 *    it returns an error code (for int32_t returns), 0 (for size_t / uint32_t /
 *    float), or is a no-op (for void returns).
 *
 * 4. HW-render entry points are declared here now but return -1/0 and log
 *    "hw_render not implemented in v0". A later task fills the GL path.
 * ---------------------------------------------------------------------------
 */
#ifndef LIBRETRO_HOST_H
#define LIBRETRO_HOST_H

#include <stddef.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/* Export marker. The host is built with -fvisibility=hidden so ONLY the rh_*
 * functions below are exported; internal helpers (logging, ring, video, dylib,
 * environment/input trampolines) stay private and cannot collide with a core's
 * symbols. This keeps the C ABI narrow — the C# layer P/Invokes only these. */
#if defined(_WIN32)
#define RH_API __declspec(dllexport)
#else
#define RH_API __attribute__((visibility("default")))
#endif

/* Error codes: 0 = success, negative = error. */
#define RH_OK 0
#define RH_ERR_NULL_HANDLE (-1)     /* NULL handle or NULL required argument     */
#define RH_ERR_NO_CORE (-2)         /* core not loaded on this handle            */
#define RH_ERR_NO_GAME (-3)         /* game not loaded on this handle            */
#define RH_ERR_DLOPEN (-4)          /* dlopen failed / missing retro_* symbols   */
#define RH_ERR_LOAD_GAME (-5)       /* retro_load_game returned false            */
#define RH_ERR_BUSY (-6)            /* another core is already live in-process   */
#define RH_ERR_BAD_ARG (-7)         /* invalid non-handle argument               */
#define RH_ERR_IO (-8)              /* file read failure (need_fullpath=false)   */
#define RH_ERR_STATE (-9)           /* serialize/unserialize failed in the core  */
#define RH_ERR_UNSUPPORTED (-10)    /* not implemented in v0 (e.g. hw_render)    */
#define RH_ERR_NO_FRAME (-11)       /* no frame produced yet                     */

/* Pixel format for rh_frame.format. NOTE: this is the HOST ABI enum and does
 * NOT match the libretro RETRO_PIXEL_FORMAT_* values — the host translates. */
#define RH_FORMAT_RGB565 0u
#define RH_FORMAT_XRGB8888 1u

typedef void *rh_handle;
typedef void (*rh_log_fn)(int level, const char *msg);

typedef struct {
    const char *system_dir;
    const char *save_dir;
    rh_log_fn log;
    int hw_mode; /* 0 = software (only supported path in v0) */
} rh_create_desc;

typedef struct {
    const void *data;
    uint32_t width, height, pitch;
    uint32_t format; /* RH_FORMAT_RGB565 or RH_FORMAT_XRGB8888 */
    uint64_t serial;
} rh_frame;

typedef struct {
    uint16_t buttons;    /* bit N = RETRO_DEVICE_ID_JOYPAD_N pressed */
    int16_t lx, ly, rx, ry;
    int16_t ptr_x, ptr_y;
    uint8_t ptr_pressed;
} rh_input_state;

typedef struct {
    double fps;
    double sample_rate;
    uint32_t base_w, base_h, max_w, max_h;
    float aspect;
} rh_av_info;

/* --- Lifecycle -------------------------------------------------------------- */
RH_API int32_t rh_abi_version(void); /* returns 1 */
RH_API rh_handle rh_create(const rh_create_desc *desc);
RH_API void rh_destroy(rh_handle h); /* safe on NULL and half-initialized handles */
RH_API int32_t rh_load_core(rh_handle h, const char *core_path);
RH_API int32_t rh_load_game(rh_handle h, const char *path); /* honors need_fullpath */
RH_API void rh_unload_game(rh_handle h);

/* --- AV / run --------------------------------------------------------------- */
RH_API void rh_get_av_info(rh_handle h, rh_av_info *out);
RH_API void rh_run_frame(rh_handle h); /* one retro_run */

/* --- Video (double-buffered; frame is stable between lock/unlock) ----------- */
RH_API int32_t rh_lock_frame(rh_handle h, rh_frame *out); /* 0 ok */
RH_API void rh_unlock_frame(rh_handle h);

/* --- Audio (fixed ring, stereo interleaved s16) ----------------------------- */
RH_API int32_t rh_read_audio(rh_handle h, int16_t *dst, int32_t max_frames); /* frames read */
RH_API float rh_audio_fill(rh_handle h);                                     /* 0..1 occupancy */

/* --- Input ------------------------------------------------------------------ */
RH_API void rh_set_input(rh_handle h, uint32_t port, const rh_input_state *in);

/* --- Savestates ------------------------------------------------------------- */
RH_API size_t rh_serialize_size(rh_handle h);
RH_API int32_t rh_serialize(rh_handle h, uint8_t *dst, size_t cap);
RH_API int32_t rh_unserialize(rh_handle h, const uint8_t *src, size_t len);

/* --- Memory regions --------------------------------------------------------- */
RH_API size_t rh_memory_size(rh_handle h, uint32_t id);
RH_API int32_t rh_memory_read(rh_handle h, uint32_t id, uint8_t *dst, size_t cap);
RH_API int32_t rh_memory_write(rh_handle h, uint32_t id, const uint8_t *src, size_t len);

/* --- HW-render (stubs in v0; a later task implements) ----------------------- */
RH_API int32_t rh_hw_requested(rh_handle h);      /* 0 = software (always, in v0) */
RH_API uint32_t rh_hw_color_texture(rh_handle h); /* 0 (no GL texture in v0)      */
RH_API void *rh_render_event_fn(void);            /* NULL (no GL callback in v0)  */

#ifdef __cplusplus
}
#endif

#endif /* LIBRETRO_HOST_H */
