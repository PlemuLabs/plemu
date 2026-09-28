/*
 * dylib.c — dlopen/dlsym/dlclose wrapper for a libretro core.
 *
 * One responsibility: load the shared object and resolve every required retro_*
 * symbol into rh_core.fn. If ANY required symbol is missing we log the full list
 * of missing names, close the handle, and return RH_ERR_DLOPEN (leaving c->dl
 * NULL) so the caller can fail cleanly.
 */
#include <dlfcn.h>
#include <string.h>

#include "internal.h"

/* Resolve one symbol; on failure append its name to the missing list. */
#define RH_RESOLVE(field, name)                                                                    \
    do {                                                                                           \
        void *sym = dlsym(c->dl, name);                                                            \
        if (sym == NULL) {                                                                         \
            if (missing_len > 0 && missing_len < (int)sizeof(missing) - 1) {                       \
                missing[missing_len++] = ' ';                                                       \
            }                                                                                      \
            for (const char *p = name; *p != '\0' && missing_len < (int)sizeof(missing) - 1;       \
                 ++p) {                                                                            \
                missing[missing_len++] = *p;                                                        \
            }                                                                                      \
            missing[missing_len] = '\0';                                                            \
            ok = false;                                                                             \
        } else {                                                                                   \
            *(void **)(&c->fn.field) = sym;                                                         \
        }                                                                                          \
    } while (0)

int rh_dylib_open(rh_core *c, const char *path)
{
    if (c == NULL || path == NULL) {
        return RH_ERR_NULL_HANDLE;
    }

    c->dl = dlopen(path, RTLD_NOW | RTLD_LOCAL);
    if (c->dl == NULL) {
        rh_log(c, RETRO_LOG_ERROR, "dlopen('%s') failed: %s", path, dlerror());
        return RH_ERR_DLOPEN;
    }

    char missing[512];
    int missing_len = 0;
    missing[0] = '\0';
    bool ok = true;

    RH_RESOLVE(set_environment, "retro_set_environment");
    RH_RESOLVE(set_video_refresh, "retro_set_video_refresh");
    RH_RESOLVE(set_audio_sample, "retro_set_audio_sample");
    RH_RESOLVE(set_audio_sample_batch, "retro_set_audio_sample_batch");
    RH_RESOLVE(set_input_poll, "retro_set_input_poll");
    RH_RESOLVE(set_input_state, "retro_set_input_state");
    RH_RESOLVE(init, "retro_init");
    RH_RESOLVE(deinit, "retro_deinit");
    RH_RESOLVE(api_version, "retro_api_version");
    RH_RESOLVE(get_system_info, "retro_get_system_info");
    RH_RESOLVE(get_system_av_info, "retro_get_system_av_info");
    RH_RESOLVE(set_controller_port_device, "retro_set_controller_port_device");
    RH_RESOLVE(reset, "retro_reset");
    RH_RESOLVE(run, "retro_run");
    RH_RESOLVE(serialize_size, "retro_serialize_size");
    RH_RESOLVE(serialize, "retro_serialize");
    RH_RESOLVE(unserialize, "retro_unserialize");
    RH_RESOLVE(load_game, "retro_load_game");
    RH_RESOLVE(unload_game, "retro_unload_game");
    RH_RESOLVE(get_region, "retro_get_region");
    RH_RESOLVE(get_memory_data, "retro_get_memory_data");
    RH_RESOLVE(get_memory_size, "retro_get_memory_size");

    if (!ok) {
        rh_log(c, RETRO_LOG_ERROR, "core '%s' missing required symbols:%s", path, missing);
        dlclose(c->dl);
        c->dl = NULL;
        memset(&c->fn, 0, sizeof(c->fn));
        return RH_ERR_DLOPEN;
    }

    return RH_OK;
}

void rh_dylib_close(rh_core *c)
{
    if (c == NULL || c->dl == NULL) {
        return;
    }
    dlclose(c->dl);
    c->dl = NULL;
    memset(&c->fn, 0, sizeof(c->fn));
}
