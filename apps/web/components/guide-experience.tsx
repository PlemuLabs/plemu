"use client";
import { useEffect, useRef, useState } from "react";
import dynamic from "next/dynamic";
import Image from "next/image";
import Link from "next/link";
import {
  guideChapters,
  guideDestinations,
  isGuideChapter,
  type GuideChapter,
} from "../lib/guide";
import { creationPaths, publicationTiers } from "../lib/creation-paths";
import { games } from "../lib/games";
import {
  DiscoveryDemo,
  MarketDemo,
  PracticeTrade,
  RankingDemo,
  RelationshipDemo,
} from "./guide-demos";
import { GameCard } from "./game-card";
import { Icon } from "./icons";
import { useProduct } from "./product-provider";

const GuidePackDemo = dynamic(
  () => import("./guide-pack-demo").then((module) => module.GuidePackDemo),
  { loading: () => <p role="status">Getting your practice pack ready…</p> },
);
const journey = [
  [
    "Discover a game",
    "Follow an image, a genre or a little curiosity.",
    "/games",
  ],
  [
    "Explore its world",
    "Meet the artwork, story and platform.",
    "/game/starfall-protocol",
  ],
  [
    "Meet its asset",
    "Find the token, price and sample activity.",
    "/game/starfall-protocol#economy",
  ],
  ["Save a favorite", "Use the bookmark to keep a shortcut.", "/library"],
  ["Open a pack", "Let a curated collection introduce three worlds.", "/packs"],
  ["Make something", "Give your own idea a shape and a name.", "/create"],
  ["Follow the market", "Read the movement behind the numbers.", "/market"],
  [
    "Try a demo trade",
    "Review a pretend buy or sell with demo funds.",
    "/game/starfall-protocol#economy",
  ],
  [
    "Come back to your Library",
    "Your saved worlds are waiting here.",
    "/library",
  ],
];

function ChapterAction({
  href,
  children,
}: {
  href: string;
  children: React.ReactNode;
}) {
  return (
    <div className="guide-chapter-action">
      <Link className="button primary" href={href}>
        {children}
        <Icon name="arrow" />
      </Link>
    </div>
  );
}

function WalletLesson() {
  const [mode, setMode] = useState<"connect" | "sign">("connect");
  const { openWallet } = useProduct();
  return (
    <>
      <div className="guide-wallet-card">
        <div className="guide-wallet-symbol">
          <Icon name="wallet" size={40} />
          <span className="eyebrow">YOU STAY IN CONTROL</span>
        </div>
        <div>
          <div className="guide-chips" aria-label="Wallet concepts">
            <button
              onClick={() => setMode("connect")}
              aria-pressed={mode === "connect"}
            >
              01 · Connect
            </button>
            <button
              onClick={() => setMode("sign")}
              aria-pressed={mode === "sign"}
            >
              02 · Sign
            </button>
          </div>
          <div className="guide-teaching-copy" key={mode} aria-live="polite">
            <h3>
              {mode === "connect"
                ? "Say hello with your public address."
                : "Approve a specific action."}
            </h3>
            <p>
              {mode === "connect"
                ? "Connecting lets an app identify your wallet and request actions. Connecting alone does not send your assets. Your wallet handles any approval it asks you for."
                : "Signing is your approval of a specific message or transaction. Read the request: which app, which asset, what amount and any fees. Only approve an action you intended; you can decline."}
            </p>
          </div>
        </div>
      </div>
      <div className="guide-inline-note">
        <Icon name="wallet" />
        <p>
          <strong>In Plemu today: a demo wallet.</strong> The Wallet button
          opens a preview. Connect demo wallet to try a local portfolio with
          pretend funds. No real assets move. Real Solana wallet connection and
          transaction signing are not connected yet.
        </p>
      </div>
      <p className="guide-small">
        The real-wallet steps above explain a future flow. The current demo
        never needs a recovery phrase or private key. A saved game is a
        bookmark, not proof that a wallet owns its token.
      </p>
      <button className="button secondary" onClick={openWallet}>
        See wallet options <Icon name="wallet" />
      </button>
    </>
  );
}

function ChapterContent({
  chapter,
  reducedMotion,
}: {
  chapter: GuideChapter;
  reducedMotion: boolean;
}) {
  switch (chapter) {
    case "start":
      return (
        <>
          <div
            className="guide-concept-path"
            aria-label="Game, token, community, market"
          >
            {[
              ["play", "Game"],
              ["spark", "Token"],
              ["users", "Community"],
              ["chart", "Market"],
            ].map(([icon, label], i) => (
              <div key={label}>
                <Icon name={icon} size={26} />
                <strong>{label}</strong>
                {i < 3 ? <span aria-hidden="true">→</span> : null}
              </div>
            ))}
          </div>
          <p className="guide-lead">
            Find a game. Meet its world and its community. Explore the
            associated token and market when you're ready.
          </p>
          <div className="guide-destinations">
            {guideDestinations.map((item) => (
              <Link href={item.href} key={item.title}>
                <Icon name={item.icon} size={23} />
                <div>
                  <h3>{item.title}</h3>
                  <p>{item.copy}</p>
                </div>
                <Icon name="diagonal" size={16} />
              </Link>
            ))}
          </div>
          <div className="guide-inline-note">
            <Icon name="spark" />
            <p>
              <strong>You're exploring a working preview.</strong> Games, token
              quotes and market activity are sample data. Bookmarks and creator
              drafts stay in this browser. Trading uses pretend funds.
            </p>
          </div>
        </>
      );
    case "discover":
      return (
        <>
          <DiscoveryDemo />
          <p className="guide-small">
            The catalog contains game concepts. The playable arcade demo is a
            separate original game, not a launch of each listed title. Bookmark
            buttons here really save to your local Library.
          </p>
          <ChapterAction href="/games">Find your next game</ChapterAction>
        </>
      );
    case "tokens":
      return (
        <>
          <RelationshipDemo />
          <div className="guide-two-notes">
            <div>
              <h3>One game. Two different things.</h3>
              <p>
                The game is the creative world. Its token is an associated
                digital asset. Owning one doesn't automatically mean owning or
                being able to play the other.
              </p>
            </div>
            <div>
              <h3>A price is a snapshot.</h3>
              <p>
                It shows the cost of one token at a moment in time. Prices can
                rise or fall. Follow the game because it interests you; learn
                what the asset actually offers.
              </p>
            </div>
          </div>
          <ChapterAction href="/game/starfall-protocol">
            Explore this world
          </ChapterAction>
        </>
      );
    case "market":
      return (
        <>
          <MarketDemo />
          <ChapterAction href="/market">Explore the Market</ChapterAction>
        </>
      );
    case "trading":
      return (
        <>
          <PracticeTrade />
          <h3 className="guide-subheading">How this maps to the product</h3>
          <ol className="guide-numbered">
            <li>
              <strong>Choose a game and read its Economy view.</strong>
              <span>
                Check the token name, sample price and chart before entering an
                amount.
              </span>
            </li>
            <li>
              <strong>Connect the demo wallet, then choose Buy or Sell.</strong>
              <span>
                The current product uses a local pretend balance. Selling
                requires enough of that example token.
              </span>
            </li>
            <li>
              <strong>Review, then confirm the demo trade.</strong>
              <span>
                The product updates its local demo portfolio. This guide's
                practice desk above updates nothing.
              </span>
            </li>
            <li>
              <strong>Real wallet approval comes later.</strong>
              <span>
                A real transaction would need a supported wallet and a separate
                approval of the amount, asset and fees. That integration is not
                available in this preview.
              </span>
            </li>
          </ol>
          <ChapterAction href="/game/starfall-protocol#economy">
            See the demo trading view
          </ChapterAction>
        </>
      );
    case "packs":
      return (
        <>
          <GuidePackDemo reducedMotion={reducedMotion} />
          <ChapterAction href="/packs">Explore all Packs</ChapterAction>
        </>
      );
    case "create":
      return (
        <>
          <div className="guide-create-grid">
            {creationPaths.map((path) => (
              <Link href={`/create/${path.id}`} key={path.id}>
                <span className="guide-path-icon">
                  <Icon name={path.icon} size={26} />
                </span>
                <span className="eyebrow">{path.label}</span>
                <h3>{path.title}</h3>
                <p>{path.intro}</p>
                <span className="text-link">
                  Open workspace <Icon name="arrow" size={16} />
                </span>
              </Link>
            ))}
          </div>
          <ol className="guide-creation-steps">
            <li>
              <b>01</b>
              <strong>Choose a starter</strong>
              <span>Pick a template for your idea.</span>
            </li>
            <li>
              <b>02</b>
              <strong>Make it yours</strong>
              <span>Edit the name, look and description.</span>
            </li>
            <li>
              <b>03</b>
              <strong>Preview & save</strong>
              <span>Save your concept in this browser.</span>
            </li>
          </ol>
          <h3 className="guide-subheading">Where publishing is heading</h3>
          <p className="guide-small">
            These are intended distribution paths. Saving a concept in today's
            web workspace does not submit, mint, deploy or publish it. Submit a
            Game also links to the advanced studio for submission preparation.
          </p>
          <div className="guide-reach">
            {publicationTiers.map((tier, i) => (
              <div key={tier.title}>
                <span>
                  0{i + 1} / {tier.reach}
                </span>
                <strong>{tier.title}</strong>
                <p>{tier.description}</p>
              </div>
            ))}
          </div>
          <ChapterAction href="/create">Choose your Create path</ChapterAction>
        </>
      );
    case "library":
      return (
        <>
          <div className="guide-demo-split">
            <div className="guide-example-card">
              <GameCard game={games[2]} visual />
              <p className="guide-small">
                Try the bookmark above. It adds or removes this game in your
                actual local Library.
              </p>
            </div>
            <ol className="guide-numbered">
              <li>
                <strong>Save what catches your eye.</strong>
                <span>
                  Tap the bookmark on a game card. Saving a revealed pack adds
                  its included games too.
                </span>
              </li>
              <li>
                <strong>Find it again, your way.</strong>
                <span>
                  Search saved titles or tokens. Use genre filters and sorting
                  to organize your view.
                </span>
              </li>
              <li>
                <strong>Revisit the world and its activity.</strong>
                <span>
                  Open a saved game to read its story, platform, price and
                  chart.
                </span>
              </li>
            </ol>
          </div>
          <div className="guide-inline-note">
            <Icon name="bookmark" />
            <p>
              <strong>A collection of shortcuts.</strong> Saves live in this
              browser on this device. They are not synced to a wallet and do not
              grant tokens, licenses or game access. Clearing browser storage
              removes them.
            </p>
          </div>
          <ChapterAction href="/library">Open your Library</ChapterAction>
        </>
      );
    case "rankings":
      return (
        <>
          <RankingDemo />
          <ChapterAction href="/rankings">Explore every ranking</ChapterAction>
        </>
      );
    case "wallet":
      return <WalletLesson />;
    case "journey":
      return (
        <>
          <ol className="guide-journey">
            {journey.map(([title, copy, href], i) => (
              <li key={title}>
                <Link href={href}>
                  <span>{String(i + 1).padStart(2, "0")}</span>
                  <div>
                    <h3>{title}</h3>
                    <p>{copy}</p>
                  </div>
                  <Icon name="diagonal" />
                </Link>
              </li>
            ))}
          </ol>
          <div className="guide-finish">
            <span className="pixel-text">Player one is you.</span>
            <p>
              No perfect starting point required. Take the path that makes you
              curious.
            </p>
            <div className="button-row">
              <Link className="button primary" href="/games">
                Discover a game <Icon name="play" />
              </Link>
              <Link className="button secondary" href="/create">
                Make something <Icon name="spark" />
              </Link>
            </div>
          </div>
        </>
      );
  }
}

export function GuideExperience() {
  const [current, setCurrent] = useState<GuideChapter>("start");
  const [reducedMotion, setReducedMotion] = useState(false);
  const heading = useRef<HTMLHeadingElement>(null);
  const moveFocus = useRef(false);
  const index = guideChapters.findIndex((item) => item.id === current);
  const chapter = guideChapters[index];
  useEffect(() => {
    const sync = () => {
      const id = window.location.hash.slice(1);
      if (isGuideChapter(id)) setCurrent(id);
      else if (!id) setCurrent("start");
    };
    sync();
    window.addEventListener("hashchange", sync);
    window.addEventListener("popstate", sync);
    return () => {
      window.removeEventListener("hashchange", sync);
      window.removeEventListener("popstate", sync);
    };
  }, []);
  useEffect(() => {
    if (moveFocus.current) {
      heading.current?.focus({ preventScroll: true });
      heading.current?.scrollIntoView({ block: "start", behavior: "instant" });
      moveFocus.current = false;
    }
  }, [current]);
  function goTo(id: GuideChapter) {
    if (id === current) {
      heading.current?.focus();
      heading.current?.scrollIntoView({ block: "start", behavior: "instant" });
      return;
    }
    moveFocus.current = true;
    window.history.pushState(null, "", `#${id}`);
    setCurrent(id);
  }
  return (
    <div
      className="page-container guide-page"
      data-reduced-motion={reducedMotion}
    >
      <header className="guide-hero">
        <div>
          <span className="eyebrow">PLEMU / PLAYER MANUAL / VOL. 01</span>
          <h1>
            Your Plemu<span className="pixel-text">field guide.</span>
          </h1>
          <p>
            Everything you can do here, explained.
            <br />
            No Web3 experience needed. Just curiosity.
          </p>
          <div className="button-row">
            <button className="button primary" onClick={() => goTo("start")}>
              Start exploring <Icon name="arrow" />
            </button>
            <button className="text-link" onClick={() => goTo("packs")}>
              Just here for Packs? <Icon name="grid" />
            </button>
          </div>
        </div>
        <div className="guide-manual-cover" aria-hidden="true">
          <div className="guide-cover-top">
            <Image src="/brand/plemu-mark.svg" alt="" width={28} height={28} />
            <span>PLEMU FIELD GUIDE</span>
            <span>01</span>
          </div>
          <div className="guide-cover-art">
            <Image
              src={games[0].artwork}
              alt=""
              loading="eager"
              fill
              sizes="(max-width: 700px) 80vw, 340px"
            />
            <span className="pixel-text">
              Press
              <br />
              curiosity.
            </span>
          </div>
          <div className="guide-cover-footer">
            <span>PLAY · COLLECT · CREATE</span>
            <Icon name="spark" size={24} />
          </div>
          <span className="guide-cover-sticker">
            BEGINNERS
            <br />
            WELCOME ↗
          </span>
        </div>
      </header>
      <div className="guide-preferences">
        <span>11 chapters. No experience required.</span>
        <label>
          <input
            type="checkbox"
            checked={reducedMotion}
            onChange={(event) => setReducedMotion(event.target.checked)}
          />
          Reduce motion in this guide
        </label>
      </div>
      <div className="guide-layout">
        <aside className="guide-sidebar">
          <div className="guide-sidebar-top">
            <span className="eyebrow">YOUR FIELD NOTES</span>
            <span>{String(index + 1).padStart(2, "0")} / 11</span>
          </div>
          <progress
            value={index + 1}
            max={guideChapters.length}
            aria-label="Current chapter position"
          />
          <nav aria-label="Guide chapters">
            {guideChapters.map((item, i) => (
              <a
                href={`#${item.id}`}
                key={item.id}
                aria-current={current === item.id ? "step" : undefined}
                onClick={(event) => {
                  event.preventDefault();
                  goTo(item.id);
                }}
              >
                <span>{String(i + 1).padStart(2, "0")}</span>
                <Icon name={item.icon} size={16} />
                {item.title}
              </a>
            ))}
          </nav>
          <p>
            Pick a chapter.
            <br />
            Learn at your own pace.
          </p>
        </aside>
        <div className="guide-reading-area">
          <div className="guide-mobile-chapters">
            <label htmlFor="guide-chapter-select">
              CHAPTER {String(index + 1).padStart(2, "0")} / 11
            </label>
            <select
              id="guide-chapter-select"
              value={current}
              onChange={(event) => goTo(event.target.value as GuideChapter)}
            >
              {guideChapters.map((item, i) => (
                <option key={item.id} value={item.id}>
                  {String(i + 1).padStart(2, "0")} — {item.title}
                </option>
              ))}
            </select>
            <progress
              value={index + 1}
              max={guideChapters.length}
              aria-label="Current chapter position"
            />
          </div>
          <section
            className="guide-chapter"
            aria-labelledby="guide-chapter-heading"
          >
            <header className="guide-chapter-heading">
              <span className="guide-chapter-label">
                <Icon name={chapter.icon} />
                CHAPTER {String(index + 1).padStart(2, "0")} /{" "}
                {chapter.title.toUpperCase()}
              </span>
              <h2 id="guide-chapter-heading" tabIndex={-1} ref={heading}>
                {chapter.heading}
              </h2>
              <p>{chapter.intro}</p>
            </header>
            <div className="guide-chapter-content" key={current}>
              <ChapterContent chapter={current} reducedMotion={reducedMotion} />
            </div>
            <nav
              className="guide-pagination"
              aria-label="Previous and next chapter"
            >
              {index > 0 ? (
                <button onClick={() => goTo(guideChapters[index - 1].id)}>
                  <Icon name="back" />
                  <span>
                    <small>PREVIOUS</small>
                    {guideChapters[index - 1].title}
                  </span>
                </button>
              ) : (
                <span className="guide-pagination-start">Let's begin ↗</span>
              )}
              {index < guideChapters.length - 1 ? (
                <button onClick={() => goTo(guideChapters[index + 1].id)}>
                  <span>
                    <small>NEXT CHAPTER</small>
                    {guideChapters[index + 1].title}
                  </span>
                  <Icon name="arrow" />
                </button>
              ) : (
                <button onClick={() => goTo("start")}>
                  <span>
                    <small>THE MANUAL IS ALWAYS HERE</small>Back to the
                    beginning
                  </span>
                  <Icon name="arrow" />
                </button>
              )}
            </nav>
          </section>
        </div>
      </div>
    </div>
  );
}
