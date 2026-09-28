/*
 * rh_smoke.c — end-to-end smoke harness for the libretro host shim.
 *
 * THIS IS THE TASK'S TEST. Every assertion is real: it decodes the stub core's
 * actual counter pixels, inspects actual audio samples, and proves the savestate
 * restore actually rewound execution. On success it prints "PASS <n> assertions"
 * and exits 0; any failure prints file:line and exits 1.
 *
 * Usage: rh_smoke <path-to-stub-core-dylib>
 */
#define _DARWIN_C_SOURCE /* expose mkdtemp() under -std=c11 on macOS */

#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h> /* mkdtemp */

#include "libretro_host.h"

/* ---- assertion machinery --------------------------------------------------- */
static int g_assertions;

#define CHECK(cond, ...)                                                                           \
    do {                                                                                           \
        g_assertions++;                                                                            \
        if (!(cond)) {                                                                             \
            fprintf(stderr, "%s:%d: FAIL: ", __FILE__, __LINE__);                                  \
            fprintf(stderr, __VA_ARGS__);                                                          \
            fprintf(stderr, "\n");                                                                 \
            exit(1);                                                                               \
        }                                                                                          \
    } while (0)

/* ---- host constants mirrored from the stub/ABI (for readable assertions) --- */
#define EXPECT_W 320u
#define EXPECT_H 240u
#define COUNTER_BITS 32u
#define PIXELS_PER_BIT 2u
#define STUB_AMPLITUDE 8000

static void log_cb(int level, const char *msg)
{
    /* Route core/host logs to stderr; keep stdout clean for the PASS line. */
    fprintf(stderr, "[rh log %d] %s\n", level, msg);
}

/* Decode the 32-bit counter from the first 64 pixels of row 0 (RGB565).
 * LSB first, 2 pixels per bit, 0xFFFF=1 / 0x0000=0. Requires both pixels of a
 * bit to agree, else the frame is malformed. */
static uint32_t decode_counter(const rh_frame *f)
{
    const uint16_t *px = (const uint16_t *)f->data;
    uint32_t value = 0;
    for (uint32_t bit = 0; bit < COUNTER_BITS; ++bit) {
        uint32_t base = bit * PIXELS_PER_BIT;
        uint16_t a = px[base + 0];
        uint16_t b = px[base + 1];
        CHECK(a == b, "counter bit %u pixels disagree: 0x%04x vs 0x%04x", bit, a, b);
        CHECK(a == 0xFFFFu || a == 0x0000u, "counter bit %u not binary: 0x%04x", bit, a);
        if (a == 0xFFFFu) {
            value |= (1u << bit);
        }
    }
    return value;
}

/* Sample a pattern pixel at (x,y) from the current frame (RGB565). */
static uint16_t pattern_pixel(const rh_frame *f, uint32_t x, uint32_t y)
{
    const uint8_t *row = (const uint8_t *)f->data + (size_t)y * f->pitch;
    const uint16_t *px = (const uint16_t *)row;
    return px[x];
}

int main(int argc, char **argv)
{
    if (argc < 2) {
        fprintf(stderr, "usage: %s <stub-core-dylib>\n", argv[0]);
        return 2;
    }
    const char *core_path = argv[1];

    /* ABI sanity. */
    CHECK(rh_abi_version() == 1, "abi version != 1 (got %d)", rh_abi_version());

    /* NULL-safety spot checks on the public surface (must not crash). */
    CHECK(rh_load_core(NULL, core_path) == RH_ERR_NULL_HANDLE, "load_core(NULL) not NULL-safe");
    rh_destroy(NULL); /* must be a no-op */
    rh_run_frame(NULL);
    rh_unlock_frame(NULL);
    {
        rh_frame nf;
        CHECK(rh_lock_frame(NULL, &nf) == RH_ERR_NULL_HANDLE, "lock_frame(NULL) not NULL-safe");
        rh_av_info ni;
        rh_get_av_info(NULL, &ni); /* no-op, no crash */
        CHECK(rh_audio_fill(NULL) == 0.0f, "audio_fill(NULL) != 0");
        CHECK(rh_serialize_size(NULL) == 0, "serialize_size(NULL) != 0");
    }

    /* ---- 1. create + load core + load game -------------------------------- */
    /* Honor $TMPDIR (set to a sandbox-writable dir in CI); fall back to /tmp. */
    const char *tmp = getenv("TMPDIR");
    if (tmp == NULL || tmp[0] == '\0') {
        tmp = "/tmp";
    }
    char sysdir[1024];
    char savedir[1024];
    (void)snprintf(sysdir, sizeof(sysdir), "%s/rh_smoke_sys_XXXXXX", tmp);
    (void)snprintf(savedir, sizeof(savedir), "%s/rh_smoke_save_XXXXXX", tmp);
    CHECK(mkdtemp(sysdir) != NULL, "mkdtemp sysdir failed (TMPDIR=%s)", tmp);
    CHECK(mkdtemp(savedir) != NULL, "mkdtemp savedir failed (TMPDIR=%s)", tmp);

    rh_create_desc desc;
    memset(&desc, 0, sizeof(desc));
    desc.system_dir = sysdir;
    desc.save_dir = savedir;
    desc.log = log_cb;
    desc.hw_mode = 0;

    rh_handle h = rh_create(&desc);
    CHECK(h != NULL, "rh_create returned NULL");

    CHECK(rh_load_core(h, core_path) == RH_OK, "rh_load_core failed");

    /* One-live-core guard: a second handle loading a core must be RH_ERR_BUSY. */
    rh_handle h2 = rh_create(&desc);
    CHECK(h2 != NULL, "second rh_create returned NULL");
    CHECK(rh_load_core(h2, core_path) == RH_ERR_BUSY, "one-live-core guard not enforced");
    rh_destroy(h2);

    CHECK(rh_load_game(h, "/dev/null") == RH_OK, "rh_load_game(/dev/null) failed");

    /* ---- 2. av info -------------------------------------------------------- */
    rh_av_info av;
    rh_get_av_info(h, &av);
    CHECK(av.fps == 60.0, "fps != 60 (got %f)", av.fps);
    CHECK(av.sample_rate == 48000.0, "sample_rate != 48000 (got %f)", av.sample_rate);
    CHECK(av.base_w == EXPECT_W, "base_w != 320 (got %u)", av.base_w);
    CHECK(av.base_h == EXPECT_H, "base_h != 240 (got %u)", av.base_h);

    /* ---- 3. run 120 frames, decode counter -------------------------------- */
    for (int i = 0; i < 120; ++i) {
        rh_run_frame(h);
    }
    rh_frame frame;
    CHECK(rh_lock_frame(h, &frame) == RH_OK, "rh_lock_frame failed after 120 runs");
    CHECK(frame.format == RH_FORMAT_RGB565, "format != RGB565 (got %u)", frame.format);
    CHECK(frame.width == EXPECT_W, "frame width != 320 (got %u)", frame.width);
    CHECK(frame.height == EXPECT_H, "frame height != 240 (got %u)", frame.height);
    /* pitch sane: tightly packed RGB565 => width*2. */
    CHECK(frame.pitch == EXPECT_W * 2u, "pitch not width*2 (got %u)", frame.pitch);
    CHECK(frame.serial >= 120, "serial too low (got %llu)", (unsigned long long)frame.serial);
    uint32_t counter = decode_counter(&frame);
    CHECK(counter == 120u, "decoded counter != 120 (got %u)", counter);
    /* Remember a known pattern pixel for the inversion test (row 1 avoids the
     * counter band on row 0). */
    uint16_t pat_before = pattern_pixel(&frame, 10u, 1u);
    rh_unlock_frame(h);

    /* ---- 4. audio ---------------------------------------------------------- */
    /* 120 runs * 800 frames = 96000 frames produced; the ring holds 16384 and
     * drops oldest, so it is FULL right now. Check occupancy (0,1] BEFORE
     * draining so no extra retro_run is needed (keeps the counter at 120). The
     * ring being exactly full after a 96000-frame flood is itself the proof that
     * the overrun policy (drop-oldest, bounded capacity) is in effect. */
    float fill = rh_audio_fill(h);
    CHECK(fill > 0.0f && fill <= 1.0f, "audio_fill out of (0,1] (got %f)", fill);
    CHECK(fill == 1.0f, "ring should be full (overrun policy) after 96000 frames (fill=%f)", fill);
    static int16_t audio[16384 * 2];
    int32_t got = rh_read_audio(h, audio, 16384);
    CHECK(got > 0, "rh_read_audio drained 0 frames");
    CHECK(got == 16384, "full ring should drain exactly capacity (got %d, cap 16384)", got);
    int found_pos = 0, found_neg = 0;
    for (int32_t i = 0; i < got; ++i) {
        int16_t l = audio[i * 2 + 0];
        int16_t r = audio[i * 2 + 1];
        CHECK(l == r, "audio not mono-duplicated across L/R at frame %d", i);
        if (l == (int16_t)STUB_AMPLITUDE) {
            found_pos = 1;
        }
        if (l == (int16_t)(-STUB_AMPLITUDE)) {
            found_neg = 1;
        }
    }
    CHECK(found_pos, "no +8000 sample in drained audio");
    CHECK(found_neg, "no -8000 sample in drained audio");

    /* ---- 5. input inversion ------------------------------------------------ */
    /* Hold B (bit 0), run one frame (counter 120 -> 121), confirm the pattern
     * pixel flipped to its bitwise complement while the counter band survives. */
    rh_input_state in;
    memset(&in, 0, sizeof(in));
    in.buttons = (uint16_t)(1u << 0); /* RETRO_DEVICE_ID_JOYPAD_B */
    rh_set_input(h, 0, &in);
    rh_run_frame(h); /* counter now 121; pattern inverted this frame */

    rh_frame inv;
    CHECK(rh_lock_frame(h, &inv) == RH_OK, "lock after inversion failed");
    uint16_t pat_after = pattern_pixel(&inv, 10u, 1u);
    /* The non-inverted pattern value at counter 121 is (x+y+c) & 0xFFFF; with B
     * held the stub emits its bitwise complement. */
    uint16_t expect_plain_121 = (uint16_t)((10u + 1u + 121u) & 0xFFFFu);
    CHECK(pat_after == (uint16_t)~expect_plain_121,
          "pattern not inverted with B held (got 0x%04x, expected complement of 0x%04x)", pat_after,
          expect_plain_121);
    /* Sanity: it also differs from the earlier uninverted sample. */
    CHECK(pat_after != pat_before, "inverted pattern equals pre-inversion pattern");
    /* Counter band survived the inversion. */
    uint32_t counter_after_inv = decode_counter(&inv);
    CHECK(counter_after_inv == 121u, "counter corrupted by inversion (got %u)", counter_after_inv);
    rh_unlock_frame(h);

    /* Release B for the remaining tests. */
    memset(&in, 0, sizeof(in));
    rh_set_input(h, 0, &in);

    /* ---- 6. serialize / run / unserialize / run --------------------------- */
    /* State here: counter == 121. */
    size_t ssize = rh_serialize_size(h);
    CHECK(ssize == 16u, "serialize_size != 16 (got %zu)", ssize);
    uint8_t state[64];
    int32_t sret = rh_serialize(h, state, sizeof(state));
    CHECK(sret == (int32_t)ssize, "rh_serialize returned %d (expected %zu)", sret, ssize);
    CHECK(memcmp(state, "STUB", 4) == 0, "state magic not STUB");

    /* Advance 10 frames -> counter 131 (per brief). Confirm mid-flight. */
    for (int i = 0; i < 10; ++i) {
        rh_run_frame(h);
    }
    rh_frame mid;
    CHECK(rh_lock_frame(h, &mid) == RH_OK, "lock after +10 failed");
    uint32_t counter_mid = decode_counter(&mid);
    CHECK(counter_mid == 131u, "counter after +10 != 131 (got %u)", counter_mid);
    rh_unlock_frame(h);

    /* Restore to counter 121, run one frame -> counter 122. This PROVES the
     * restore rewound execution (without it we'd be at 132). */
    CHECK(rh_unserialize(h, state, ssize) == RH_OK, "rh_unserialize failed");
    rh_run_frame(h);
    rh_frame after;
    CHECK(rh_lock_frame(h, &after) == RH_OK, "lock after restore failed");
    uint32_t counter_restored = decode_counter(&after);
    CHECK(counter_restored == 122u,
          "restore did not rewind: counter %u (expected 122 = restored 121 + 1 run)",
          counter_restored);
    rh_unlock_frame(h);

    /* ---- 7. unload + destroy + clean second cycle ------------------------- */
    rh_unload_game(h);
    rh_destroy(h);

    /* A fresh create/load must work with no stale globals (counter resets). */
    rh_handle h3 = rh_create(&desc);
    CHECK(h3 != NULL, "second-cycle rh_create returned NULL");
    CHECK(rh_load_core(h3, core_path) == RH_OK, "second-cycle rh_load_core failed");
    CHECK(rh_load_game(h3, "/dev/null") == RH_OK, "second-cycle rh_load_game failed");
    rh_run_frame(h3);
    rh_frame fresh;
    CHECK(rh_lock_frame(h3, &fresh) == RH_OK, "second-cycle lock failed");
    uint32_t fresh_counter = decode_counter(&fresh);
    CHECK(fresh_counter == 1u, "second cycle counter != 1 (stale global? got %u)", fresh_counter);
    rh_unlock_frame(h3);
    rh_unload_game(h3);
    rh_destroy(h3);

    printf("PASS %d assertions\n", g_assertions);
    return 0;
}
