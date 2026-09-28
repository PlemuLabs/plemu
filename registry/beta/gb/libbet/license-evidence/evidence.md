# License evidence — libbet (Libbet and the Magic Floor)

**License:** zlib (recorded in the manifest as `custom-attested`, see note below)

## Statement

*Libbet and the Magic Floor* is a homebrew Game Boy game by Damian Yerrick
(pinobatch). It is distributed by its author under the
[zlib License](https://github.com/pinobatch/libbet/blob/master/README.md), an
OSI-approved permissive license that grants the right to use, copy, modify, and
redistribute the work (including in binary form) provided the origin is not
misrepresented and altered versions are marked as such.

The redistributed artifact in this entry is the unmodified `libbet.gb` ROM image:

- sha256: `3607412031c8287cf878299ce96e581e85b852dde703806343b95576fa3ff1a9`
- size: 32768 bytes

## Why `custom-attested`

The Plemu SPEC §1.3 license enum (`PD | CC0 | CC-BY | CC-BY-SA |
MIT | GPL | custom-attested`) has no `zlib` value. Per the SPEC, a work under a
permissive license outside the closed enum is recorded as `custom-attested` with
an `evidenceUrl` pointing at the upstream license, and a **curator-role**
attestation: a Plemu curator states they verified the work is distributed
under the stated license per the linked evidence. This is that curator
attestation.

## Provenance

- Upstream author: Damian Yerrick (pinobatch)
- Upstream project: <https://github.com/pinobatch/libbet>
- License text: <https://github.com/pinobatch/libbet/blob/master/README.md>
- Curated by: Plemu (dev curator key `dev-secrets/creator-dev.pem`)
- Used here as the M3/M4 real-core fixture (SameBoy running a real Game Boy ROM).
