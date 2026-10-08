# Plemu Guide — implementation checklist

Scope: add a dedicated /guide product manual and secondary global navigation entry; preserve existing product behavior and identity. No publishing or real transactions.

- [x] Eleven concise chapters, deep links, chapter navigation and a mobile selector.
- [x] Visual game/token/community/market relationship and eight product destinations.
- [x] Interactive discovery, price/chart, isolated practice trade and real tear/card components.
- [x] Current Create paths, Library, rankings and wallet guidance; accurately label prototype limits.
- [x] Responsive styling, keyboard/focus flow, reduced-motion fallback.
- [x] Browser QA for all chapters/demos, desktop and Seeker widths, build and existing tests.

## Delivered

Route: `/guide`. Secondary Guide link follows Library in desktop navigation, appears in mobile More, and in the footer. Eleven chapters: Start, Discover, Tokens, Market, Trading, Packs, Create, Library, Rankings, Wallet, Journey. Chapters have hash links, browser history, position indicators, previous/next actions and focus handling. Mobile uses a native chapter selector at the existing 1000px handheld breakpoint.

Existing GameCard, GamePrice, Chart, TearPack, PackObject, CollectibleCardStack, icons, artwork, game data, Create path data and demo trade calculations are reused. Pack demonstrations load only when their chapter is opened. No dependency added. Shared tear/card controls accept an optional reduced-motion override; default product behavior is retained. Chart sample tooltips now expose an accessible status announcement.

## Verification

- Opened all 11 chapters at 390px. No horizontal document overflow or controls outside the viewport.
- Visual/layout checks at 375×812, 390×844, 412×915, 432×960, 915×412, 1000×800 and 1224×800/860. Added desktop menu item fits at 1000px without colliding with logo or wallet/search.
- Genre selection updates the shared game card; tokens relationship buttons update their lesson; rising/falling charts and metric explanations work.
- Chart keyboard arrows select a sample, and its value appears in the accessible status region.
- Practice Buy review and completion work without connecting a wallet or writing a portfolio; excessive Sell amount correctly refuses with a visible explanation. The existing immutable trade-calculation tests pass.
- Pack opens through a horizontal pointer drag and Enter. Reveals the known three cards; Next switches order. Reduced-motion preference opens and switches instantly, with no running stack transition. Reset returns keyboard focus to the pack.
- Rankings switch to losers and reorder accurately. Wallet Connect/Sign explanations switch with the keyboard; wallet options open the existing demo dialog and Escape closes it. No wallet connection or real transaction performed.
- Library bookmark toggled and restored to its original state. Create a Pack link opens the correct workspace; browser Back restores the Guide chapter. Chapter next, back and forward preserve matching URL and content.
- Mobile More includes Guide and closes after selecting it.
- `npm run build`: passes; 27 generated pages, including Guide. Typecheck passes. All 15 existing tests pass. Formatting passes.
- No runtime errors found in the browser. An initial image loading advisory was resolved by marking the manual cover artwork eager.

Screenshots: `qa/guide-desktop.png`, `qa/guide-market-412.png` (local ignored QA artifacts).

## Prototype boundaries and follow-up

All game/token/market figures remain fictional. Guide trading is isolated practice; product trading remains a local demo. Bookmarks and drafts are browser-local. Public publishing, AI generation and real wallet signing remain unavailable, and are explained as such. Foil HIT is a deterministic sample-price highlight, not a random reward.

Browser viewport testing does not substitute for a physical Seeker or Android touch test. Pointer gestures and keyboard alternatives were exercised. The explicit guide reduced-motion option was tested end to end; native system preference support was reviewed in CSS/handlers, without changing OS settings. Full screen-reader output was not hardware-tested; labels, focus and live regions were inspected through the browser accessibility tree.

Possible future work: Portuguese copy, remembered chapter progress, and updated lessons when live market/wallet/publishing integrations arrive. No staging, commits or push. The existing local application stays running for review.
