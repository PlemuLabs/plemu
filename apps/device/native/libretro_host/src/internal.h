/*
 * internal.h — private shared state for the libretro host implementation.
 *
 * Not part of the public ABI. Defines the rh_core handle backing rh_handle and
 * the cross-file prototypes used by libretro_host.c, environment.c, video.c,
 * audio.c, input.c, ring_buffer.c and dylib.c.
 *
 * libretro installs callbacks process-globally, so we keep a single file-scope
 * pointer (g_active) to the one live core; the environment/video/audio/input
 * trampolines route through it. See libretro_host.h for the one-live-core rule.
 */
#ifndef RH_INTERNAL_H
#define RH_INTERNAL_H

#include <pthread.h>
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#include "libretro.h"
#include "libretro_host.h"

/* ---- Video double buffer --------------------------------------------------- */
/* Two heap buffers sized to max_w*max_h*4 (fits XRGB8888 or RGB565). The core's
 * video_refresh writes into the back buffer; on completion we swap under a mutex
 * so rh_lock_frame can publish a stable read-only view of the front buffer. */
typedef struct {
    uint8_t *buf[2]; /* two framebuffers                                    */
    size_t cap;      /* bytes allocated per buffer                          */
    int front;       /* index of the last completed frame                  */
    uint32_t width, height, pitch;
    uint32_t format;   /* RH_FORMAT_* (host enum)                           */
    uint64_t serial;   /* frame counter, bumped each completed frame        */
    bool have_frame;   /* at least one frame produced                       */
    pthread_mutex_t mtx;
    bool locked; /* rh_lock_frame outstanding (guards swap timing)          */
} rh_video;

/* ---- Audio ring (fixed capacity, stereo interleaved s16) ------------------- */
#include <stdatomic.h>

typedef struct {
    int16_t *data; /* capacity_frames * 2 samples                           */
    uint32_t capacity_frames;
    _Atomic uint32_t head; /* next write frame index (producer writes)      */
    _Atomic uint32_t tail; /* next read frame index (consumer writes)       */
    _Atomic uint32_t drops;  /* frames dropped due to overrun               */
} rh_ring;

/* ---- Minimal core-variable map (SET_VARIABLES / GET_VARIABLE) -------------- */
#define RH_MAX_VARS 32
typedef struct {
    char *key;
    char *value;
} rh_var;

/* ---- The handle ------------------------------------------------------------ */
typedef struct rh_core {
    /* dynamic library + resolved retro_* symbols */
    void *dl; /* dlopen handle                                              */
    struct {
        void (*set_environment)(retro_environment_t);
        void (*set_video_refresh)(retro_video_refresh_t);
        void (*set_audio_sample)(retro_audio_sample_t);
        void (*set_audio_sample_batch)(retro_audio_sample_batch_t);
        void (*set_input_poll)(retro_input_poll_t);
        void (*set_input_state)(retro_input_state_t);
        void (*init)(void);
        void (*deinit)(void);
        unsigned (*api_version)(void);
        void (*get_system_info)(struct retro_system_info *);
        void (*get_system_av_info)(struct retro_system_av_info *);
        void (*set_controller_port_device)(unsigned, unsigned);
        void (*reset)(void);
        void (*run)(void);
        size_t (*serialize_size)(void);
        bool (*serialize)(void *, size_t);
        bool (*unserialize)(const void *, size_t);
        bool (*load_game)(const struct retro_game_info *);
        void (*unload_game)(void);
        unsigned (*get_region)(void);
        void *(*get_memory_data)(unsigned);
        size_t (*get_memory_size)(unsigned);
    } fn;

    bool core_loaded;
    bool game_loaded;
    bool inited; /* retro_init called                                        */
    bool need_fullpath;

    rh_log_fn log;
    char *system_dir;
    char *save_dir;
    int hw_mode;

    struct retro_system_av_info av; /* cached from retro_get_system_av_info  */
    uint32_t host_format;           /* RH_FORMAT_* set via SET_PIXEL_FORMAT  */

    rh_video video;
    rh_ring audio;

    rh_input_state input[8]; /* per-port last snapshot                       */
    bool input_bitmasks;     /* core asked for GET_INPUT_BITMASKS            */

    rh_var vars[RH_MAX_VARS];
    int var_count;
    bool var_dirty; /* GET_VARIABLE_UPDATE flag                             */

    /* one-time "unknown environment cmd" logging bookkeeping */
    uint32_t logged_unknown[8]; /* small dedup set of cmd numbers            */
    int logged_unknown_count;
} rh_core;

/* The single live core (process-global libretro callback routing). */
extern rh_core *g_active;

/* ---- Cross-file helpers ---------------------------------------------------- */

/* logging: level uses RETRO_LOG_* semantics; safe if c or c->log is NULL. */
void rh_log(rh_core *c, int level, const char *fmt, ...);

/* environment.c */
bool rh_environment_cb(unsigned cmd, void *data);

/* video.c */
int rh_video_init(rh_video *v, uint32_t max_w, uint32_t max_h);
void rh_video_free(rh_video *v);
void rh_video_refresh_cb(const void *data, unsigned width, unsigned height, size_t pitch);

/* audio.c */
int rh_ring_init(rh_ring *r, uint32_t capacity_frames);
void rh_ring_free(rh_ring *r);
int rh_audio_ring_init(rh_core *c); /* allocates the ring at the fixed capacity */
void rh_audio_sample_cb(int16_t left, int16_t right);
size_t rh_audio_sample_batch_cb(const int16_t *data, size_t frames);

/* ring_buffer.c (pure ring mechanics, callback-independent, unit-testable) */
void rh_ring_reset(rh_ring *r);
uint32_t rh_ring_push(rh_ring *r, const int16_t *frames, uint32_t n); /* returns pushed */
uint32_t rh_ring_pop(rh_ring *r, int16_t *dst, uint32_t n);           /* returns popped */

/* input.c */
void rh_input_poll_cb(void);
int16_t rh_input_state_cb(unsigned port, unsigned device, unsigned index, unsigned id);

/* dylib.c */
int rh_dylib_open(rh_core *c, const char *path); /* RH_OK or RH_ERR_DLOPEN */
void rh_dylib_close(rh_core *c);

#endif /* RH_INTERNAL_H */
