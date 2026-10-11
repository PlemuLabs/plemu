"use client";
import { useRef, useState, useEffect } from "react";
import Link from "next/link";
import Image from "next/image";
import { games, genres, money, sortGames, type MarketSort } from "../lib/games";
import { simulateTrade } from "../lib/demo-economy";
import { Chart } from "./chart";
import { GameCard } from "./game-card";
import { GamePrice } from "./game-price";
import { Icon } from "./icons";

const world = games[0];
export function RelationshipDemo() {
  const [step, setStep] = useState(0);
  const steps = [
    {
      title: "Game",
      icon: "play",
      copy: "Start with the world: its artwork, story, genre and platform. This is Starfall Protocol, an adventure concept.",
    },
    {
      title: "Token",
      icon: "spark",
      copy: "STAR is the short name for this game's example asset. A token is a digital unit you can hold or exchange when real support is available.",
    },
    {
      title: "Community",
      icon: "users",
      copy: "Players and creators give a world its story. Saving a game and following its progress are ways to stay close. Community conversations here are previews.",
    },
    {
      title: "Market",
      icon: "chart",
      copy: "A market shows the asset's price and trading activity. Price can move up or down, independently of how much you enjoy the game.",
    },
  ];
  return (
    <div className="guide-relationship">
      <div
        className="guide-flow"
        aria-label="How a game connects to its ecosystem"
      >
        {steps.map((item, i) => (
          <button
            key={item.title}
            aria-pressed={step === i}
            onClick={() => setStep(i)}
          >
            <Icon name={item.icon} size={22} />
            <span>{item.title}</span>
            {i < 3 ? <b aria-hidden="true">→</b> : null}
          </button>
        ))}
      </div>
      <div className="guide-world-window">
        <div className="guide-world-art">
          <Image
            src={world.artwork}
            alt="Starfall Protocol's pixel-art alien world"
            fill
            sizes="(max-width: 700px) 90vw, 420px"
          />
        </div>
        <div className="guide-world-data">
          <span className="eyebrow">ONE WORLD / FOUR CONNECTIONS</span>
          <h3>{world.name}</h3>
          <GamePrice game={world} />
          <span className="platform-badge">{world.platform}</span>
          <div className="guide-teaching-copy" key={step} aria-live="polite">
            <strong>
              {String(step + 1).padStart(2, "0")} / {steps[step].title}
            </strong>
            <p>{steps[step].copy}</p>
          </div>
        </div>
      </div>
      <p className="guide-small">
        All games and quotes in this preview are fictional. A token does not
        automatically grant game access, copyright, voting rights or a share of
        a studio.
      </p>
    </div>
  );
}

export function DiscoveryDemo() {
  const [genre, setGenre] = useState("All games");
  const [index, setIndex] = useState(0);
  const filtered = games.filter(
    (game) => genre === "All games" || game.genre === genre,
  );
  const game = filtered[index % filtered.length];
  return (
    <div className="guide-discovery">
      <div className="guide-chips" aria-label="Try a game category">
        {genres.map((item) => (
          <button
            key={item}
            aria-pressed={genre === item}
            onClick={() => {
              setGenre(item);
              setIndex(0);
            }}
          >
            {item}
          </button>
        ))}
      </div>
      <div className="guide-demo-split">
        <div className="guide-example-card">
          <GameCard game={game} visual />
          <button
            className="button secondary full"
            onClick={() => setIndex((value) => value + 1)}
          >
            Show another{" "}
            {genre === "All games" ? "game" : genre.toLowerCase() + " game"}
            <Icon name="arrow" />
          </button>
        </div>
        <div className="guide-tip-list">
          <div>
            <span>01</span>
            <h3>A genre is your starting point.</h3>
            <p>
              Adventure, Action, RPG, Racing and Arcade group games by the kind
              of play they offer. Try the chips above.
            </p>
          </div>
          <div>
            <span>02</span>
            <h3>A platform tells you where to look.</h3>
            <p>
              Look for Nintendo, PlayStation, Sega, PC or Arcade on a game card.
              These are sample labels here, not verified compatibility or
              licenses.
            </p>
          </div>
          <div>
            <span>03</span>
            <h3>Follow a clue.</h3>
            <p>
              Featured games are editorial picks. Rankings offer another
              discovery route. Search finds a name or token; Games adds genre
              filters and sorting.
            </p>
          </div>
          <Link className="text-link" href="/#play-world">
            Try the PSG1: swipe its screen or tap A <Icon name="arrow" />
          </Link>
        </div>
      </div>
    </div>
  );
}

export function MarketDemo() {
  const [selection, setSelection] = useState(0);
  const [metric, setMetric] = useState("price");
  const game = selection === 0 ? games[0] : games[3];
  const metrics = [
    {
      id: "price",
      label: "Price",
      value: money(game.price),
      text: `One $${game.symbol} is shown at ${money(game.price)} in this example. The current product uses fixed sample prices, not live quotes.`,
    },
    {
      id: "change",
      label: "24h change",
      value: `${game.change >= 0 ? "+" : ""}${game.change}%`,
      text: "The percentage difference from 24 hours earlier. Green means up; a minus sign means down. It describes a past move, not what happens next.",
    },
    {
      id: "volume",
      label: "24h volume",
      value: money(game.volume, true),
      text: "The total value traded during those 24 hours. It helps describe activity; it is not a count of players or money paid to the creator.",
    },
    {
      id: "cap",
      label: "Market cap",
      value: money(game.marketCap, true),
      text: "A rough size measure: token price multiplied by circulating token supply. It is not the studio's value or a pool of money available to withdraw.",
    },
  ];
  const selected = metrics.find((item) => item.id === metric)!;
  return (
    <div className="guide-market-demo">
      <div className="guide-demo-top">
        <span className="guide-demo-label">SAMPLE MARKET / 24 HOURS</span>
        <button
          className="text-link"
          onClick={() => setSelection((value) => 1 - value)}
        >
          Show {selection === 0 ? "a falling" : "a rising"} example{" "}
          <Icon name="arrow" size={16} />
        </button>
      </div>
      <div className="guide-market-identity">
        <Image src={game.artwork} alt="" width={64} height={64} />
        <div>
          <h3>{game.name}</h3>
          <span className="platform-badge">{game.platform}</span>
        </div>
        <GamePrice game={game} />
      </div>
      <div className="guide-metric-buttons" aria-label="Learn a market metric">
        {metrics.map((item) => (
          <button
            key={item.id}
            aria-pressed={metric === item.id}
            onClick={() => setMetric(item.id)}
          >
            <span>{item.label}</span>
            <strong>{item.value}</strong>
          </button>
        ))}
      </div>
      <p className="guide-metric-explanation" aria-live="polite">
        <strong>{selected.label} / </strong>
        {selected.text}
      </p>
      <div className="guide-chart">
        <Chart
          key={game.id}
          data={game.chart}
          positive={game.change >= 0}
          large
          label={`${game.name} sample chart. Touch or use left and right arrows to inspect prices.`}
        />
      </div>
      <p className="guide-small">
        Touch the chart or focus it and use ← → to inspect a sample. The line
        shows example price history; recent trades show individual buys and
        sells.
      </p>
      <div className="guide-inline-note">
        <Icon name="globe" />
        <p>
          <strong>Keep the game in the picture.</strong> Artwork, title, token,
          price and activity belong to the same world. A rising chart alone
          tells you little about the game.
        </p>
      </div>
    </div>
  );
}

export function PracticeTrade() {
  const [side, setSide] = useState<"buy" | "sell">("buy");
  const [amount, setAmount] = useState("10");
  const [stage, setStage] = useState<"edit" | "review" | "done">("edit");
  const [error, setError] = useState("");
  const focus = useRef<HTMLHeadingElement>(null);
  const amountField = useRef<HTMLInputElement>(null);
  const practiced = useRef(false);
  useEffect(() => {
    if (stage !== "edit") {
      focus.current?.focus({ preventScroll: true });
      practiced.current = true;
    } else if (practiced.current)
      amountField.current?.focus({ preventScroll: true });
  }, [stage]);
  const quote = simulateTrade(
    { balance: 100, holdings: { [world.id]: 500 }, trades: [] },
    world,
    side,
    Number(amount),
    true,
    { id: "guide-practice", time: "practice" },
  );
  const number = new Intl.NumberFormat("en-US", { maximumFractionDigits: 4 });
  return (
    <div className="guide-practice">
      <span className="guide-demo-label">
        PRACTICE ONLY / NO WALLET REQUIRED
      </span>
      <div className="guide-practice-steps" aria-label="Practice stages">
        {["Choose", "Review", "Finish"].map((label, i) => (
          <span
            key={label}
            aria-current={
              i === ["edit", "review", "done"].indexOf(stage)
                ? "step"
                : undefined
            }
          >
            {i + 1} · {label}
          </span>
        ))}
      </div>
      <div className="guide-practice-world">
        <h3>{world.name}</h3>
        <GamePrice game={world} />
      </div>
      {stage === "edit" ? (
        <form
          onSubmit={(event) => {
            event.preventDefault();
            if (!quote.ok) {
              setError(quote.message);
              return;
            }
            setError("");
            setStage("review");
          }}
        >
          <div className="guide-chips" aria-label="Practice trade direction">
            <button
              type="button"
              aria-pressed={side === "buy"}
              onClick={() => {
                setSide("buy");
                setError("");
              }}
            >
              Buy
            </button>
            <button
              type="button"
              aria-pressed={side === "sell"}
              onClick={() => {
                setSide("sell");
                setError("");
              }}
            >
              Sell
            </button>
          </div>
          <label className="guide-amount">
            {side === "buy"
              ? "Practice amount to spend"
              : "Practice value to sell"}
            <span>
              <input
                ref={amountField}
                aria-describedby="guide-trade-units"
                type="number"
                inputMode="decimal"
                step="0.01"
                min="0.01"
                value={amount}
                onChange={(event) => {
                  setAmount(event.target.value);
                  setError("");
                }}
                required
              />
              <b>USDC</b>
            </span>
          </label>
          <p id="guide-trade-units" className="guide-small">
            USDC is the dollar-based unit used in this example. Your practice
            budget is 100 demo USDC and 500 STAR. Nothing is connected to your
            product wallet.
          </p>
          {error ? (
            <p role="alert" className="guide-error">
              {error}
            </p>
          ) : null}
          <button className="button primary" type="submit">
            Review practice trade <Icon name="arrow" />
          </button>
        </form>
      ) : quote.ok ? (
        <div className="guide-practice-review">
          <h3 tabIndex={-1} ref={focus}>
            {stage === "done"
              ? "Practice complete. Nothing was sent."
              : "Check both sides of the trade."}
          </h3>
          <dl>
            <div>
              <dt>{side === "buy" ? "You spend" : "You receive"}</dt>
              <dd>{money(Number(amount))} demo USDC</dd>
            </div>
            <div>
              <dt>{side === "buy" ? "You receive" : "You sell"}</dt>
              <dd>{number.format(quote.trade.tokens)} STAR</dd>
            </div>
            <div>
              <dt>Example rate</dt>
              <dd>{money(world.price)} per STAR</dd>
            </div>
          </dl>
          <p>
            {stage === "done"
              ? "Your real assets, demo portfolio and Library are unchanged. You can repeat this lesson as often as you like."
              : "The amount and asset should match what you intended. This fixed-price exercise includes no real fees or price movement."}
          </p>
          <div className="button-row">
            {stage === "review" ? (
              <>
                <button
                  className="button secondary"
                  onClick={() => setStage("edit")}
                >
                  Edit amount
                </button>
                <button
                  className="button primary"
                  onClick={() => setStage("done")}
                >
                  Finish practice <Icon name="check" />
                </button>
              </>
            ) : (
              <button
                className="button secondary"
                onClick={() => {
                  setStage("edit");
                  setAmount("10");
                }}
              >
                Try again <Icon name="arrow" />
              </button>
            )}
          </div>
        </div>
      ) : null}
    </div>
  );
}

const rankingSignals: { id: MarketSort; label: string; explanation: string }[] =
  [
    {
      id: "trending",
      label: "Trending",
      explanation:
        "The preset discovery order in this preview. A live trending score is not connected yet.",
    },
    {
      id: "volume",
      label: "Most traded",
      explanation:
        "Highest total trading value over 24 hours. This is the same signal as Highest volume, not the largest number of individual trades.",
    },
    {
      id: "gainers",
      label: "Biggest gainers",
      explanation:
        "The largest percentage rises over 24 hours. A past rise does not promise another one.",
    },
    {
      id: "losers",
      label: "Biggest losers",
      explanation:
        "The largest percentage falls over 24 hours. Price movement is only one part of a game's story.",
    },
    {
      id: "popular",
      label: "Most popular",
      explanation:
        "The most active players in our sample data. Player interest and token price are different signals.",
    },
    {
      id: "new",
      label: "Rising games",
      explanation:
        "The newest additions to this demo catalog. On this prototype, this label sorts by arrival date, not price growth.",
    },
  ];
export function RankingDemo() {
  const [signal, setSignal] = useState<MarketSort>("popular");
  const active = rankingSignals.find((item) => item.id === signal)!;
  return (
    <div>
      <label className="guide-select-label">
        Choose a discovery signal
        <select
          value={signal}
          onChange={(event) => setSignal(event.target.value as MarketSort)}
        >
          {rankingSignals.map((item) => (
            <option key={item.id} value={item.id}>
              {item.label}
            </option>
          ))}
        </select>
      </label>
      <p className="guide-signal-copy" aria-live="polite">
        {active.explanation}
      </p>
      <ol className="guide-ranking-list">
        {sortGames(games, signal)
          .slice(0, 3)
          .map((game, i) => (
            <li key={game.id}>
              <span className="guide-rank">0{i + 1}</span>
              <Image src={game.artwork} alt="" width={60} height={60} />
              <div>
                <Link href={`/game/${game.id}`}>
                  {game.name} <Icon name="diagonal" size={14} />
                </Link>
                <GamePrice game={game} compact />
                <small>
                  {signal === "volume"
                    ? `${money(game.volume, true)} traded`
                    : signal === "popular"
                      ? `${game.players.toLocaleString()} sample players`
                      : game.platform}
                </small>
              </div>
            </li>
          ))}
      </ol>
      <p className="guide-small">
        Every signal here uses fictional data. Rankings are discovery tools, not
        recommendations to buy.
      </p>
    </div>
  );
}
