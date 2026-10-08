# Plemu Web — play, create, collect

A product preview by Plemu Labs, built on Next.js 16 / React 19 / TypeScript. Discover games, shape creator concepts, open collectible packs and learn the sample economy through an interactive Guide. The current identity uses charcoal surfaces, Plemu violet/lime and a pixel-face logo.

## Run locally

Requires Node.js 22.18 or newer.

```sh
cd apps/web
npm ci
npm run dev
```

Open http://127.0.0.1:3000. The server binds to loopback. `npm run build` checks the production build; `npm start` serves it.

```sh
npm run typecheck
npm test
npm run format:check
npm run build
```

## Product spaces

| Route | Experience |
| --- | --- |
| `/` | Warm editorial opening, interactive cartridge rail, controllable PSG1 scroll scene, Create and Packs showcases, game discovery, sample economy and community favorites |
| `/games` | Cartridge discovery, featured world, real catalog genres, search, trending/recent/community sorting, artwork-first cards |
| `/create` | Four-path creation hub and opt-in, local-only Pixel Mirror |
| `/create/[path]` | Pack, Submit, Mod and AI Lab concept workspaces: choose a starter, edit name/genre/story/color, preview and save locally; advanced Studio entry |
| `/packs` | Three curated packs with known contents, tear-open interaction, collectible card reveals, replay and save-to-library behavior |
| `/library` | Persistent saved games and collected packs, plus a discovery tab and genre/search/sort controls |
| `/market` | Search, genre filters, six market sort modes, grid/list, highlights, and empty-result recovery |
| `/game/[id]` | Twelve game profiles: prominent artwork and story, saved collection, arcade demo, sample market/chart, tabs, community preview and reviewed buy/sell simulation |
| `/rankings` | Six categories, illustrated top-three podium, complete leaderboard; community popularity is the default |
| `/creator` | Retained Portuguese advanced Creator Studio with its own scoped theme and local drafts |
| `/guide` | Eleven beginner chapters with direct links, category/token/market demonstrations, an isolated practice trade, a tearable pack and wallet explanations |

## Interactions

- **Cartridges:** drag/swipe, select an object, use arrow keys or previous/next buttons, then follow the selected title.
- **PSG1:** D-pad changes game; L/R changes genre; A/Start opens information; B returns; X/Select changes the game/economy panel; Y saves the game. Arrow keys, A/B, Enter/Escape work when the device is focused. Mobile/tablet also have large browse buttons and a genre select. The console is a live DOM interface, not a hardware connection.
- **Packs:** contents are visible before opening. Tear the seal, reveal three predetermined worlds and swipe cards to the back of the stack. The foil HIT is determined by the highest sample token price. Save the games to the library; reopening does not duplicate games or give assets.
- **Create:** each creation path has an independent versioned local draft. The advanced Studio and its preexisting drafts remain available. Mod and AI Lab prepare concepts; neither game patching nor AI generation is connected.
- **Pixel Mirror:** camera access starts only after an explicit action. The local canvas processes no audio and uploads or records nothing. A sample works without camera access; cancellation, hidden tabs and unmount release camera tracks.
- **Guide:** chapters use a desktop contents rail and a native mobile selector. Reused game cards, charts and packs teach the actual product; its practice trade never changes a wallet or portfolio. Keyboard actions and an explicit reduced-motion option complement system preferences.
- **Demo wallet:** starts with 1,000 sample USDC, 250 STAR, and 800 MOSS. Review before confirming; overdrafts, overselling, disconnected transactions, and invalid amounts are rejected. Integer cents are used for balance changes. Refresh disconnects the demo wallet; its portfolio remains local.
- **Arcade:** the original Neon Orbit is playable in a sandboxed dialog from game profiles. Use arrow keys/A/D, P to pause, or touch arrow buttons. The concept games are explicitly separate from this shared demo.

## Architecture

- `app/`: thin routes, metadata, static game generation, error/not-found views. `play-system.css` contains design tokens and object/scene styles; `globals.css` retains shared market/dialog/layout components. `handheld.css` adapts touch layouts, navigation and safe-area spacing; `guide/guide.css` scopes the manual.
- `components/app-shell.tsx`, `dialog.tsx`, `product-provider.tsx`: navigation, native modal dialogs, versioned storage, demo portfolio, saved games and idempotent pack collection.
- `components/cartridge.tsx`, `psg1-experience.tsx`, `packs-experience.tsx`, `create-experience.tsx`: bounded signature interactions with local state.
- `components/*-experience.tsx`: page composition and browse state. `game-card.tsx` supplies game objects, visual discovery cards, and compact market rows.
- `components/chart.tsx`, `trade-panel.tsx`, `game-detail.tsx`: reusable SVG charts, demo order flow, and game profiles.
- `lib/games.ts`: typed twelve-game catalog, deterministic sample chart histories, activity, formatting and sorting.
- `lib/packs.ts`: curated pack identities and game relationships.
- `lib/creator-draft.ts`: starter concepts and persisted-draft validation.
- `lib/demo-economy.ts`: pure typed simulation isolated from presentation and storage. Eight tests cover its balance, precision, rejection and history invariants.
- `lib/camera-session.ts`, `lib/wallet-gateway.ts`: cancellable camera ownership and a provider boundary that keeps demo identity separate from future Solana sessions. Seven additional tests cover lifecycle cleanup and wallet isolation.

## Art, typography, and motion

Twelve original 1600 × 900 pixel-world illustrations are optimized in `public/art/*.webp`; see [ART-DIRECTION.md](ART-DIRECTION.md) for the prompts and mapping. Existing assets are reused in iteration 2.

Fonts are self-hosted: DM Sans for body UI, Space Grotesk for headings, DM Mono for data, and **Silkscreen** for selective retro display moments. GSAP handles bounded interactions; Prettier checks formatting. The production logo is available as SVG and transparent PNG in `public/brand/`.

GSAP/ScrollTrigger drive the desktop console arrival/pin and short contextual reveals. CSS perspective, finite reveal transitions, chart drawing, and restrained magnetic feedback provide depth. There is no WebGL effect stack or perpetual carousel. Motion cleans up on navigation; reduced-motion visitors receive static readable content and usable controls. Touch layouts use stacked scenes instead of desktop pinning.

No React Bits Pro source was installed or copied. Public interactions informed original Plemu components; the Pixel Mirror adapts the public Aceternity Webcam Pixel Grid. See [DESIGN-REVIEW.md](DESIGN-REVIEW.md), [INTERACTION-REVIEW.md](INTERACTION-REVIEW.md) and [PIXEL-EXPERIENCES.md](PIXEL-EXPERIENCES.md) for provenance and iteration records. [SEEKER-REVIEW.md](SEEKER-REVIEW.md) and [GUIDE-REVIEW.md](GUIDE-REVIEW.md) document mobile and manual validation.

## Mock boundaries and next steps

Games, studios, tokens, prices, market activity, and community previews are fictional. The wallet and trading are local simulations; no signature, RPC, or real transaction occurs. Packs save local collections and do not grant ownership, tokens, or game access. Create saves a visual concept; it does not generate or publish a game or issue a token. Browser storage must never authorize content or commerce.

Next: catalog/account APIs; real playable game artifacts; server-verified pack contents and entitlements; real creator projects; Devnet wallet and settlement adapters; authenticated communities; and broader accessibility/cross-browser testing. Frozen manifests, launch gates, and existing commerce services are unchanged.

## Repository hygiene

Only frontend source, tests, documentation and optimized production assets belong in this directory's commits. Environment files, dependencies, build output, local QA screenshots and artwork intermediates are ignored. Historical review records describe the checks and local-only status at the time of each iteration; they are not deployment instructions. Publishing source to GitHub does not deploy the application or enable real transactions.
