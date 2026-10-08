"use client";
import Link from "next/link";
import { GamePrice } from "./game-price";
import Image from "next/image";
import { useState } from "react";
import { games, genres, sortGames } from "../lib/games";
import { GameCard } from "./game-card";
import { SectionHeading } from "./section-heading";
import { Icon } from "./icons";
import { useProduct } from "./product-provider";
import { packs } from "../lib/packs";
import { CartridgeBrowser } from "./cartridge";
export function LibraryExperience({
  collection = false,
}: {
  collection?: boolean;
}) {
  const [tab, setTab] = useState(collection ? "saved" : "discover");
  const [genre, setGenre] = useState("All games");
  const [query, setQuery] = useState("");
  const [order, setOrder] = useState<"trending" | "new" | "popular">(
    "trending",
  );
  const { saved, collectedPacks } = useProduct();
  const visible = games.filter(
    (g) =>
      (tab !== "saved" || saved.includes(g.id)) &&
      (genre === "All games" || genre === g.genre) &&
      g.name.toLowerCase().includes(query.toLowerCase()),
  );
  return (
    <div className="page-container inner-page">
      <div className="page-title">
        <div>
          <span className="eyebrow">A LITTLE ESCAPE. A LOT TO DISCOVER.</span>
          <h1>
            {collection ? (
              <>
                Your worlds.
                <br />
                <em>Your happy place.</em>
              </>
            ) : (
              <>
                A good day for
                <br />
                <em>a new adventure.</em>
              </>
            )}
          </h1>
          <p>
            For the explorers, the one-more-runners, and everyone in between.
          </p>
        </div>
        <span className="version-tag">
          {collection ? "YOUR COLLECTION" : "THE GAME ROOM"}
        </span>
      </div>
      <nav className="game-categories" aria-label="Game categories">
        {genres.map((category, i) => (
          <button
            key={category}
            aria-pressed={genre === category}
            onClick={() => {
              setGenre(category);
              document.getElementById("library-games")?.scrollIntoView({
                behavior: window.matchMedia("(prefers-reduced-motion: reduce)")
                  .matches
                  ? "instant"
                  : "smooth",
                block: "start",
              });
            }}
          >
            <span aria-hidden="true">{["✳", "↗", "✦", "▣", "⚑", "◇"][i]}</span>
            {category}
            <small>
              {category === "All games"
                ? games.length
                : games.filter((g) => g.genre === category).length}
            </small>
          </button>
        ))}
      </nav>
      <div className="section-tabs">
        <button
          className={tab === "discover" ? "selected" : ""}
          onClick={() => setTab("discover")}
        >
          Discover
        </button>
        <button
          className={tab === "saved" ? "selected" : ""}
          onClick={() => setTab("saved")}
        >
          <Icon name="bookmark" size={14} />
          Your collection <span className="count-badge">{saved.length}</span>
        </button>
      </div>
      {collection && collectedPacks.length > 0 ? (
        <div className="collected-pack-shelf">
          <span className="eyebrow">YOUR PACKS</span>
          {packs
            .filter((p) => collectedPacks.includes(p.id))
            .map((p) => (
              <Link key={p.id} href="/packs">
                <Icon name="grid" size={20} />
                <span>
                  {p.name}
                  <small>{p.gameIds.length} curated worlds</small>
                </span>
                <Icon name="check" size={16} />
              </Link>
            ))}
        </div>
      ) : null}
      {tab === "discover" ? (
        <>
          {!collection ? <CartridgeBrowser /> : null}
          <Link href="/game/moss-and-ember" className="library-feature">
            <Image
              src="/art/moss.webp"
              alt="An ancient forest temple in Moss & Ember"
              fill
              sizes="100vw"
            />
            <div className="library-feature-shade" />
            <div>
              <span className="eyebrow">
                EDITOR'S PICK / A WORLD TO GET LOST IN
              </span>
              <h2>
                Follow the light.
                <br />
                Find something <em>wonderful.</em>
              </h2>
              <p>
                A forest full of stories. An adventure at your own pace.
                <br />
                Step inside Moss & Ember.
              </p>
              <GamePrice game={games.find((g) => g.id === "moss-and-ember")!} />
              <span className="button primary">
                Explore the game <Icon name="arrow" />
              </span>
            </div>
            <span className="library-feature-title">
              MOSS &<br />
              EMBER
            </span>
          </Link>
          <div className="genre-collections">
            {[
              { genre: "Adventure", text: "Beyond the map", icon: "globe" },
              { genre: "Action", text: "One more run", icon: "spark" },
              { genre: "Racing", text: "Chase the horizon", icon: "arrow" },
              { genre: "RPG", text: "Write your story", icon: "bookmark" },
            ].map((c) => (
              <button
                key={c.genre}
                aria-pressed={genre === c.genre}
                onClick={() => {
                  setGenre(c.genre);
                  document.getElementById("library-games")?.scrollIntoView({
                    behavior: window.matchMedia(
                      "(prefers-reduced-motion: reduce)",
                    ).matches
                      ? "instant"
                      : "smooth",
                    block: "start",
                  });
                }}
              >
                <Icon name={c.icon} size={24} />
                <span>
                  {c.text}
                  <small>{c.genre}</small>
                </span>
                <Icon name="diagonal" size={16} />
              </button>
            ))}
          </div>
        </>
      ) : null}
      <section className="section" id="library-games">
        <SectionHeading
          eyebrow={tab === "saved" ? "CURATED BY YOU" : "PICK YOUR ADVENTURE"}
          title={
            tab === "saved"
              ? "Your worlds, together."
              : "Good games. Great possibilities."
          }
        />
        <div className="market-toolbar library-toolbar">
          <label className="sort-select">
            <span>Browse</span>
            <select
              aria-label="Sort games"
              value={order}
              onChange={(e) => setOrder(e.target.value as typeof order)}
            >
              <option value="trending">Trending</option>
              <option value="new">Recently added</option>
              <option value="popular">Community favorites</option>
            </select>
          </label>
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
          </div>
          <label className="search-field">
            <Icon name="search" />
            <input
              aria-label="Search library"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="Find a game"
            />
          </label>
        </div>
        {visible.length ? (
          <div className="game-grid library-grid">
            {sortGames(visible, order).map((g) => (
              <GameCard key={g.id} game={g} visual />
            ))}
          </div>
        ) : (
          <div className="empty-state">
            <Icon name="bookmark" size={32} />
            <h2>
              {tab === "saved" && !saved.length
                ? "Your story starts here."
                : "No games match these filters."}
            </h2>
            <p>
              {tab === "saved" && !saved.length
                ? "Save a game with its bookmark button to start your collection."
                : "Try another genre or clear the search."}
            </p>
            <button
              className="button primary"
              onClick={() => {
                setTab("discover");
                setGenre("All games");
                setQuery("");
              }}
            >
              Discover games <Icon name="arrow" />
            </button>
          </div>
        )}
        <p className="market-disclosure">
          Saved games are a local collection. They do not grant ownership or
          game access. Concept titles are not playable.
        </p>
      </section>
    </div>
  );
}
