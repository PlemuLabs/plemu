# Native shim test fixtures

Dev test fixtures for the libretro host shim's real-core smoke harness
(`smoke/rh_realcore_smoke.c`). **Not** a registry entry — the registry/manifest
entry for any shipping game is created separately (M4). These files exist only to
prove a real libretro core runs a real, license-verified game through the shim on
macOS arm64.

## `libbet.gb`

| Field       | Value |
|-------------|-------|
| Game        | Libbet and the Magic Floor |
| Author      | Damian Yerrick (pinobatch) — a Game Boy port of Martin Korth's "Magic Floor" |
| System      | Game Boy / Game Boy Color (runs on the SameBoy core) |
| Version     | v0.08 |
| License     | **zlib License** (OSI-approved, permissive; permits free redistribution incl. commercial) |
| Source URL  | https://github.com/pinobatch/libbet — release: https://github.com/pinobatch/libbet/releases/tag/v0.08 |
| Asset URL   | https://github.com/pinobatch/libbet/releases/download/v0.08/libbet.gb |
| Pinned rev  | v0.08 = commit `46a765a2c01701bffb8c0b7dd6e4be3a6b193090` |
| Size        | 32768 bytes (32 KiB) |
| SHA-256     | `3607412031c8287cf878299ce96e581e85b852dde703806343b95576fa3ff1a9` |

### License verification

Verified from the authoritative upstream source — the project's own `LICENSE`
file (https://raw.githubusercontent.com/pinobatch/libbet/master/LICENSE) and
`README.md`. Verbatim grant from `LICENSE`:

> The zlib License
>
> Copyright (c) 2018 Damian Yerrick
>
> Permission is granted to anyone to use this software for any purpose, including
> commercial applications, and to alter it and redistribute it freely, subject to
> the following restrictions:
>
> 1. The origin of this software must not be misrepresented; you must not claim
>    that you wrote the original software. ...
> 2. Altered source versions must be plainly marked as such ...
> 3. This notice may not be removed or altered from any source distribution.

`README.md` credits: "Copyright 2002, 2012 Martin Korth / Copyright 2018, 2024
Damian Yerrick ... subject to the terms of the zlib License. See the file
`LICENSE`."

The zlib License explicitly permits redistribution of the compiled ROM (this is
why the fixture may be committed). This file is the **author's own** pre-built
release binary for v0.08 (unmodified); its SHA-256 above pins the exact artifact.

We attribute the work here (per restriction 1) and have not altered it (per
restriction 2). The full license text is preserved upstream in the pinned repo.

### Why the release binary (not a from-source build)

The from-source build at v0.08 requires RGBDS < 1.0 (its makefile passes the old
`rgbasm -h` "halt-without-nop" flag, which RGBDS 1.0.x repurposed to `--help`).
The Homebrew RGBDS is 1.0.x, so a from-source rebuild fails on that flag. The
official v0.08 release asset is the unmodified author build of the same
zlib-licensed source, so it is the cleaner-provenance choice here. Its byte-exact
identity is pinned by the SHA-256 above.
