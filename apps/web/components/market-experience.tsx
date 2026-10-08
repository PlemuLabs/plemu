"use client";
import { useState } from "react";
import Link from "next/link";
import { games, genres, money, sortGames, type MarketSort } from "../lib/games";
import { GameCard, MarketRows, Change } from "./game-card";
import { Icon } from "./icons";
import { Chart } from "./chart";
const sorts: [MarketSort, string][] = [
  ["trending", "Trending"],
  ["gainers", "Top gainers"],
  ["losers", "Top losers"],
  ["volume", "Highest volume"],
  ["cap", "Market cap"],
  ["new", "Recently added"],
];
export function MarketExperience() {
  const [query, setQuery] = useState("");
  const [genre, setGenre] = useState("All games");
  const [sort, setSort] = useState<MarketSort>("trending");
  const [view, setView] = useState<"grid" | "list">("grid");
  const visible = sortGames(
    games.filter(
      (g) =>
        (genre === "All games" || g.genre === genre) &&
        `${g.name} ${g.symbol}`.toLowerCase().includes(query.toLowerCase()),
    ),
    sort,
  );
  return (
    <div className="page-container inner-page market-page">
      <div className="page-title">
        <div>
          <span className="eyebrow">
            <i className="live-dot" /> THE ARCADE EXCHANGE
          </span>
          <h1>
            Good games.
            <br />
            <em>New possibilities.</em>
          </h1>
          <p>
            The game market. Follow the worlds you love, one move at a time.
          </p>
        </div>
        <span className="version-tag">MARKET / V0 DEMO</span>
      </div>
      <div className="market-highlights">
        {[
          ["The front runner", sortGames(games, "volume")[0]],
          ["Making moves", sortGames(games, "gainers")[0]],
          ["New to the arcade", sortGames(games, "new")[0]],
        ].map(([label, item]) => {
          const g = item as (typeof games)[number];
          return (
            <Link
              key={label as string}
              href={`/game/${g.id}`}
              className="market-highlight"
            >
              <span className="eyebrow">
                {label as string}
                <Icon name="diagonal" size={14} />
              </span>
              <div>
                <strong>
                  {g.name}
                  <small>${g.symbol}</small>
                </strong>
                <span>
                  {money(g.price)}
                  <Change value={g.change} />
                </span>
              </div>
              <Chart data={g.chart} />
            </Link>
          );
        })}
      </div>
      <div className="market-toolbar">
        <label className="search-field">
          <Icon name="search" />
          <input
            aria-label="Search market"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Search games or tokens"
          />
        </label>
        <label className="sort-select">
          <span>Sort by</span>
          <select
            aria-label="Sort market"
            value={sort}
            onChange={(e) => setSort(e.target.value as MarketSort)}
          >
            {sorts.map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
          <Icon name="chevron" size={14} />
        </label>
        <div className="view-switch" aria-label="Market layout">
          <button
            aria-label="Grid view"
            aria-pressed={view === "grid"}
            className={view === "grid" ? "selected" : ""}
            onClick={() => setView("grid")}
          >
            <Icon name="grid" />
          </button>
          <button
            aria-label="List view"
            aria-pressed={view === "list"}
            className={view === "list" ? "selected" : ""}
            onClick={() => setView("list")}
          >
            <Icon name="list" />
          </button>
        </div>
      </div>
      <div className="filter-bar">
        {genres.map((g) => (
          <button
            key={g}
            className={genre === g ? "selected" : ""}
            aria-pressed={genre === g}
            onClick={() => setGenre(g)}
          >
            {g}
          </button>
        ))}
        <span>{visible.length} assets</span>
      </div>
      <div className="market-results" key={view + genre + sort}>
        {visible.length ? (
          view === "grid" ? (
            <div className="game-grid">
              {visible.map((g) => (
                <GameCard key={g.id} game={g} />
              ))}
            </div>
          ) : (
            <MarketRows items={visible} />
          )
        ) : (
          <div className="empty-state">
            <Icon name="search" size={32} />
            <h2>No assets found.</h2>
            <p>Try another name, token, or genre.</p>
            <button
              className="button secondary"
              onClick={() => {
                setQuery("");
                setGenre("All games");
              }}
            >
              Reset filters
            </button>
          </div>
        )}
      </div>
      <p className="market-disclosure">
        All game assets, prices, volumes, and activity are fictional
        demonstration data.
      </p>
    </div>
  );
}
