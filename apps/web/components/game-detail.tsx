"use client";
import Image from "next/image";
import Link from "next/link";
import { GamePrice } from "./game-price";
import { useState } from "react";
import { type Game, games, money } from "../lib/games";
import { Icon } from "./icons";
import { Change, GameCard } from "./game-card";
import { Chart } from "./chart";
import { Dialog } from "./dialog";
import { useProduct } from "./product-provider";
import { TradePanel } from "./trade-panel";
const timeframes = ["1H", "4H", "1D", "1W", "1M", "ALL"];
export function GameDetail({ game }: { game: Game }) {
  const [range, setRange] = useState("1D");
  const [tab, setTab] = useState("overview");
  const [play, setPlay] = useState(false);
  const [community, setCommunity] = useState(false);
  const { saved, toggleSaved, portfolio, notify } = useProduct();
  const rangeIndex = timeframes.indexOf(range);
  const rangeData = game.chart.map((v, i) =>
    range === "1D"
      ? v
      : v *
        (1 +
          Math.sin(i * 0.22 + rangeIndex) *
            Math.sin((Math.PI * i) / 47) *
            (rangeIndex + 1) *
            0.03),
  );
  const ownTrades = portfolio.trades.filter((t) => t.gameId === game.id);
  async function copyToken() {
    try {
      await navigator.clipboard.writeText(`DEMO_${game.symbol}`);
      notify("Demo token identifier copied. This is not an on-chain address.");
    } catch (error) {
      console.error("PLEMU_CLIPBOARD_FAILED", error);
      notify("Clipboard unavailable. Demo identifier: DEMO_" + game.symbol);
    }
  }
  return (
    <div className="page-container detail-page">
      <div className="detail-breadcrumb">
        <Link href="/games">
          <Icon name="back" size={14} />
          Games
        </Link>
        <span>/</span>
        <span>{game.name}</span>
        <span className="detail-data-note">
          <i className="live-dot" /> SIMULATED GAME ASSET
        </span>
      </div>
      <section className="game-banner">
        <Image
          src={game.artwork}
          alt={`${game.name} cinematic pixel world`}
          fill
          priority
          loading="eager"
          sizes="100vw"
        />
        <div className="banner-shade" />
        <div className="game-banner-copy">
          <div className="banner-badges">
            <span className="genre-badge">{game.genre}</span>
            <span>BY {game.studio.toUpperCase()}</span>
          </div>
          <h1>{game.name}</h1>
          <GamePrice game={game} />
          <span className="platform-badge">{game.platform}</span>
          <p>Explore the world. Become part of its story.</p>
        </div>
        <div className="banner-actions">
          <button
            className={`button secondary ${saved.includes(game.id) ? "is-saved" : ""}`}
            onClick={() => toggleSaved(game.id)}
            aria-pressed={saved.includes(game.id)}
          >
            <Icon
              name={saved.includes(game.id) ? "check" : "bookmark"}
              size={16}
            />
            {saved.includes(game.id) ? "In your collection" : "Save game"}
          </button>
          <button className="button primary" onClick={() => setPlay(true)}>
            <Icon name="play" size={16} />
            Arcade demo
          </button>
        </div>
      </section>
      <section className="game-first-intro">
        <div>
          <span className="eyebrow">A WORLD WORTH GETTING LOST IN</span>
          <h2>{game.genre}. With a little more soul.</h2>
          <p>{game.description}</p>
        </div>
        <div className="game-first-facts">
          <span>
            <b>{game.studio}</b>Independent studio
          </span>
          <span>
            <b>{game.genre}</b>Find your kind of play
          </span>
          <Link href="/packs">
            Part of a bigger adventure
            <Icon name="arrow" size={16} />
          </Link>
        </div>
      </section>
      <div className="economy-divider">
        <span>THE ECONOMY BEHIND THE GAME</span>
        <p>Curious about its asset? Explore the sample market below.</p>
        <Icon name="chart" size={20} />
      </div>
      <div className="terminal-layout" id="economy">
        <div className="terminal-main">
          <section className="price-panel">
            <div className="price-panel-heading">
              <div>
                <span className="eyebrow">
                  ${game.symbol} <span className="muted">/ USDC</span>
                </span>
                <div className="main-price">
                  <h2>{money(game.price)}</h2>
                  <Change value={game.change} />
                  <span className="muted">24h</span>
                </div>
              </div>
              <span className="market-rank">#{game.rank} IN THE ECOSYSTEM</span>
            </div>
            <div className="game-metrics">
              {[
                ["Market cap", money(game.marketCap, true)],
                ["24h volume", money(game.volume, true)],
                ["Holders", game.holders.toLocaleString()],
                ["Active players", game.players.toLocaleString()],
              ].map(([label, value]) => (
                <span key={label}>
                  {label}
                  <strong>{value}</strong>
                </span>
              ))}
            </div>
            <div className="chart-toolbar">
              <span>
                <Icon name="chart" size={15} /> Price chart
              </span>
              <div>
                {timeframes.map((t) => (
                  <button
                    key={t}
                    aria-pressed={range === t}
                    className={range === t ? "selected" : ""}
                    onClick={() => setRange(t)}
                  >
                    {t}
                  </button>
                ))}
              </div>
            </div>
            <div className="detail-chart" key={range}>
              <Chart
                data={rangeData}
                large
                positive={game.change >= 0}
                range={range}
                label={`${game.name} ${range} demo price chart. Use arrow keys to inspect samples.`}
              />
              <div className="volume-bars" aria-hidden="true">
                {rangeData.map((_, i) => (
                  <i
                    key={i}
                    style={{
                      height: `${(8 + Math.abs(Math.sin(i * 2.2 + rangeIndex)) * 23).toFixed(2)}px`,
                    }}
                  />
                ))}
              </div>
            </div>
            <div className="chart-caption">
              <span>{range} DEMO SERIES · Touch or hover to inspect</span>
              <span>
                PLEMU <Icon name="spark" size={12} />
              </span>
            </div>
          </section>
          <div className="section-tabs detail-tabs" aria-label="Game details">
            {["overview", "activity", "token"].map((t) => (
              <button
                key={t}
                className={tab === t ? "selected" : ""}
                aria-pressed={tab === t}
                onClick={() => setTab(t)}
              >
                {t === "overview"
                  ? "The game"
                  : t === "activity"
                    ? "Trade activity"
                    : "Token info"}
                {t === "activity" ? (
                  <span className="count-badge">{8 + ownTrades.length}</span>
                ) : null}
              </button>
            ))}
          </div>
          {tab === "overview" ? (
            <section className="game-about">
              <span className="eyebrow">
                A WORLD BY {game.studio.toUpperCase()}
              </span>
              <h2>There’s more beyond the screen.</h2>
              <p>{game.description}</p>
              <div className="about-tags">
                <span>{game.genre}</span>
                <span>Pixel art</span>
                <span>Independent</span>
                <span>Concept game</span>
              </div>
              <div className="about-callout">
                <Icon name="globe" size={24} />
                <div>
                  <strong>A game. A community. A shared story.</strong>
                  <p>
                    Follow the asset, save the world to your collection, and
                    explore a new relationship between games and their
                    communities.
                  </p>
                </div>
              </div>
              <button
                className="button secondary"
                onClick={() => setCommunity(true)}
              >
                Inside the community <Icon name="diagonal" size={16} />
              </button>
            </section>
          ) : tab === "token" ? (
            <section className="token-info">
              <h2>The asset behind the adventure.</h2>
              <p className="muted">
                A fictional token profile for exploring the product experience.
              </p>
              {[
                ["Token name", game.name],
                ["Symbol", `$${game.symbol}`],
                ["Network", "Solana · demo"],
                ["Sample supply", "100,000,000"],
                ["Token identifier", `DEMO_${game.symbol}`],
                ["Status", "Not deployed on-chain"],
              ].map(([key, value]) => (
                <div key={key}>
                  <span>{key}</span>
                  <strong>{value}</strong>
                  {key === "Token identifier" ? (
                    <button
                      className="icon-button"
                      aria-label="Copy demo token identifier"
                      onClick={copyToken}
                    >
                      <Icon name="copy" size={15} />
                    </button>
                  ) : null}
                </div>
              ))}
            </section>
          ) : null}
          {tab !== "token" ? (
            <section className="recent-trades">
              <div className="section-heading compact-heading">
                <h2>
                  {tab === "activity" ? "Game asset activity" : "Recent moves"}
                </h2>
                <span className="eyebrow">
                  <i className="live-dot" /> DEMO FEED
                </span>
              </div>
              <div className="trades-head">
                <span>TYPE / WALLET</span>
                <span>AMOUNT (USDC)</span>
                <span>{game.symbol}</span>
                <span>TIME</span>
              </div>
              {ownTrades.map((t) => (
                <div className="trade-row" key={t.id}>
                  <span>
                    <b className={t.side === "buy" ? "positive" : "negative"}>
                      {t.side.toUpperCase()}
                    </b>
                    <small>Your demo wallet</small>
                  </span>
                  <span>{money(t.amount)}</span>
                  <span>{t.tokens.toFixed(2)}</span>
                  <span>Local demo</span>
                </div>
              ))}
              {game.activity.map((t) => (
                <div className="trade-row" key={t.id}>
                  <span>
                    <b className={t.side === "buy" ? "positive" : "negative"}>
                      {t.side.toUpperCase()}
                    </b>
                    <small>{t.wallet}</small>
                  </span>
                  <span>{money(t.amount)}</span>
                  <span>{t.tokens.toLocaleString()}</span>
                  <span>{t.minutes}m ago</span>
                </div>
              ))}
            </section>
          ) : null}
        </div>
        <aside>
          <TradePanel game={game} />
          <div className="community-mini">
            <Icon name="users" />
            <h3>Better with your people.</h3>
            <p>
              {game.holders.toLocaleString()} sample holders.
              <br />
              One shared adventure.
            </p>
            <button className="text-link" onClick={() => setCommunity(true)}>
              Community preview
              <Icon name="diagonal" size={14} />
            </button>
          </div>
        </aside>
      </div>
      <section className="section related-games">
        <div className="section-heading">
          <div>
            <span className="eyebrow">KEEP EXPLORING</span>
            <h2>Another world awaits.</h2>
          </div>
          <Link className="text-link" href="/games">
            All games
            <Icon name="arrow" />
          </Link>
        </div>
        <div className="game-grid">
          {games
            .filter((g) => g.id !== game.id)
            .slice(0, 4)
            .map((g) => (
              <GameCard game={g} key={g.id} />
            ))}
        </div>
      </section>
      <Dialog
        open={play}
        onClose={() => setPlay(false)}
        title="The Plemu arcade cabinet"
        wide
      >
        <h2>Neon Orbit</h2>
        <p className="muted">
          An original playable arcade demo. {game.name} is a concept title and
          is not playable yet.
        </p>
        {play ? (
          <iframe
            className="arcade-frame"
            title="Neon Orbit playable arcade demo"
            src="/games/neon-orbit.html"
            sandbox="allow-scripts"
          />
        ) : null}
        <p className="wallet-help">
          Move with ← → or A/D. On touch screens, hold the arrow buttons.
        </p>
      </Dialog>
      <Dialog
        open={community}
        onClose={() => setCommunity(false)}
        title={`${game.name} / Community preview`}
      >
        <span className="pixel-star">✳</span>
        <h2>Meet the world builders.</h2>
        <p className="muted">
          A preview of the conversations around {game.name}. Discussion posting
          and social links will arrive with the live community.
        </p>
        <div className="community-post">
          <small>PINNED BY {game.studio.toUpperCase()} · DEMO</small>
          <h3>Welcome to the next chapter.</h3>
          <p>
            What would you explore first? Share your favorite corners of the
            world with the people who helped bring it to life.
          </p>
          <span>24 sample replies · 128 sample reactions</span>
        </div>
        <div className="community-post">
          <small>PLAYER SPOTLIGHT · DEMO</small>
          <h3>The little details make the world.</h3>
          <p>
            The art, the atmosphere, that feeling of finding something new. This
            is why we keep coming back.
          </p>
          <span>8 sample replies · 42 sample reactions</span>
        </div>
      </Dialog>
    </div>
  );
}
