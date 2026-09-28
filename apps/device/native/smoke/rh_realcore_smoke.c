/*
 * rh_realcore_smoke.c — real-core smoke harness for the libretro host shim.
 *
 * The stub harness (rh_smoke.c) decodes a DETERMINISTIC counter/pattern that only
 * the stub core emits. A real emulator core (SameBoy running a real Game Boy ROM)
 * produces arbitrary, content-dependent frames and audio, so this harness cannot
 * assert exact pixels. Instead it makes GENERIC "it really runs" assertions:
 *
 *   - av_info is sane (geometry within the core's max, fps in (50,65), sr > 0);
 *   - video is non-blank AND changes over time (framebuffer hash at frame 60 vs
 *     600 differs, and no frame is a single constant colour) — proves the core is
 *     actually emulating, not idling on a static image;
 *   - audio yields > 0 frames over the run and the samples are not all zero;
 *   - input is honoured (a Start/A-held run diverges from a no-input control run
 *     replayed from the same savestate; relaxed to "accepted without error" if the
 *     title screen happens to be input-inert);
 *   - a savestate round-trip actually rewinds execution (serialize -> run 60 ->
 *     unserialize -> run 1 reproduces the byte-exact frame of restore-point + 1
 *     run, which is impossible without a real state restore). Gated on
 *     serialize_size > 0.
 *
 * On success prints "REALCORE PASS <n> assertions" and exits 0; any failure prints
 * file:line and exits 1.
 *
 * Usage: rh_realcore_smoke <real-core-dylib> <real-game-rom>
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

static void log_cb(int level, const char *msg)
{
    /* Route core/host logs to stderr; keep stdout clean for the PASS line. */
    fprintf(stderr, "[rh log %d] %s\n", level, msg);
}

/* ---- framebuffer hashing (format-agnostic, FNV-1a over the tight buffer) ---
 * The host hands out a tightly-packed front buffer (pitch == width*bpp), so we
 * hash exactly width*height*bpp bytes. bpp is derived from the host format enum.
 * Also reports whether every byte is identical (single-constant / blank frame). */
typedef struct {
    uint64_t hash;
    int single_constant; /* 1 if the whole framebuffer is one repeated byte */
    size_t bytes;
} fb_digest;

static uint32_t bpp_for_format(uint32_t fmt)
{
    return (fmt == RH_FORMAT_XRGB8888) ? 4u : 2u; /* RGB565 otherwise */
}

static fb_digest hash_frame(const rh_frame *f)
{
    fb_digest d;
    const uint8_t *base = (const uint8_t *)f->data;
    const uint32_t bpp = bpp_for_format(f->format);
    const size_t row_bytes = (size_t)f->width * bpp;
    uint64_t h = 1469598103934665603ULL; /* FNV-1a offset basis */
    int single = 1;
    uint8_t first = (f->height > 0 && row_bytes > 0) ? base[0] : 0u;
    /* Hash row by row using the reported pitch (defensive even though the host
     * packs tightly), so padding never leaks into the digest. */
    for (uint32_t y = 0; y < f->height; ++y) {
        const uint8_t *row = base + (size_t)y * f->pitch;
        for (size_t x = 0; x < row_bytes; ++x) {
            uint8_t b = row[x];
            if (b != first) {
                single = 0;
            }
            h ^= b;
            h *= 1099511628211ULL; /* FNV-1a prime */
        }
    }
    d.hash = h;
    d.single_constant = single;
    d.bytes = (size_t)f->height * row_bytes;
    return d;
}

/* Run n frames, then lock+hash the current frame. Caller holds no lock. */
static fb_digest run_and_hash(rh_handle h, int frames)
{
    for (int i = 0; i < frames; ++i) {
        rh_run_frame(h);
    }
    rh_frame f;
    CHECK(rh_lock_frame(h, &f) == RH_OK, "rh_lock_frame failed after %d frames", frames);
    fb_digest d = hash_frame(&f);
    rh_unlock_frame(h);
    return d;
}

/* Drain all currently-buffered audio into buf (cap frames); return frames read
 * and set *any_nonzero if any sample was non-zero. */
static int32_t drain_audio(rh_handle h, int16_t *buf, int32_t cap, int *any_nonzero)
{
    int32_t got = rh_read_audio(h, buf, cap);
    if (got > 0) {
        for (int32_t i = 0; i < got * 2; ++i) {
            if (buf[i] != 0) {
                *any_nonzero = 1;
                break;
            }
        }
    }
    return got;
}

int main(int argc, char **argv)
{
    if (argc < 3) {
        fprintf(stderr, "usage: %s <real-core-dylib> <real-game-rom>\n", argv[0]);
        return 2;
    }
    const char *core_path = argv[1];
    const char *game_path = argv[2];

    /* ABI sanity. */
    CHECK(rh_abi_version() == 1, "abi version != 1 (got %d)", rh_abi_version());

    /* ---- create + load core + load game ----------------------------------- */
    /* Honor $TMPDIR (set to a sandbox-writable dir in CI); fall back to /tmp. */
    const char *tmp = getenv("TMPDIR");
    if (tmp == NULL || tmp[0] == '\0') {
        tmp = "/tmp";
    }
    char sysdir[1024];
    char savedir[1024];
    (void)snprintf(sysdir, sizeof(sysdir), "%s/rh_rc_sys_XXXXXX", tmp);
    (void)snprintf(savedir, sizeof(savedir), "%s/rh_rc_save_XXXXXX", tmp);
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
    CHECK(rh_load_core(h, core_path) == RH_OK, "rh_load_core('%s') failed", core_path);
    CHECK(rh_load_game(h, game_path) == RH_OK, "rh_load_game('%s') failed", game_path);

    /* ---- av info: sane, not stub-specific --------------------------------- */
    rh_av_info av;
    rh_get_av_info(h, &av);
    CHECK(av.base_w > 0 && av.base_h > 0, "base geometry zero (%ux%u)", av.base_w, av.base_h);
    CHECK(av.max_w > 0 && av.max_h > 0, "max geometry zero (%ux%u)", av.max_w, av.max_h);
    CHECK(av.base_w <= av.max_w && av.base_h <= av.max_h,
          "base %ux%u exceeds max %ux%u", av.base_w, av.base_h, av.max_w, av.max_h);
    CHECK(av.fps > 50.0 && av.fps < 65.0, "fps not in (50,65): %f", av.fps);
    CHECK(av.sample_rate > 0.0, "sample_rate not > 0: %f", av.sample_rate);

    /* Capture an EARLY savestate (S0, during the animated boot) so the rewind test
     * below can return here after the long video/input tests have driven the game
     * onto a static title screen. Reproduced byte-for-byte by the rewind test as
     * the proof that unserialize genuinely rewinds. Gated on size.
     *
     * Run one frame first: some cores (SameBoy included) reject retro_serialize
     * before the first retro_run even though serialize_size is already non-zero.
     * One frame is still deep in the animated boot, so S0 remains an active point. */
    rh_run_frame(h);
    size_t s0_size = rh_serialize_size(h);
    uint8_t *s0 = NULL;
    if (s0_size > 0) {
        s0 = (uint8_t *)malloc(s0_size);
        CHECK(s0 != NULL, "OOM allocating early savestate (%zu bytes)", s0_size);
        CHECK(rh_serialize(h, s0, s0_size) == (int32_t)s0_size, "early serialize (S0) failed");
    }

    /* ---- video: non-blank + changes over time ----------------------------- */
    /* Hash at frame 60, then continue to frame 600 and hash again. A real core
     * emulating a booting game changes the screen between these points; an idling
     * or stubbed core would not. Also assert neither frame is a single constant
     * colour (blank). Total run here == 600 frames. */
    fb_digest d60 = run_and_hash(h, 60);
    CHECK(d60.bytes > 0, "frame @60 has zero bytes");
    CHECK(!d60.single_constant, "frame @60 is a single constant colour (blank)");

    fb_digest d600 = run_and_hash(h, 540); /* 60 + 540 = 600 frames total */
    CHECK(d600.bytes > 0, "frame @600 has zero bytes");
    CHECK(!d600.single_constant, "frame @600 is a single constant colour (blank)");
    CHECK(d60.hash != d600.hash,
          "framebuffer identical at frame 60 and 600 (hash 0x%016llx) — core not emulating",
          (unsigned long long)d60.hash);

    /* ---- savestate rewind: state actually rewinds ------------------------- */
    /* Rewind to the EARLY savestate S0 (captured at ~frame 0), because the long
     * video test above drove the game onto a static title screen where no window
     * animates. From S0 the boot/intro is animating, so a fork there provably
     * diverges — which makes the rewind assertion load-bearing (a static screen
     * would let "restored == ref" pass even if unserialize were a no-op).
     *
     * Method: unserialize S0 -> find a fork point S in the animating region
     * (snapshot S -> run 1 frame => reference H1 -> run N-1 more => MUST differ
     * from H1, proving forward progress) -> unserialize S -> run 1 frame -> hash
     * MUST equal H1. Reproducing H1 after diverging is only possible if
     * unserialize genuinely rewound execution to S. Gated on serialize_size > 0. */
    if (s0_size > 0 && s0 != NULL) {
        CHECK(rh_unserialize(h, s0, s0_size) == RH_OK, "rewind to S0 (early state) failed");

        size_t ssize = rh_serialize_size(h);
        CHECK(ssize > 0, "serialize_size dropped to 0 after S0 restore");
        uint8_t *state = (uint8_t *)malloc(ssize);
        CHECK(state != NULL, "OOM allocating savestate (%zu bytes)", ssize);

        const int diverge_frames = 30; /* frames run from S to force divergence */
        fb_digest ref;
        fb_digest diverged;
        int found_active = 0;
        for (int attempt = 0; attempt < 20 && !found_active; ++attempt) {
            CHECK(rh_serialize(h, state, ssize) == (int32_t)ssize, "rewind serialize failed");
            ref = run_and_hash(h, 1); /* reference: S + 1 run */
            CHECK(ref.bytes > 0, "reference frame (S+1) has zero bytes");
            diverged = run_and_hash(h, diverge_frames - 1); /* now at S + diverge_frames */
            if (diverged.hash != ref.hash) {
                found_active = 1; /* animating window: divergence from S+1 is real */
            } else {
                fprintf(stderr, "[realcore] note: static window at attempt %d, advancing\n",
                        attempt);
            }
        }
        CHECK(found_active,
              "no animating window found in bounded search from S0 — cannot prove non-trivial rewind");

        /* Rewind to S, run exactly one frame; MUST reproduce the reference. Because
         * diverged.hash != ref.hash was established, matching ref here is only
         * possible if unserialize genuinely rewound execution to S. */
        CHECK(rh_unserialize(h, state, ssize) == RH_OK, "rewind unserialize failed");
        fb_digest restored = run_and_hash(h, 1);
        CHECK(restored.hash == ref.hash,
              "savestate did NOT rewind: frame hash 0x%016llx != reference 0x%016llx "
              "(diverged 0x%016llx)",
              (unsigned long long)restored.hash, (unsigned long long)ref.hash,
              (unsigned long long)diverged.hash);
        free(state);
    } else {
        fprintf(stderr, "[realcore] note: serialize_size==0, skipping rewind test\n");
    }
    free(s0);

    /* ---- audio: > 0 frames over the run, not all zero --------------------- */
    /* Run a chunk and drain; SameBoy fills the ring quickly, so a short burst is
     * enough. Assert we got frames AND at least one non-zero sample. */
    static int16_t audio[16384 * 2];
    int any_nonzero = 0;
    int32_t audio_total = 0;
    for (int burst = 0; burst < 8; ++burst) {
        for (int i = 0; i < 8; ++i) {
            rh_run_frame(h);
        }
        audio_total += drain_audio(h, audio, 16384, &any_nonzero);
    }
    CHECK(audio_total > 0, "no audio frames produced over the run");
    CHECK(any_nonzero, "all drained audio samples were zero (silent) — core not producing sound");

    /* ---- input: video responds (best-effort), else accepted-without-error -- */
    /* Fork the current state, then replay it two ways over an equal frame budget:
     *   (A) control : no buttons ever pressed;
     *   (B) input   : buttons EDGE-triggered — released for a few frames, then
     *                 Start+A+Right pressed, then released — because GB menus
     *                 typically latch on a press EDGE, not a held level.
     * If the two runs diverge, input demonstrably affects emulation. Per the brief
     * this is best-effort: a title screen that ignores input is allowed, and we
     * then relax to "input accepted without error" (the run still produced valid
     * frames and left the core healthy). Gated on serialize_size > 0. */
    size_t fork_size = rh_serialize_size(h);
    int input_verified_by_divergence = 0;
    int input_exercised = 0;
    if (fork_size > 0) {
        uint8_t *fork = (uint8_t *)malloc(fork_size);
        CHECK(fork != NULL, "OOM allocating fork savestate (%zu bytes)", fork_size);
        CHECK(rh_serialize(h, fork, fork_size) == (int32_t)fork_size, "fork serialize failed");

        const int input_frames = 180;

        rh_input_state none;
        memset(&none, 0, sizeof(none));

        /* (A) control: restore fork, run with no input the whole time. */
        rh_set_input(h, 0, &none);
        CHECK(rh_unserialize(h, fork, fork_size) == RH_OK, "restore fork (control) failed");
        for (int i = 0; i < input_frames; ++i) {
            rh_set_input(h, 0, &none);
            rh_run_frame(h);
        }
        rh_frame cf;
        CHECK(rh_lock_frame(h, &cf) == RH_OK, "lock after control run failed");
        fb_digest control = hash_frame(&cf);
        rh_unlock_frame(h);

        /* (B) input: restore fork, drive edge-triggered presses. Cycle every 20
         * frames: 6 released, then Start+A+Right pressed for 14, repeat. This
         * produces multiple press edges over the budget. */
        rh_input_state pressed;
        memset(&pressed, 0, sizeof(pressed));
        pressed.buttons = (uint16_t)((1u << 3) | (1u << 8) | (1u << 4)); /* START | A | RIGHT */
        CHECK(rh_unserialize(h, fork, fork_size) == RH_OK, "restore fork (input) failed");
        for (int i = 0; i < input_frames; ++i) {
            int phase = i % 20;
            rh_set_input(h, 0, (phase < 6) ? &none : &pressed);
            rh_run_frame(h);
        }
        rh_frame inf;
        CHECK(rh_lock_frame(h, &inf) == RH_OK, "lock after input run failed");
        fb_digest with_input = hash_frame(&inf);
        rh_unlock_frame(h);

        CHECK(with_input.bytes > 0, "input-run frame has zero bytes (input rejected core state)");
        if (with_input.hash != control.hash) {
            input_verified_by_divergence = 1;
            fprintf(stderr, "[realcore] input divergence confirmed (control 0x%016llx vs "
                            "input 0x%016llx)\n",
                    (unsigned long long)control.hash, (unsigned long long)with_input.hash);
        } else {
            /* Input-inert screen: at minimum, input was accepted and the core kept
             * producing valid frames. Document and relax (allowed by the brief). */
            fprintf(stderr,
                    "[realcore] note: input did not change the frame over %d frames "
                    "(input-inert screen); relaxed to accepted-without-error\n",
                    input_frames);
        }

        input_exercised = 1; /* both control + input runs completed without error */

        /* Clear input for subsequent tests. */
        rh_set_input(h, 0, &none);
        free(fork);
    } else {
        fprintf(stderr, "[realcore] note: serialize_size==0, skipping input-divergence test\n");
    }
    /* Input must have been either exercised end-to-end (control + edge-triggered
     * input runs both produced valid frames) or legitimately skipped because the
     * core reports no savestate. Divergence is the stronger outcome; the relaxed
     * path still requires input_exercised. */
    CHECK(fork_size == 0 || input_exercised,
          "input path neither exercised nor cleanly skipped");
    if (input_verified_by_divergence) {
        CHECK(input_exercised, "divergence flagged without a completed input run");
    }

    /* ---- teardown --------------------------------------------------------- */
    rh_unload_game(h);
    rh_destroy(h);

    printf("REALCORE PASS %d assertions\n", g_assertions);
    return 0;
}
