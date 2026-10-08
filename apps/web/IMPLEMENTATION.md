# Plemu frontend V0 — implementation record

Scope: `apps/web/**`. The iteration records below describe the development and verification performed before source publication. Their local-only Git status statements apply to those historical passes.

## Source publication review — 2026-10-08

- [x] Inspect current branch, status, tracked diff and all new frontend file paths before making changes.
- [x] Confirm the change set is confined to `apps/web/**`; preserve all prior frontend work and historical review records.
- [x] Scan frontend text for credential patterns, private keys, embedded tokens, credential URLs, high-entropy string literals and personal local paths; no findings. Inspect candidate file names and sizes; no symlinks or unexpected executables.
- [x] Verify environment files, dependencies, Next output, TypeScript cache, screenshots and source-art intermediates are ignored. Extend ignores for build exports, coverage, deployment cache and logs.
- [x] Update the README to describe the current Create paths, cards, Pixel Mirror, Guide and mobile implementation.
- [x] Production build (27 pages), TypeScript, all 15 tests and formatting pass. Git whitespace check passes.

The current feature branch is the sole push target. No merge into main, history rewriting or pull request is part of source publication.

- [x] Read the product brief, repository invariants, existing frontend, and reference sites available to browsing.
- [x] Establish the arcade economy visual system and responsive navigation.
- [x] Build Explore, Market, Rankings, Library, twelve game terminals, wallet UI, and error/empty states.
- [x] Generate twelve original artwork assets and optimize delivery.
- [x] Implement search, genres, market sorting, grid/list, saved collections, chart inspection, and community previews.
- [x] Implement reviewable local demo buy/sell flows with typed simulation failures and persistent balances/history.
- [x] Add cinematic scroll scenes, horizontal gallery, parallax, numeric and chart animation, pointer feedback, and reduced-motion fallbacks.
- [x] Retain Creator Studio and the sandboxed original arcade demo.
- [x] Verify browser search/filter/sort/navigation, collection persistence, ranking categories, chart keyboard/range controls, buy/sell and failure states, and arcade start/pause.
- [x] Inspect desktop (1440), tablet (768), and mobile (390) layouts; no horizontal overflow in inspected market/library/terminal views.
- [x] Complete final production build, type check, formatting check, and simulation tests after the last asset/style changes: all pass; eight tests, nineteen generated pages.
- [x] Capture final review screenshots and leave the local preview running on Explore.

Final review images are saved locally in the ignored `qa/` directory: `desktop-home.jpg`, `desktop-terminal.jpg`, and `mobile-terminal.jpg`. Creator Studio was also visually inspected. The chart-bar hydration mismatch was corrected; the final terminal and Explore check recorded no new browser errors. Temporary viewport overrides and the extra QA tab were cleaned up. The local development server remains running at http://127.0.0.1:3000.

## Iteration 2 — play, create, collect

Audit: retain Next/React architecture, typed catalog, original WebP artwork, shared dialogs, charts, market filters, trading simulation, saved collections, Creator Studio, and arcade demo. The first pass overemphasized financial metrics, used small navigation, and gave Create/Packs no meaningful product presence.

- [x] Study public React Bits interaction documentation and PSG1 hardware reference.
- [x] Establish warm paper / charcoal / orange / mint / blue design tokens and selective Silkscreen typography.
- [x] Make Discover, Games, Create, Packs, Market, Rankings, Library primary navigation destinations.
- [x] Build original interactive cartridge rail, PSG1 browser, finite pack reveal, and live creator concept workbench.
- [x] Integrate local pack collections without altering the existing trading data model or legacy creator drafts.
- [x] Give game identity and description priority over the economy on game profiles.
- [x] Review rendered routes, responsive behavior, controls, persistence, and browser diagnostics, including keyboard entry from the PSG1 screen.
- [x] Complete final checks and capture review images; leave local app running.

Iteration 2 validation: production build generated 22 pages; TypeScript, formatting, and all eight simulation tests pass. Desktop, tablet, and mobile browser checks are recorded in `DESIGN-REVIEW.md`. Review images: `qa/v2-home.jpg`, `qa/v2-psg1.jpg`, `qa/v2-create.jpg`, and `qa/v2-packs.jpg`. All work remains local and unstaged.

## Iteration 3 — targeted dark-theme and hierarchy refinement

- [x] Preserve current routes, motion, cartridge browsing, PSG1 controls, Create, Packs, and simulated market flows.
- [x] Replace light hero/pack panels and light creator object surfaces with layered charcoal surfaces. Apply #9945FF / #14F195 as restrained action, selection, and price accents.
- [x] Keep larger persistent global navigation, including in the advanced studio; use a scrollable navigation row on small screens.
- [x] Add accessible catalog-backed category buttons with game counts.
- [x] Restore always-visible token, price, and daily change to cartridges, discovery cards, editorials, featured game, profile banner, search, holdings, pack contents, rankings, and both PSG1 modes.
- [x] Inspect desktop, tablet, and mobile routes, test categories/search/PSG1/pack reveal, and confirm no document overflow in inspected layouts.
- [x] Pass production build (22 pages), TypeScript, formatting, eight simulation tests, and whitespace checks. Leave preview running locally; no staging, commits, or remote changes.

Creator concepts do not have issued tokens or prices; no fictional quote is invented for an unpublished user draft. Existing catalog quotes remain explicitly simulated.

## Iteration 4 — Plemu palette only

- [x] Replace interface purple/green with #7D51FE / #7AB800 and related tonal variants, including legacy styles, studio, favicon, and arcade demo.
- [x] Preserve layout, copy, components, animation, and behavior. Conflicting layout/content browser annotations were excluded under the final palette-only instruction.
- [x] Search active frontend source for former Solana accent literals: no matches.
- [x] Production build passes (22 pages); inspect home, games, market, rankings, library, create, packs, and game profile in browser.
- [x] Verify principal contrast pairs: white/purple 4.69:1; dark text/green 6.91:1; green/dark surface 6.91:1. Lighter purple text uses a derived tint for readability.

Local preview remains running. No GitHub changes. Screenshot: `qa/plemu-palette.jpg`.

## Homepage annotation follow-up

- [x] Hero text/removals and contextual price typography applied; header aligned on desktop, responsive navigation retained.
- [x] Pack fan opens with a 650ms torn-seal animation, then navigates to /packs at 700ms. Reduced motion and modified link clicks retain direct navigation; timer cleaned on unmount.
- [x] Market introduction uses welcoming copy and four existing game cards; ranking filters and prices retained.
- [x] Community ending reduced to approximately 349px at the reviewed desktop width.
- [x] PSG1 title, controls, quote, and supporting content fit beside the console at 1224px; Create restored to its prior flat dark surface and divider.
- [x] TypeScript/build pass; browser verifies pack animation state and destination, mobile filters, and no mobile document overflow. Local preview left running.

## Six-color refinement — current request

- [x] Apply #8C76DB / #B6D935 accents with #4516BB hover, #2D1A71 purple support, #424B16 selected green surfaces and #19113E wallet support. Preserve layout and controls.
- [x] Keep body background #121418 and the entire existing hero background (including its faint previous purple gradient) unchanged.
- [x] Keep contextual quote sizes and neutral price colors intact; update UI accents, studio, favicon and arcade demo.
- [x] Verify contrast: dark text/soft purple 4.81:1, white/deep purple 10.07:1, green/dark green 5.76:1, soft purple/main background 5.03:1.
- [x] TypeScript, production build (22 pages), formatting and whitespace checks pass. Browser confirms background colors, carousel selection and navigation. Screenshot: qa/six-color-home.png.
- [x] Superseded by the explicit request for an independent Pixel Magnet implementation: completed in the curated interaction pass below, without private Pro source.

Local preview remains running. No commits, staging, pushes or remote deployment.

## Tactile interactions and homepage rhythm

- [x] Audit home at 1440×1000, 1224px desktop, 768×1024 and 390×844. Reduce accumulated section padding and desktop PSG1 pin duration from 500px to 180px; retain entry, boot and workbench animation with smaller travel.
- [x] Stack hero and pack composition at 701–999px after finding tablet overlap. Keep existing mobile single-column layout and desktop information beside the PSG1. Main backgrounds and palette unchanged.
- [x] Shared TearPack pointer interaction on homepage and /packs: pointer capture, intentional 80–140px rightward threshold, vertical cancellation, partial-drag spring return, jagged matching wrapper fragments, stretched top, visible inner artwork and automatic completion. Plain click does not open. Enter/Space and visible opening buttons remain available.
- [x] Homepage reveals the front pack's three games with prices and links; /packs retains sequential reveals, collection, selection and replay. Focus moves to reveal/status. Timers and tweens clean up on unmount.
- [x] PSG1 mouse/pen depth is bounded to 4° and 3px, with eased return, no overlay and no continuous idle loop. Motion pauses over controls to prevent moving click targets. Disabled for coarse pointer/reduced motion; tear completes immediately under reduced motion.
- [x] Browser: partial drag returns to zero; full drag reveals on home and /packs; narrow-viewport drag works; Enter opens and focuses home reveal; sequential reveal updates; replay works. PSG1 measured 3.49°/3.06° then returned to zero; Next game updates title to Neon Ronin. No browser errors and no horizontal page overflow in inspected sizes.
- [x] TypeScript, formatting, eight existing simulation tests and production build pass. Screenshots: qa/tactile-packs.png, qa/tactile-reveal.png, qa/psg-depth.png.

QA limits: native browser tooling provides pointer drags but not configurable drag speed or physical touch emulation. Touch uses the same pointer handlers with pan-y and pointercancel; real-device finger/stylus testing remains advisable. Reduced-motion behavior reviewed in code; no OS preference was changed. No new dependency, remote push, commit or staging. Preview remains running.

## Collectible card reveal

- [x] Replace cartridges revealed by packs with original Plemu trading cards: foil-style purple/green frame, framed world artwork, title, genre/studio, token quote, flavor text and catalog number. Same card component in homepage pack reveal and /packs.
- [x] Preserve tear gesture, sequential reveal, replay and collection behavior. Revealed cards link to their games; unrevealed cards are inert.
- [x] Browser verifies 1→2→3 reveal, no document overflow at 390px, and card navigation to /game/wildwood. TypeScript and 22-page production build pass. Screenshot: qa/collectible-pack-card.png. Preview running; no push.

## Traditional card stack and hit foil

- [x] Enforce 63:88 aspect ratio for collectible cards across pack surfaces, using an internal grid so artwork and text cannot stretch the card.
- [x] Replace homepage horizontal rail with a three-card stack. Horizontal swipes over 65px send the front card to the back in either direction; partial gestures return. Vertical scrolling, game links, keyboard arrows and previous/next buttons retained; background cards inert.
- [x] Deterministic HIT selection: highest token price within the pack, player count breaks ties, then catalog rank. Visible criterion shown. Hit-only foil frame/art shimmer responds to pointer and remains static for reduced motion. Pixel Rebels is the current homepage pack hit.
- [x] Browser validates exact 280×391.109px card (63:88), right swipe Void→Pixel→Redline, left swipe Redline→Void→Pixel, short-drag cancellation, mobile no-overflow, next button and Enter navigation to /game/void-runner. Screenshot qa/card-stack-hit.png.
- [x] TypeScript and production build validated; preview kept running, no remote changes.

## Smoother card switching and Create Hub

- [x] Replace fading card switches with continuous movement: the front card follows the gesture with resistance, moves aside, and settles behind the stack. Distance and recent velocity determine completion; short gestures spring back. Previous navigation brings the last card to the front.
- [x] Preserve card proportions, hit foil, game links, keyboard navigation, vertical scrolling, pointer cancellation, focus, and reduced-motion handling. Prevent overlapping switches and clean up animation on unmount.
- [x] Introduce /create as a four-path hub: Create a Pack, Submit a Game, Mod a Game, and AI Game Lab. Retain the existing editor at each path, with independent local drafts and the original submit draft key preserved.
- [x] Add Local → Private pack → Public beta → Public main publication guidance and contextual creation links on home and packs. Mod and AI paths clearly describe concept-only functionality; no publishing or generation backend is implied.
- [x] Browser checks: both swipe directions, previous navigation, short-gesture return without accidental navigation, mobile overflow, all four workspaces, editor preview flow, and empty browser error logs. Inspect desktop and 390px Create Hub layouts.
- [x] TypeScript, production build (26 pages), formatting, eight simulation tests, and whitespace checks pass. Screenshots: qa/smooth-card-slide.png and qa/create-hub.png.

Pointer gestures were tested at desktop and narrow viewport widths; physical touch and configurable flick-speed testing are not available through the current browser tooling. All changes remain local and unstaged; preview stays running.

## Targeted layout, navigation, foil and platform refinement

- [x] Desktop header reduced from 92px to 68px through spacing/logo sizing, retaining navigation text sizes. Tablet/mobile retain an intentional horizontally scrollable navigation row with 44px link targets. Exact order: Discover, Market, Create, Packs, Games, Ranking, Library.
- [x] Typed sample platform field added to all 12 fictional catalog games. Reused by standard/visual cards, collectible cards, cartridges, homepage editorials, PSG1, game profile, ranking podium/rows and pack contents. These are mock platform assignments, not tested compatibility claims.
- [x] HIT laminate now spans the full card, with stronger iridescent reflection, pointer-position highlights and gentle idle hue movement. Metadata layers above reflection; reduced-motion disables animation/pointer response. HIT price/player ranking rule unchanged.
- [x] Fix hero fan escaping its column using container-relative offsets. Carousel grid cells now let card content determine height, avoiding cropped cartridge bottoms. Pack fan uses content height and reserves transform space so wrappers do not obscure the opening button.
- [x] Card switch travel respects viewport space, including rotation/stack allowance. Completed /packs reveals use the same navigable stack as home; sequential single-card reveal, replay, collection and game links remain available.
- [x] Ranking podium uses content flow instead of colliding absolute labels; price lines wrap. Editorial arrows have reserved caption space. Card titles and metadata can wrap within their columns.
- [x] QA: inspected home at 1440, 1224, 768 and 390px; header collision measurements at 1000px. Reviewed market, ranking, game profile and search/wallet dialogs on mobile; games/library/Create hub/workspace at 768px; advanced studio at 390px. Verified /packs reveal 1→2→3→next card and homepage stack controls. No console errors. Horizontal navigation/filter/editorial rails retain intentional scrolling.
- [x] TypeScript, production build (26 pages), formatting, eight existing simulation tests and whitespace checks pass. Screenshot: qa/full-card-foil.png. Local preview remains running; nothing staged, committed, pushed or deployed.

The visual audit samples representative game profiles and shared layouts rather than claiming every possible data/state combination was exercised. Physical touch and OS reduced-motion emulation were not available; reduced-motion behavior was reviewed in source.

## Supplied pixel-face logo

- [x] Reconstruct supplied mark as symmetric crisp geometry in Plemu lime, preserving eyes, smile and stepped headphone-like outline. Initial ImageGen explorations were rejected for texture; final source is a clean native SVG.
- [x] Export transparent 1120×1120 RGBA PNG at public/brand/plemu-logo.png. Update shared header/footer logo and favicon metadata.
- [x] TypeScript passes; browser verifies header/footer asset loads and compact header layout. Local only, no push.

## Curated interaction pass

- [x] Implement independent cursor-driven Pixel Magnet locally in the hero: 40 square particles, velocity response, damped follow/settle/return, padded content exclusions, no pointer interception. Pause when settled/offscreen/hidden; disable for reduced motion/coarse pointer/no hover.
- [x] Review Aceternity card, scroll, background, text, CTA and navigation patterns. Add custom spotlight/artwork drift only to the first discovery editorial; refine the existing Magnetic component and apply it to the Create entry CTA. Retain the current backgrounds, palette and navigation.
- [x] Preserve subtle PSG1 depth, neutralize during keyboard focus and fix nested 3D plane hit-testing that blocked pointer clicks. Add selected-game economy link; verify screen, title, token, price and change update together.
- [x] Preserve pack tear, collectible switch, full-card HIT foil and metadata. Verify tear and swipe on desktop, swipe at 390px, spotlight and keyboard focus, magnetic bounds, canvas movement/return, and tablet layout. No horizontal page overflow in inspected states; no browser errors.
- [x] Production build (26 pages), TypeScript, formatting and eight existing simulation tests pass. No dependencies added. Local preview remains running; no stage, commit, push or deployment.
- [x] Document decisions, sources, implementation, performance and QA limits in INTERACTION-REVIEW.md. Screenshots: qa/pixel-magnet-hero.png and qa/interaction-psg-market.png.

Physical touch, configurable pointer-speed sweeps and OS reduced-motion toggles were not exercised by browser automation; corresponding code paths were reviewed.

## Pixel Magnet replacement + Aceternity Webcam Pixel Grid

- [x] Evaluate requested shadcn installations in an isolated temporary project. React Bits starter registry unavailable/unconfigured; official docs require license. Aceternity Webcam Pixel Grid installs successfully; public renderer integrated and adapted, no runtime dependencies added.
- [x] Replace old Pixel Magnet completely with spring/inertia and grid-snapped contracting swarm driven by cursor velocity. Stronger visible square scale, protected content bounds, local hero placement, coarse pointer/reduced-motion disabling, idle/offscreen suspension.
- [x] Add Pixel Mirror at /create#pixel-mirror and home Create entry link. Preserve four creation paths. Monochrome violet/lime, responsive density, static/moving sample labelled camera off, explicit camera opt-in and inline unavailable/denied state.
- [x] Camera lifecycle handles cancellation, late permissions, track release, unmount, leaving section and hidden tab. Original Aceternity automatic permission request removed; local canvas rendering only, no microphone/upload/recording.
- [x] Browser checks sample/motion/color, 1224/768/390px layouts, offscreen unmount, cursor inertia/settle/exit, PSG1 switching and pack keyboard reveal. Twelve tests, production build, TypeScript, formatting and whitespace checks pass.
- [ ] Real camera activation/permission prompt/denial/motion requires specific authorization: automatic approval review rejected the activation click. No workaround used. Physical Seeker/touch and OS reduced-motion testing not performed.
- [x] Report implementation, exact configurations, origin, privacy, performance and QA limits in PIXEL-EXPERIENCES.md. No stage, commit, push or deploy. Local preview remains running.

## Seeker refinement — 2026-10-08
- [x] Handheld navigation, safe areas, mobile market/cards/detail/Create layouts.
- [x] Axis-aware PSG/cartridge gestures, release-to-tear packs and bounded card queue.
- [x] Provider-ready wallet seam; demo identity isolated; 15 tests passing.
- [x] 375/390/412/432 CSS px + landscape and desktop review; production build passed.
- [ ] Physical Seeker/Chrome, actual touch media branches, keyboard, performance and real wallet integration remain future checks.
Details and evidence: [SEEKER-REVIEW.md](SEEKER-REVIEW.md).
