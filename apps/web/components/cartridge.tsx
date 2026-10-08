"use client";
import Image from "next/image";
import Link from "next/link";
import { GamePrice } from "./game-price";
import { useRef, useState, type CSSProperties } from "react";
import { type Game, games } from "../lib/games";
import { Icon } from "./icons";

export function Cartridge({
  game,
  eager = false,
}: {
  game: Game;
  eager?: boolean;
}) {
  return (
    <div
      className="cartridge"
      style={{ "--cart-color": game.color } as CSSProperties}
    >
      <div className="cartridge-ridge">
        <span>PLEMU®</span>
        <i />
        <i />
        <i />
      </div>
      <div className="cartridge-label">
        <Image
          src={game.artwork}
          alt={`${game.name} cartridge artwork`}
          fill
          sizes="(max-width: 700px) 65vw, 340px"
          loading={eager ? "eager" : "lazy"}
          draggable={false}
        />
        <div className="cartridge-label-shade" />
        <span className="cartridge-genre">
          {game.platform} / {String(game.rank).padStart(2, "0")}
        </span>
        <strong>{game.name}</strong>
      </div>
      <div className="cartridge-bottom">
        <GamePrice game={game} compact />
      </div>
      <div className="cartridge-contacts" />
    </div>
  );
}
export function CartridgeBrowser({
  items = games.slice(0, 7),
  compact = false,
}: {
  items?: Game[];
  compact?: boolean;
}) {
  const [index, setIndex] = useState(2);
  const selected = Math.min(index, items.length - 1);
  const start = useRef<{
    id: number;
    x: number;
    y: number;
    dx: number;
    locked: boolean;
  } | null>(null);
  const dragged = useRef(false);
  const move = (delta: number) =>
    setIndex((i) => (i + delta + items.length) % items.length);
  const game = items[selected];
  return (
    <div className={`cartridge-browser ${compact ? "compact" : ""}`}>
      <div
        className="cartridge-track"
        role="region"
        aria-roledescription="carousel"
        aria-label="Game cartridges"
        tabIndex={0}
        onKeyDown={(e) => {
          if (e.key === "ArrowRight" || e.key === "ArrowLeft") {
            e.preventDefault();
            move(e.key === "ArrowRight" ? 1 : -1);
          }
        }}
        onPointerDown={(e) => {
          if (!e.isPrimary || e.button !== 0) return;
          start.current = {
            id: e.pointerId,
            x: e.clientX,
            y: e.clientY,
            dx: 0,
            locked: false,
          };
          dragged.current = false;
        }}
        onPointerMove={(e) => {
          const d = start.current;
          if (!d || d.id !== e.pointerId) return;
          const dx = e.clientX - d.x,
            dy = Math.abs(e.clientY - d.y);
          if (!d.locked && dy > 10 && dy > Math.abs(dx)) {
            start.current = null;
            dragged.current = true;
            return;
          }
          if (!d.locked && Math.abs(dx) < 8) return;
          d.locked = true;
          d.dx = dx;
          dragged.current = true;
          e.currentTarget.setPointerCapture(e.pointerId);
        }}
        onPointerUp={(e) => {
          const d = start.current;
          if (d && d.id === e.pointerId && d.locked && Math.abs(d.dx) >= 36)
            move(d.dx < 0 ? 1 : -1);
          start.current = null;
          if (e.currentTarget.hasPointerCapture(e.pointerId))
            e.currentTarget.releasePointerCapture(e.pointerId);
        }}
        onPointerCancel={() => {
          start.current = null;
          dragged.current = true;
        }}
        onLostPointerCapture={() => {
          start.current = null;
        }}
      >
        {items.map((g, i) => {
          let offset = i - selected;
          if (offset > items.length / 2) offset -= items.length;
          if (offset < -items.length / 2) offset += items.length;
          return (
            <button
              key={g.id}
              className={`cartridge-slot ${i === selected ? "selected" : ""}`}
              style={
                {
                  "--offset": offset,
                  "--distance": Math.abs(offset),
                  zIndex: items.length - Math.abs(offset),
                  visibility: Math.abs(offset) > 2 ? "hidden" : "visible",
                } as CSSProperties
              }
              aria-label={`Select ${g.name}`}
              aria-pressed={i === selected}
              tabIndex={Math.abs(offset) > 2 ? -1 : 0}
              onClick={() => {
                if (!dragged.current) setIndex(i);
              }}
            >
              <Cartridge game={g} eager={compact && i === selected} />
            </button>
          );
        })}
      </div>
      <div className="cartridge-caption">
        <button
          className="round-control"
          aria-label="Previous cartridge"
          onClick={() => move(-1)}
        >
          <Icon name="back" />
        </button>
        <div aria-live="polite">
          <span className="eyebrow">
            {String(selected + 1).padStart(2, "0")} /{" "}
            {String(items.length).padStart(2, "0")} · {game.genre}
          </span>
          <Link href={`/game/${game.id}`}>
            {game.name} <Icon name="diagonal" size={18} />
          </Link>
          <GamePrice game={game} />
        </div>
        <button
          className="round-control"
          aria-label="Next cartridge"
          onClick={() => move(1)}
        >
          <Icon name="arrow" />
        </button>
      </div>
      <p className="interaction-hint">
        Drag to browse · Arrow keys work too · Select a title to explore
      </p>
    </div>
  );
}
