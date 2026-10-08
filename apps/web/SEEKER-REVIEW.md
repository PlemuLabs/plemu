# Plemu on Seeker — 2026-10-08

Incremental refinement of the existing Next.js frontend. Existing identity, desktop destinations, creation paths, local collections and simulated economy are preserved. No deployment, Git staging, commit or push. No Android packaging, real wallet connection or transactions.

## Completed

- [x] Compact top bar, reachable bottom navigation and safe-area spacing.
- [x] Mobile game cards, market rows, category chips, detail hierarchy, Create and Library.
- [x] PSG1 horizontal gesture recognition, tap selection and synchronized game metadata.
- [x] Pack tear, cancellation, reveal and card stack gestures.
- [x] Wallet provider boundary with explicitly separate demo identity.
- [x] Effects/performance audit and touch-specific rendering limits.
- [x] Browser layout/interaction verification, desktop regression and production checks.

## Layout decisions

`app/handheld.css` owns these adaptations after the established desktop stylesheet. The shell switches below 1000 CSS px, matching the existing header's narrow layout boundary. Detailed phone cards/forms switch at 700 px. Short landscape windows below 1000 px use an inline bottom bar and side-by-side console/metadata. These are logical viewport sizes, not the panel's physical resolution.

The compact top bar retains logo, search and wallet. Bottom navigation provides Discover, Games, Create, Packs and More. More opens a native modal with **all seven destinations** and wallet access. Active states remain visible; the native dialog provides focus containment, Escape and close controls. Bottom content clearance includes the device safe area. Header and bottom navigation have opaque surfaces on handheld layouts.

Cards use one readable column on phones. Artwork, title, token, price, change and platform remain visible; save controls are 44 px. Category chips and ranking highlights have intentional horizontal rails. Price is enlarged in game cards and cartridge captions. Market list mode becomes labelled two-column cards: identity, price/change, market cap and 24h volume. Desktop tables are preserved.

Game details show artwork, name, quote and actions first, followed directly by a 175 px chart. Timeframe buttons are 44 px; tap or horizontal inspection displays a sample price. Secondary metrics follow the chart. The redundant introductory block is omitted on phones; its full game description and studio remain in The game. Activity and the simulated trading panel follow. Trade rows have readable mobile typography.

Create retains all four paths and the existing editor. Inputs use 16 px text, primary controls are at least 48 px high, color controls are 44 px, step navigation scrolls intentionally, and previews stack below the editor. A preview title/genre change was verified without saving a new draft.

## Interaction audit

| Interaction | Handheld behavior | Desktop behavior |
| --- | --- | --- |
| PSG1 | 36 px horizontal swipe, vertical-scroll cancellation, tap screen for info, A/B and L/R controls; 44 px physical controls. Gentle bounded drag depth, no pointer-following tilt. | Existing tilt, keyboard and physical controls retained. |
| PSG1 selected game | Artwork/title/platform/token/price/change update from one selected game; existing connected screen/metadata transitions retained. | Preserved. |
| Cartridge browser | Axis-aware swipe on release; taps and previous/next controls remain. | Mouse drag, taps and arrow keys preserved. |
| Pack tear | Pointer Events; touch threshold 56–88 px based on wrapper width. Direction lock allows native vertical scrolling; limited stretch while held, release completes tear; short/cancelled drag springs back. | Same flow, 80–140 px threshold; Enter/Space and Open fallback preserved. |
| Collectible stack | Swipe front card to the back; velocity-aware flick and short-drag return. Bounded travel/rotation reserves space for stacked cards. 63:88 shape, larger price, readable platform. | Existing queue and keyboard controls retained. |
| HIT foil | Full-surface iridescence remains; continuous foil animation and pointer highlights disabled for touch. Widths below 1000 also suppress continuous foil animation. | Existing foil treatment retained. |
| Pixel Magnet | Disabled on touch/coarse/no-hover, reduced motion, and widths below 1000. No fake cursor. | Full pointer response retained; idle/offscreen pause retained. |
| Artwork light / magnetic CTAs | No hover-only action; stationary artwork and stable tap targets. | Fine-pointer effect retained. |
| Scroll scenes | No PSG pin or workbench scrub on handheld. Hero entry rotation omitted to prevent transient overflow. | Desktop pin/tilt remains on sufficiently tall fine-pointer layouts. |
| Pixel Mirror | Existing lazy canvas, 48×36 sampling at phone widths, 18 fps cap, lower pixel elevation; pauses offscreen/background. Explicit sample or camera controls. | Existing higher-density mode retained. |
| Charts | Tap/drag sample inspection and larger timeframe buttons; vertical page scroll allowed. | Hover and keyboard inspection remain. |

Reduced-motion fallbacks remain in effect. No WebGL runtime was introduced. No artificial 120 fps target. Artwork continues through Next Image; only the selected hero cartridge is eagerly loaded and the below-fold library feature no longer requests eager/priority loading. Canvas loops remain visibility-gated. Real-device frame time, thermal behavior and battery use have not been measured.

## Wallet architecture

`lib/wallet-gateway.ts` defines the connector/identity/result boundary, optional provider-owned signing capabilities and cancellation. Only the demo connector is registered. A missing mobile provider returns `PROVIDER_UNAVAILABLE`. Concurrent authorization attempts are rejected; a late authorization after cancellation is disconnected. Tests cover these cases. Demo balances and simulated trades are authorized only by an identity explicitly tagged `demo`; no real address is fabricated or persisted.

This is an integration seam, **not an implemented MWA connection**. The wallet dialog says mobile providers are coming later. Future integration must register a real adapter, bind its account/session UI, validate provider/chain/account changes and delegate authorization/signing to the wallet. No Seed Vault custody, biometric authentication, Seeker detection, .skr resolution, rewards or device attestation is claimed. Future verified identity belongs in a separate resolver associated with an authorized address.

The [official MWA documentation](https://docs.solanamobile.com/developers/mobile-wallet-adapter) currently lists Android and Chrome on Android support; arbitrary WebViews should not be assumed compatible. The [official Seeker specifications](https://solanamobile.com/seeker) confirm the 6.36-inch AMOLED, 2670×1200 panel, dynamic 120 Hz, 8 GB RAM and Seed Vault. Browser QA uses CSS widths independently of Android display scaling.

## Verification

- Layout matrix: `/`, `/market`, `/games`, `/game/starfall-protocol`, `/create`, `/create/pack`, `/packs`, `/rankings`, `/library` at 375×812, 390×844, 412×915, 432×960; 915×412 landscape. All four creation workspace routes also checked at 375 and 412.
- Measured document width equals viewport width on those routes. Fixed a transient 5 px entry-animation overflow at 375 and rechecked the home page. Intentional chips, ranking highlights and editorial rails are locally scrollable.
- Visually inspected phone home, game cards, market filtered list, game detail/chart/activity, wallet, PSG1, packs, Create editor, Pixel Mirror sample, ranking and Library. Landscape console and menu inspected. Desktop 1224×800 header remains 68 px and has the original seven-destination order.
- Interaction checks: bottom nav; More open/close and navigation; Games Arcade filter; Market Arcade/list filter; 1W chart and sample inspection; trade-activity tab; demo wallet connection; PSG A/B, screen tap, horizontal and vertical drags, genre button; pack short/full drag, sequential reveals and stack swipe; Create name/genre preview; ranking gainers; Library search; desktop console button and keyboard pack opening.
- `npm run build`: 26 routes generated successfully. Typecheck, formatting, 15 automated tests and `git diff --check` passed. No new dependencies.
- Screenshots: `qa/seeker-packs-412.png`, `qa/seeker-hit-390.png`, `qa/seeker-game-390.png`.

### Remaining physical-device checks

The browser tool resizes the viewport and sends native mouse/drag input. It does **not** emulate a physical Seeker touchscreen, Android keyboard, system cutouts, 120 Hz display or biometric wallet flow. The touch-specific threshold/media branches were reviewed in code; a real Seeker/Chrome pass is still needed for finger feel, Android back/keyboard behavior, safe-area insets and sustained performance. The camera remained off; Pixel Mirror was verified only with the local sample. No real wallet authorization was attempted.

## Future Android / dApp Store

The [Solana dApp Store guide](https://docs.solanamobile.com/dapp-store/intro) allows an existing web app to be wrapped as an Android app and submitted as an APK. A later release needs a hosted HTTPS frontend, a chosen Android wrapper/runtime with tested MWA handoff, signed APK/package identity and release assets, lifecycle/back/navigation and permission testing, and the store's submission/publisher-policy process. This Next.js app currently uses optimized image delivery; packaging should preserve its host or deliberately replace that service for an offline bundle. No wrapper or publishing changes were necessary for this UX iteration.
