/*
 * stub_libretro.c — deterministic libretro test core (software / RGB565).
 *
 * This is NOT an emulator; it is a fixture whose output the smoke harness can
 * decode and assert against, proving the host ABI works before any real core.
 *
 * Contract implemented:
 *   - System: 320x240 RGB565 @ 60 fps, 48000 Hz stereo. max geometry == base.
 *   - need_fullpath = true; retro_load_game accepts ANY path (content ignored)
 *     and requests RGB565 via SET_PIXEL_FORMAT.
 *   - Video per retro_run:
 *       * Frame counter is incremented FIRST, then drawn, so the Nth run shows
 *         counter value N (run once => counter 1).
 *       * First 64 pixels of row 0 encode the 32-bit counter, LSB first, 2
 *         pixels per bit: bit=1 -> two 0xFFFF pixels, bit=0 -> two 0x0000.
 *       * The rest of the framebuffer holds a moving diagonal pattern derived
 *         from the counter. Holding joypad B (id 0) inverts that pattern (bitwise
 *         complement of each pixel), giving the harness an input-path assertion.
 *         Row 0's counter band is drawn AFTER the pattern, so inversion never
 *         corrupts the counter.
 *   - Audio per retro_run: exactly 800 stereo frames of a 440 Hz square wave,
 *     amplitude 8000, emitted via audio_sample_batch.
 *   - Savestate: 16 bytes = "STUB" + u32 counter (LE) + 8 zero bytes.
 *   - Memory: exposes SYSTEM_RAM (a small static buffer) for completeness.
 */
#include <stdint.h>
#include <string.h>

#include "libretro.h"

#define STUB_WIDTH 320u
#define STUB_HEIGHT 240u
#define STUB_FPS 60.0
#define STUB_SAMPLE_RATE 48000.0
#define STUB_AUDIO_FRAMES_PER_RUN 800u /* 48000 / 60 */
#define STUB_SQUARE_HZ 440.0
#define STUB_AMPLITUDE 8000
#define STUB_COUNTER_BITS 32u
#define STUB_PIXELS_PER_BIT 2u
#define STUB_SYSRAM_BYTES 256u

/* ---- callbacks provided by the host --------------------------------------- */
static retro_environment_t s_environ;
static retro_video_refresh_t s_video;
static retro_audio_sample_t s_audio_sample;
static retro_audio_sample_batch_t s_audio_batch;
static retro_input_poll_t s_input_poll;
static retro_input_state_t s_input_state;

/* ---- core state ------------------------------------------------------------ */
static uint32_t s_frame_counter;
static uint16_t s_framebuffer[STUB_WIDTH * STUB_HEIGHT];
static int16_t s_audio[STUB_AUDIO_FRAMES_PER_RUN * 2u];
static uint32_t s_audio_phase; /* running sample index for the square wave */
static uint8_t s_sysram[STUB_SYSRAM_BYTES];
static bool s_game_loaded;

/* ---- callback setters ------------------------------------------------------ */
RETRO_API void retro_set_environment(retro_environment_t cb)
{
    s_environ = cb;
}
RETRO_API void retro_set_video_refresh(retro_video_refresh_t cb)
{
    s_video = cb;
}
RETRO_API void retro_set_audio_sample(retro_audio_sample_t cb)
{
    s_audio_sample = cb;
}
RETRO_API void retro_set_audio_sample_batch(retro_audio_sample_batch_t cb)
{
    s_audio_batch = cb;
}
RETRO_API void retro_set_input_poll(retro_input_poll_t cb)
{
    s_input_poll = cb;
}
RETRO_API void retro_set_input_state(retro_input_state_t cb)
{
    s_input_state = cb;
}

/* ---- lifecycle ------------------------------------------------------------- */
RETRO_API void retro_init(void)
{
    s_frame_counter = 0;
    s_audio_phase = 0;
    s_game_loaded = false;
    memset(s_framebuffer, 0, sizeof(s_framebuffer));
    memset(s_sysram, 0, sizeof(s_sysram));
}

RETRO_API void retro_deinit(void)
{
    s_frame_counter = 0;
    s_audio_phase = 0;
    s_game_loaded = false;
}

RETRO_API unsigned retro_api_version(void)
{
    return RETRO_API_VERSION;
}

RETRO_API void retro_get_system_info(struct retro_system_info *info)
{
    if (info == NULL) {
        return;
    }
    memset(info, 0, sizeof(*info));
    info->library_name = "StubCore";
    info->library_version = "0.1.0";
    info->valid_extensions = "stub|bin";
    info->need_fullpath = true; /* per brief */
    info->block_extract = false;
}

RETRO_API void retro_get_system_av_info(struct retro_system_av_info *info)
{
    if (info == NULL) {
        return;
    }
    memset(info, 0, sizeof(*info));
    info->geometry.base_width = STUB_WIDTH;
    info->geometry.base_height = STUB_HEIGHT;
    info->geometry.max_width = STUB_WIDTH;
    info->geometry.max_height = STUB_HEIGHT;
    info->geometry.aspect_ratio = (float)STUB_WIDTH / (float)STUB_HEIGHT;
    info->timing.fps = STUB_FPS;
    info->timing.sample_rate = STUB_SAMPLE_RATE;
}

RETRO_API void retro_set_controller_port_device(unsigned port, unsigned device)
{
    (void)port;
    (void)device;
}

RETRO_API void retro_reset(void)
{
    s_frame_counter = 0;
    s_audio_phase = 0;
}

/* ---- game load ------------------------------------------------------------- */
RETRO_API bool retro_load_game(const struct retro_game_info *game)
{
    (void)game; /* content ignored — accept any path */

    /* Request RGB565. If the frontend refuses, we still run (it defaults to a
     * format we can live with), but log via nothing here — the host logs. */
    if (s_environ != NULL) {
        enum retro_pixel_format fmt = RETRO_PIXEL_FORMAT_RGB565;
        (void)s_environ(RETRO_ENVIRONMENT_SET_PIXEL_FORMAT, &fmt);
    }
    s_frame_counter = 0;
    s_audio_phase = 0;
    s_game_loaded = true;
    return true;
}

RETRO_API bool retro_load_game_special(unsigned type, const struct retro_game_info *info,
                                       size_t num)
{
    (void)type;
    (void)info;
    (void)num;
    return false;
}

RETRO_API void retro_unload_game(void)
{
    s_game_loaded = false;
}

RETRO_API unsigned retro_get_region(void)
{
    return RETRO_REGION_NTSC;
}

/* ---- video helpers --------------------------------------------------------- */
static void stub_draw_pattern(bool invert)
{
    /* Moving diagonal gradient keyed off the counter. */
    for (uint32_t y = 0; y < STUB_HEIGHT; ++y) {
        for (uint32_t x = 0; x < STUB_WIDTH; ++x) {
            uint16_t v = (uint16_t)((x + y + s_frame_counter) & 0xFFFFu);
            if (invert) {
                v = (uint16_t)~v;
            }
            s_framebuffer[y * STUB_WIDTH + x] = v;
        }
    }
}

static void stub_draw_counter(void)
{
    /* Row 0, first 64 pixels: 32 bits, LSB first, 2 pixels per bit.
     * Drawn last so pattern inversion cannot corrupt the counter band. */
    for (uint32_t bit = 0; bit < STUB_COUNTER_BITS; ++bit) {
        uint16_t on = ((s_frame_counter >> bit) & 1u) ? 0xFFFFu : 0x0000u;
        uint32_t base = bit * STUB_PIXELS_PER_BIT;
        for (uint32_t p = 0; p < STUB_PIXELS_PER_BIT; ++p) {
            s_framebuffer[base + p] = on;
        }
    }
}

static void stub_emit_audio(void)
{
    /* 440 Hz square wave, amplitude 8000, 800 stereo frames.
     * period_samples = sample_rate / freq = 48000 / 440 ~= 109.09; use the
     * running phase so the wave is continuous across runs. */
    const double period = STUB_SAMPLE_RATE / STUB_SQUARE_HZ;
    const uint32_t half = (uint32_t)(period / 2.0 + 0.5);
    for (uint32_t i = 0; i < STUB_AUDIO_FRAMES_PER_RUN; ++i) {
        uint32_t phase = s_audio_phase + i;
        uint32_t pos = (half == 0) ? 0 : (phase % (half * 2u));
        int16_t s = (pos < half) ? (int16_t)STUB_AMPLITUDE : (int16_t)(-STUB_AMPLITUDE);
        s_audio[i * 2u + 0u] = s;
        s_audio[i * 2u + 1u] = s;
    }
    s_audio_phase += STUB_AUDIO_FRAMES_PER_RUN;
}

/* ---- run ------------------------------------------------------------------- */
RETRO_API void retro_run(void)
{
    if (s_input_poll != NULL) {
        s_input_poll();
    }

    /* Read joypad button B (id 0) on port 0. */
    bool b_held = false;
    if (s_input_state != NULL) {
        int16_t v = s_input_state(0, RETRO_DEVICE_JOYPAD, 0, RETRO_DEVICE_ID_JOYPAD_B);
        b_held = (v != 0);
    }

    /* Increment first so run N produces counter N. */
    s_frame_counter += 1u;

    stub_draw_pattern(b_held);
    stub_draw_counter();

    if (s_video != NULL) {
        s_video(s_framebuffer, STUB_WIDTH, STUB_HEIGHT,
                (size_t)STUB_WIDTH * sizeof(uint16_t));
    }

    stub_emit_audio();
    if (s_audio_batch != NULL) {
        (void)s_audio_batch(s_audio, STUB_AUDIO_FRAMES_PER_RUN);
    } else if (s_audio_sample != NULL) {
        for (uint32_t i = 0; i < STUB_AUDIO_FRAMES_PER_RUN; ++i) {
            s_audio_sample(s_audio[i * 2u + 0u], s_audio[i * 2u + 1u]);
        }
    }
}

/* ---- savestates ------------------------------------------------------------ */
#define STUB_STATE_SIZE 16u

RETRO_API size_t retro_serialize_size(void)
{
    return STUB_STATE_SIZE;
}

RETRO_API bool retro_serialize(void *data, size_t size)
{
    if (data == NULL || size < STUB_STATE_SIZE) {
        return false;
    }
    uint8_t *p = (uint8_t *)data;
    memcpy(p, "STUB", 4);
    /* u32 counter, little-endian */
    p[4] = (uint8_t)(s_frame_counter & 0xFFu);
    p[5] = (uint8_t)((s_frame_counter >> 8) & 0xFFu);
    p[6] = (uint8_t)((s_frame_counter >> 16) & 0xFFu);
    p[7] = (uint8_t)((s_frame_counter >> 24) & 0xFFu);
    memset(p + 8, 0, 8); /* 8 zero bytes */
    return true;
}

RETRO_API bool retro_unserialize(const void *data, size_t size)
{
    if (data == NULL || size < STUB_STATE_SIZE) {
        return false;
    }
    const uint8_t *p = (const uint8_t *)data;
    if (memcmp(p, "STUB", 4) != 0) {
        return false;
    }
    s_frame_counter = (uint32_t)p[4] | ((uint32_t)p[5] << 8) | ((uint32_t)p[6] << 16) |
                      ((uint32_t)p[7] << 24);
    return true;
}

/* ---- memory ---------------------------------------------------------------- */
RETRO_API void *retro_get_memory_data(unsigned id)
{
    if (id == RETRO_MEMORY_SYSTEM_RAM) {
        return s_sysram;
    }
    return NULL;
}

RETRO_API size_t retro_get_memory_size(unsigned id)
{
    if (id == RETRO_MEMORY_SYSTEM_RAM) {
        return STUB_SYSRAM_BYTES;
    }
    return 0;
}

/* ---- cheats (unused stubs required by the API) ----------------------------- */
RETRO_API void retro_cheat_reset(void) {}
RETRO_API void retro_cheat_set(unsigned index, bool enabled, const char *code)
{
    (void)index;
    (void)enabled;
    (void)code;
}
