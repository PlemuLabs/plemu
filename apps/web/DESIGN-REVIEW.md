# Plemu — play system, iteration 2

## What changed and why

The first pass had useful product infrastructure: typed catalog data, original art, reusable charts, a local trading simulation, persistent saved games, a Creator Studio, and an arcade demo. Its opening story and repeated price cards made finance feel like the primary purpose. Small navigation compounded the hierarchy problem. The implementation was extended rather than replaced.

The new sequence is **Discover → Play → Create → Collect → Explore the economy**. Warm paper, charcoal, orange, mint, and pale blue establish distinct spaces. Silkscreen is reserved for display moments and game objects; DM Sans and Space Grotesk remain the readable product typography. The seven primary destinations are Discover, Games, Create, Packs, Market, Rankings, and Library.

## Signature interactions

1. **Cartridge browsing.** Original molded-shell objects wrap the existing artwork. Selection brings one game forward and turns neighboring cartridges in perspective. Pointer drag, touch swipe, arrow keys, previous/next buttons, and individual selection share the same state. Titles link to game profiles. No auto-rotation.
2. **PSG1 discovery.** A CSS/DOM interpretation of the PSG1 hardware carries a real interactive screen. A finite screen-on effect and desktop pinned scene establish the device; all content remains readable without motion. D-pad cycles games; L/R changes genres; A/Start opens screen information; B returns; X/Select switches the adjacent game/economy panel; Y saves the current game. Keyboard arrows, A/B, Enter/Escape and large touch browsing controls are supported. The on-screen link opens the game profile. This is a browser concept, not a device connection or emulator.
3. **Pack reveals.** Three themed packs always contain the three games listed before opening. An unwrapping action reveals the first cartridge; subsequent presses reveal the rest. Saving deduplicates games into the local library and records the collected pack. There are no random rewards, paid draws, fabricated scarcity, or allocation of tokens.
4. **Creator workbench.** Choose a game/pack concept and starter, edit the name, genre, story, and color, then preview/save. Changes update the visual object immediately. A versioned local record restores on return; validation and storage failures give actionable feedback. Existing advanced Studio drafts remain independent.
5. **Game → economy.** Game descriptions and identity now precede the terminal. The PSG1’s explicit economy state introduces a sample token/chart in context. Market cards keep precise figures; discovery/library cards use story and artwork instead.

## React Bits evaluation

Public documentation and previews were inspected. The available browser session shows the Skewed Carousel source behind an “Unlock” paid-plan screen. **No React Bits Pro component source or package was copied or installed.** The following are original Plemu implementations inspired by selected public interaction patterns.

| Reference | Decision |
| --- | --- |
| [Skewed Carousel](https://pro.reactbits.dev/docs/components/skewed-carousel) | Adopt the selected-object/perspective browsing idea in an original cartridge rail with explicit navigation and no automatic motion. |
| [Click Stack](https://pro.reactbits.dev/docs/components/click-stack) | Adapt the progressive stack idea into a finite three-world reveal with visible contents and a clear collection action. |
| [Depth Card](https://pro.reactbits.dev/docs/components/depth-card) | Do not add a separate pointer-tilt card system; focus depth in the cartridge selection and device scene already explains hierarchy. |
| [Circle Gallery](https://pro.reactbits.dev/docs/components/circle-gallery) | Reject a second gallery mechanism. Circular rotation competes with cartridge browsing and makes labels harder to scan. |
| [Pixel Magnet](https://pro.reactbits.dev/docs/components/pixel-magnet) | Reject a cursor-following swarm; it contributes little to selecting a game or completing a task. |
| [Dither Cursor](https://pro.reactbits.dev/docs/components/dither-cursor) | Reject global trails. Use static texture and finite screen transitions without obstructing precise controls. |
| [Cursor Wave](https://pro.reactbits.dev/docs/components/cursor-wave) | Reject a canvas cursor grid; the creator canvas already communicates interactivity through its live preview. |
| [Blinking Squares](https://pro.reactbits.dev/docs/components/blinking-squares) | Keep the pixel-grid idea as a static creator texture. Avoid continual ambient blinking. |
| [Device](https://pro.reactbits.dev/docs/components/device) | Its phone framing is the wrong geometry for a PSG1; use original DOM hardware with individually operable controls. |
| [Frame Scrub](https://pro.reactbits.dev/docs/components/frame-scrub) | Avoid a decoded frame/video sequence. The console needs live buttons and game state, and the existing GSAP scene achieves the presentation with less media. |
| [Parallax Cards](https://pro.reactbits.dev/docs/components/parallax-cards) | Avoid another freely moving stack. The cartridge rail and finite pack stack already give the objects a clear purpose. |

[Play Solana](https://www.playsolana.com/) informed the PSG1’s portrait body, screen, D-pad, face buttons, start/select, and dual-stick arrangement. The frontend labels it as a Plemu concept. Steam, Pons, and Pump public pages informed discovery categories, compact asset browsing, and the retained game-market relationship. AgencyPad and StonkFun were unavailable to the web reader during this pass; no unverified implementation claims are based on them.

## Technical boundaries

Next.js / React / TypeScript, the existing GSAP dependency, reusable charts, and original optimized art are retained. The only new dependency in iteration 2 is self-hosted `@fontsource/silkscreen`. There is no WebGL or canvas effect stack. CSS transforms carry object transitions; GSAP contexts clean up on navigation and rebuild at breakpoints. Reduced-motion styles and media conditions remove animated/pinned scenes. Smaller screens use stacked layouts, native horizontal discovery scrolling, and touch-sized alternative console controls.

The new routes are `/games`, `/create`, and `/packs`. Components own local interaction state; `lib/packs.ts` and `lib/creator-draft.ts` define the new data. The existing product provider adds idempotent pack collection without changing demo trade arithmetic. `app/play-system.css` extends the retained base component styles; legacy Creator Studio keeps its scoped theme.

All markets, tokens, holdings, community activity, and twelve game concepts remain fictional. Packs are local saved collections, not entitlements. The Create workbench does not generate executable games, publish projects, mint assets, upload files, or assemble real distributable packs. The shared Neon Orbit arcade is still the playable demo.

## Review record

Browser checks cover desktop 1440, tablet 768, and mobile 390 layouts; cartridge selection; PSG1 controls and genres; pack reveals, saving, and library persistence; creator preview, validation, saving, and restore; game filters/sorts; wallet entry and a reviewed demo buy; market list/loser sorting; and ranking category changes. No horizontal overflow was observed in the inspected layouts after correcting the console entrance offset. Reduced-motion behavior is verified in the implementation; operating-system reduced-motion emulation was not available through the browser tool.

Next product work: connect real catalog/account data; define server-verified pack contents and entitlement contracts; connect the Creator workbench to real game projects; use a Solana Devnet wallet/settlement adapter; add authentic community activity; and broaden screen-reader and real-device testing. Preserve signed-manifest and raw-byte verification boundaries when connecting content.
