"use client";
import Link from "next/link";
import { GamePrice } from "./game-price";
import Image from "next/image";
import { type Game, money } from "../lib/games";
import { Chart } from "./chart";
import { Icon } from "./icons";
import { useProduct } from "./product-provider";
export function Change({ value }: { value: number }) {
  return (
    <span className={value >= 0 ? "positive" : "negative"}>
      {value >= 0 ? "↗" : "↘"} {Math.abs(value).toFixed(2)}%
    </span>
  );
}
export function GameCard({
  game,
  visual = false,
}: {
  game: Game;
  visual?: boolean;
}) {
  const { saved, toggleSaved } = useProduct();
  return (
    <article
      className={`game-card ${visual ? "visual-card" : ""}`}
      style={{ "--game-color": game.color } as React.CSSProperties}
    >
      <Link
        className="card-art"
        href={`/game/${game.id}`}
        aria-label={`Explore ${game.name}`}
      >
        <Image
          src={game.artwork}
          alt={`${game.name} pixel art world`}
          fill
          sizes="(max-width: 700px) 90vw, (max-width: 1100px) 45vw, 25vw"
        />
        <span className="art-shade" />
        <span className="genre-badge">{game.genre}</span>
        <span className="card-open">
          <Icon name="diagonal" />
        </span>
        {visual ? (
          <div className="visual-title">
            <small>{game.studio.toUpperCase()}</small>
            <h3>{game.name}</h3>
          </div>
        ) : null}
      </Link>
      <button
        className={`save-button ${saved.includes(game.id) ? "saved" : ""}`}
        aria-label={`${saved.includes(game.id) ? "Remove" : "Save"} ${game.name}`}
        aria-pressed={saved.includes(game.id)}
        onClick={() => toggleSaved(game.id)}
      >
        <Icon name="bookmark" size={16} />
      </button>
      {visual ? (
        <div className="game-discovery-info">
          <GamePrice game={game} />
          <span className="platform-badge">{game.platform}</span>
          <p>{game.description.split(". ")[0]}.</p>
          <Link href={`/game/${game.id}`}>
            Explore world <Icon name="arrow" size={15} />
          </Link>
        </div>
      ) : (
        <div className="card-info">
          <div className="card-name">
            <Link href={`/game/${game.id}`}>{game.name}</Link>
            <span>${game.symbol}</span>
          </div>
          <div className="card-price">
            <strong>{money(game.price)}</strong>
            <Change value={game.change} />
          </div>
          <span className="platform-badge">{game.platform}</span>
          <Chart
            data={game.chart}
            positive={game.change >= 0}
            label={`${game.name} demo price trend`}
          />
          <div className="card-foot">
            <span>
              MCAP <b>{money(game.marketCap, true)}</b>
            </span>
            <span>
              VOL <b>{money(game.volume, true)}</b>
            </span>
          </div>
        </div>
      )}
    </article>
  );
}
export function MarketRows({ items }: { items: Game[] }) {
  return (
    <div className="market-table">
      <div className="market-table-head">
        <span># / GAME ASSET</span>
        <span>PRICE</span>
        <span>24H CHANGE</span>
        <span>MARKET CAP</span>
        <span>VOLUME (24H)</span>
        <span>LAST 24H</span>
      </div>
      {items.map((g, i) => (
        <Link className="market-row" key={g.id} href={`/game/${g.id}`}>
          <div className="row-game">
            <span className="row-rank">{String(i + 1).padStart(2, "0")}</span>
            <Image src={g.artwork} alt="" width={44} height={44} />
            <div>
              <strong>{g.name}</strong>
              <small>
                ${g.symbol} · {g.platform}
              </small>
            </div>
          </div>
          <strong className="row-price">{money(g.price)}</strong>
          <Change value={g.change} />
          <span className="row-cap">
            <small>Market cap</small>
            {money(g.marketCap, true)}
          </span>
          <span className="row-volume">
            <small>24h volume</small>
            {money(g.volume, true)}
          </span>
          <Chart data={g.chart} positive={g.change >= 0} />
        </Link>
      ))}
    </div>
  );
}
