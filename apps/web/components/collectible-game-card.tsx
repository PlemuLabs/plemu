"use client";
import Image from "next/image";
import Link from "next/link";
import { type Game } from "../lib/games";
import { GamePrice } from "./game-price";

// Price leads; player count resolves ties without implying random rarity.
export function packHit(games: Game[]) {
  return [...games].sort(
    (a, b) => b.price - a.price || b.players - a.players || a.rank - b.rank,
  )[0];
}
export function CollectibleGameCard({
  game,
  hit = false,
}: {
  game: Game;
  hit?: boolean;
}) {
  return (
    <Link
      className={`collectible-game-card ${hit ? "is-hit" : ""}`}
      onPointerMove={(e) => {
        if (
          !hit ||
          e.pointerType === "touch" ||
          window.matchMedia("(prefers-reduced-motion: reduce)").matches
        )
          return;
        const r = e.currentTarget.getBoundingClientRect();
        e.currentTarget.style.setProperty(
          "--foil-x",
          `${((e.clientX - r.left) / r.width) * 100}%`,
        );
        e.currentTarget.style.setProperty(
          "--foil-y",
          `${((e.clientY - r.top) / r.height) * 100}%`,
        );
      }}
      onPointerLeave={(e) => {
        e.currentTarget.style.removeProperty("--foil-x");
        e.currentTarget.style.removeProperty("--foil-y");
      }}
      href={`/game/${game.id}`}
      aria-label={`Explore ${game.name} collectible card`}
    >
      <div className="collectible-card-inner">
        <header>
          <span>PLEMU · WORLD CARD</span>
          <span className="card-hit-label">{hit ? "✦ HIT" : "◇"}</span>
        </header>
        <h3>{game.name}</h3>
        <div className="collectible-card-art">
          <Image
            src={game.artwork}
            alt={`${game.name} world`}
            fill
            sizes="280px"
            draggable={false}
          />
        </div>
        <div className="collectible-card-type">
          <span>{game.genre}</span>
          <span className="platform-badge">{game.platform}</span>
        </div>
        <div className="collectible-card-quote">
          <GamePrice game={game} compact />
        </div>
        <p>{game.description.split(".")[0]}.</p>
        <footer>
          <span>PLEMU COLLECTION</span>
          <span>
            {String(game.rank).padStart(3, "0")} / 012{" "}
            <b aria-hidden="true">↗</b>
          </span>
        </footer>
      </div>
    </Link>
  );
}
