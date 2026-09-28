# Licensing Matrix

Each row maps a `license.type` enum value to the evidence required to accept a submission. Where
evidence is absent or ambiguous, do not accept.

---

## Evidence Requirements by License Type

| `license.type` | Minimum Acceptable Evidence |
|---|---|
| `PD` | Pre-1927 publication date with verifiable source, OR explicit public-domain dedication in the original source/packaging, archived. Scene "PD ROMs" do NOT qualify — see traps below. |
| `CC0` | LICENSE file in the source repository stating CC0 1.0, OR the itch.io / distributing-site page explicitly stating "CC0" or "No Rights Reserved", with an archived copy of that page committed to `license-evidence/`. |
| `CC-BY` | LICENSE file or distribution page stating CC-BY (any version), archived. Confirm the version (e.g. CC-BY 4.0). |
| `CC-BY-SA` | Same as CC-BY plus confirmation that the ShareAlike term is acceptable for hub redistribution. |
| `MIT` | LICENSE file in source repository containing the MIT/Expat text, archived. |
| `GPL` | LICENSE file in source repository (GPL-2.0-only, GPL-2.0-or-later, GPL-3.0-only, or GPL-3.0-or-later), archived. Source availability must be confirmed (link to repo or source archive). |
| `custom-attested` | Author's written grant, e.g. a forum post, email, or signed statement, explicitly granting Plemu the right to distribute the work. The grant must be committed verbatim to `license-evidence/`. **Requires two-person curator approval.** |

---

## Known Traps and Warnings

> **"Free download" ≠ redistribution right.** A game being available for free download does not
> mean you may redistribute it. "Freeware" typically means the author charges nothing, but all
> copyright is retained. Without an explicit redistribution grant, do not accept.

> **PICO-8 BBS default is CC-BY-NC-SA — NOT acceptable.** The NonCommercial (NC) clause conflicts
> with Plemu's tip and NFT creator economy, which involves potential commercial flows. An
> NC-licensed work cannot legally be included. The only path for PICO-8 games is an explicit
> author-submitted grant (using `custom-attested`) that waives the NC restriction — the BBS page
> alone is not sufficient.

> **AtariAge titles are often sold commercially.** Many Atari 2600 homebrew games listed on
> AtariAge are for sale as physical cartridges. The author retains full commercial copyright.
> Confirm with the author via a `custom-attested` grant if you believe the game is freely licensed;
> do not assume from the AtariAge listing alone.

> **Exclude Net Yaroze titles.** Net Yaroze was a Sony developer program; titles created under it
> may carry Sony IP restrictions or ambiguous licence terms inherited from the program. Do not
> accept Net Yaroze ROMs or executables without verified written clearance from both the creator
> and confirmed absence of Sony encumbrance. In practice this means: do not accept unless counsel
> has reviewed.

> **Scene "PD ROMs" usually means freeware, not public domain.** The demoscene and ROM-site usage
> of "Public Domain" almost always means the author released it for free, not that they waived
> copyright. Treat any "PD ROM" submission as `custom-attested` until the author explicitly
> confirms a public-domain dedication or a CC0 waiver.

---

## Archiving Evidence

All evidence URLs must be archived:

1. Submit the URL to [archive.org/save](https://archive.org/save) (or use `wget --mirror` for
   offline snapshots).
2. Commit the archive URL (or the snapshot file if the site may disappear) to `license-evidence/`.
3. Record the archive URL in a PR comment so curators can verify.

Evidence URLs that return 404 or are behind a login wall are not acceptable. Request a re-submission
with a stable or archived URL.
