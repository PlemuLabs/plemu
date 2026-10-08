"use client";
import { useState } from "react";
import Link from "next/link";
import Image from "next/image";
import { games, money, sortGames, type MarketSort } from "../lib/games";
import { MarketRows, Change } from "./game-card";
import { Icon } from "./icons";
const categories: [MarketSort, string][] = [
  ["trending", "Trending"],
  ["volume", "Most traded"],
  ["gainers", "Biggest gainers"],
  ["losers", "Biggest losers"],
  ["popular", "Most popular"],
  ["new", "Rising games"],
];
export function RankingsExperience() {
  const [category, setCategory] = useState<MarketSort>("trending");
  const ranked = sortGames(games, category);
  return (
    <div className="page-container inner-page">
      <div className="page-title">
        <div>
          <span className="eyebrow">
            <Icon name="trophy" size={14} /> THE LEADERBOARD
          </span>
          <h1>
            Legends in the <em>making.</em>
          </h1>
          <p>
            The crowd has good taste. Meet the worlds everyone’s talking about.
          </p>
        </div>
        <span className="version-tag">24H / DEMO RANKINGS</span>
      </div>
      <div className="section-tabs ranking-tabs">
        {categories.map(([id, label]) => (
          <button
            key={id}
            className={category === id ? "selected" : ""}
            aria-pressed={category === id}
            onClick={() => setCategory(id)}
          >
            {label}
          </button>
        ))}
      </div>
      <div className="podium" key={category}>
        {[ranked[1], ranked[0], ranked[2]].map((g, i) => {
          const rank = i === 0 ? 2 : i === 1 ? 1 : 3;
          return (
            <Link
              key={g.id}
              className={`podium-card place-${rank}`}
              href={`/game/${g.id}`}
            >
              <Image
                src={g.artwork}
                alt={`${g.name} artwork`}
                fill
                sizes="(max-width: 700px) 90vw, 40vw"
              />
              <div className="podium-shade" />
              <span className="podium-number">0{rank}</span>
              <span className="podium-caption">
                {rank === 1
                  ? "THE HIGH SCORE"
                  : "PLAYER " + String(rank).padStart(2, "0")}
              </span>
              <div className="podium-info">
                <small>
                  ${g.symbol} · {g.genre}
                </small>
                <h2>{g.name}</h2>
                <div>
                  <strong>{money(g.price)}</strong>
                  <Change value={g.change} />
                </div>
                <span className="platform-badge">{g.platform}</span>
                {category === "volume" ? (
                  <small>24h volume {money(g.volume, true)}</small>
                ) : category === "popular" ? (
                  <small>{g.players.toLocaleString()} players</small>
                ) : null}
              </div>
              <Icon name="diagonal" size={22} />
            </Link>
          );
        })}
      </div>
      <div className="section-heading compact-heading">
        <h2>The full leaderboard</h2>
        <span className="eyebrow">12 GAME ASSETS</span>
      </div>
      <MarketRows items={ranked} />
    </div>
  );
}
