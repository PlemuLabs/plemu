/*
 * environment.c — the retro_environment_t trampoline.
 *
 * One responsibility: service the environment commands the software path needs
 * and, per the libretro contract, return FALSE for everything else. Unknown
 * command numbers are logged once each at DEBUG level.
 *
 * Implemented commands:
 *   SET_PIXEL_FORMAT      accept RGB565 + XRGB8888 (reject 0RGB1555)
 *   GET_SYSTEM_DIRECTORY  return create-desc system_dir
 *   GET_SAVE_DIRECTORY    return create-desc save_dir
 *   GET_LOG_INTERFACE     hand back our retro_log_printf_t
 *   GET_CAN_DUPE          true
 *   SET_VARIABLES         copy the core's option keys/defaults into our map
 *   GET_VARIABLE          look up a key in our map
 *   GET_VARIABLE_UPDATE   report + clear the dirty flag
 *   GET_INPUT_BITMASKS    true (and remember it for input.c)
 */
#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "internal.h"

/* our single retro_log_printf_t shim; forwards to the core's rh_log_fn */
static void RETRO_CALLCONV rh_retro_log_printf(enum retro_log_level level, const char *fmt, ...)
{
    rh_core *c = g_active;
    if (c == NULL || fmt == NULL) {
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
    if (c->log != NULL) {
        c->log((int)level, msg);
    }
}

static void rh_log_unknown_once(rh_core *c, unsigned cmd)
{
    /* Small dedup set so a noisy core doesn't spam the log every frame. */
    for (int i = 0; i < c->logged_unknown_count; ++i) {
        if (c->logged_unknown[i] == (uint32_t)cmd) {
            return;
        }
    }
    const int cap = (int)(sizeof(c->logged_unknown) / sizeof(c->logged_unknown[0]));
    if (c->logged_unknown_count < cap) {
        c->logged_unknown[c->logged_unknown_count++] = (uint32_t)cmd;
    }
    rh_log(c, RETRO_LOG_DEBUG, "environment: unhandled command %u (returning false)", cmd);
}

static const char *rh_var_lookup(rh_core *c, const char *key)
{
    if (key == NULL) {
        return NULL;
    }
    for (int i = 0; i < c->var_count; ++i) {
        if (c->vars[i].key != NULL && strcmp(c->vars[i].key, key) == 0) {
            return c->vars[i].value;
        }
    }
    return NULL;
}

/* SET_VARIABLES defines options as "label; val0|val1|val2"; the effective
 * default is the FIRST listed value. We store key -> first-value. */
static void rh_set_variables(rh_core *c, const struct retro_variable *vars)
{
    /* reset current map */
    for (int i = 0; i < c->var_count; ++i) {
        free(c->vars[i].key);
        free(c->vars[i].value);
        c->vars[i].key = NULL;
        c->vars[i].value = NULL;
    }
    c->var_count = 0;

    if (vars == NULL) {
        c->var_dirty = true;
        return;
    }

    for (const struct retro_variable *v = vars; v->key != NULL && v->value != NULL; ++v) {
        if (c->var_count >= RH_MAX_VARS) {
            rh_log(c, RETRO_LOG_WARN, "environment: variable map full (%d), ignoring '%s'",
                   RH_MAX_VARS, v->key);
            break;
        }
        /* Extract default value: skip "label; ", then take up to first '|'. */
        const char *desc = v->value;
        const char *semi = strchr(desc, ';');
        const char *vals = semi ? semi + 1 : desc;
        while (*vals == ' ') {
            vals++;
        }
        size_t vlen = 0;
        while (vals[vlen] != '\0' && vals[vlen] != '|') {
            vlen++;
        }
        char *key = strdup(v->key);
        char *val = (char *)malloc(vlen + 1);
        if (key == NULL || val == NULL) {
            free(key);
            free(val);
            rh_log(c, RETRO_LOG_ERROR, "environment: OOM storing variable '%s'", v->key);
            break;
        }
        memcpy(val, vals, vlen);
        val[vlen] = '\0';
        c->vars[c->var_count].key = key;
        c->vars[c->var_count].value = val;
        c->var_count++;
    }
    c->var_dirty = true;
}

bool rh_environment_cb(unsigned cmd, void *data)
{
    rh_core *c = g_active;
    if (c == NULL) {
        return false;
    }

    switch (cmd) {
    case RETRO_ENVIRONMENT_SET_PIXEL_FORMAT: {
        if (data == NULL) {
            return false;
        }
        const enum retro_pixel_format pf = *(const enum retro_pixel_format *)data;
        if (pf == RETRO_PIXEL_FORMAT_RGB565) {
            c->host_format = RH_FORMAT_RGB565;
            return true;
        }
        if (pf == RETRO_PIXEL_FORMAT_XRGB8888) {
            c->host_format = RH_FORMAT_XRGB8888;
            return true;
        }
        rh_log(c, RETRO_LOG_WARN, "environment: unsupported pixel format %d (only RGB565/XRGB8888)",
               (int)pf);
        return false;
    }

    case RETRO_ENVIRONMENT_GET_SYSTEM_DIRECTORY:
        if (data == NULL) {
            return false;
        }
        *(const char **)data = c->system_dir;
        return c->system_dir != NULL;

    case RETRO_ENVIRONMENT_GET_SAVE_DIRECTORY:
        if (data == NULL) {
            return false;
        }
        *(const char **)data = c->save_dir;
        return c->save_dir != NULL;

    case RETRO_ENVIRONMENT_GET_LOG_INTERFACE: {
        if (data == NULL) {
            return false;
        }
        struct retro_log_callback *cb = (struct retro_log_callback *)data;
        cb->log = rh_retro_log_printf;
        return true;
    }

    case RETRO_ENVIRONMENT_GET_CAN_DUPE:
        if (data == NULL) {
            return false;
        }
        *(bool *)data = true;
        return true;

    case RETRO_ENVIRONMENT_SET_VARIABLES:
        rh_set_variables(c, (const struct retro_variable *)data);
        return true;

    case RETRO_ENVIRONMENT_GET_VARIABLE: {
        if (data == NULL) {
            return false;
        }
        struct retro_variable *v = (struct retro_variable *)data;
        v->value = rh_var_lookup(c, v->key);
        return v->value != NULL;
    }

    case RETRO_ENVIRONMENT_GET_VARIABLE_UPDATE:
        if (data == NULL) {
            return false;
        }
        *(bool *)data = c->var_dirty;
        c->var_dirty = false;
        return true;

    case RETRO_ENVIRONMENT_GET_INPUT_BITMASKS:
        /* data may be NULL — the query itself signals support. */
        c->input_bitmasks = true;
        if (data != NULL) {
            *(bool *)data = true;
        }
        return true;

    default:
        rh_log_unknown_once(c, cmd);
        return false;
    }
}
