"use client";
import Image from "next/image";
import Link from "next/link";
import { ConsoleDepth } from "./console-depth";
import { GamePrice } from "./game-price";
import { useRef, useState, type CSSProperties } from "react";
import { games, genres } from "../lib/games";
import { Chart } from "./chart";
import { Icon } from "./icons";
import { useProduct } from "./product-provider";

export function Psg1Experience() {
  const [index, setIndex] = useState(0);
  const [category, setCategory] = useState(0);
  const [mode, setMode] = useState<"game" | "asset">("game");
  const [info, setInfo] = useState(false);
  const { saved, toggleSaved } = useProduct();
  const swipe = useRef<{
    id: number;
    x: number;
    y: number;
    dx: number;
    locked: boolean;
  } | null>(null);
  const resetTouch = (node: HTMLElement) => {
    swipe.current = null;
    const depth = node.closest<HTMLElement>(".console-depth");
    depth?.style.removeProperty("--touch-x");
    depth?.style.removeProperty("--touch-tilt");
  };
  const available = games.filter(
    (g) => category === 0 || g.genre === genres[category],
  );
  const game = available[index % available.length];
  const move = (delta: number) => {
    setIndex((i) => (i + delta + available.length) % available.length);
    setInfo(false);
  };
  const changeGenre = (delta: number) => {
    setCategory((c) => (c + delta + genres.length) % genres.length);
    setIndex(0);
    setInfo(false);
  };
  return (
    <section className="psg-story" id="play-world">
      <div className="psg-stage">
        <div className="psg-title">
          <span className="eyebrow">02 / GET YOUR HANDS ON A NEW WORLD</span>
          <h2>
            Less scrolling.
            <br />
            <span className="pixel-text">More playing.</span>
          </h2>
          <p>
            Meet your next favorite through the PSG1.
            <br />
            Yes, the buttons actually work.
          </p>
        </div>
        <div className="psg-layout">
          <div className="console-scene">
            <span className="console-orbit" aria-hidden="true" />
            <span className="console-annotation">PICK UP & EXPLORE ↘</span>
            <ConsoleDepth>
              <div
                className="psg-console"
                tabIndex={0}
                role="group"
                aria-label="Interactive PSG1 game browser"
                onKeyDown={(e) => {
                  if ((e.target as HTMLElement).closest("a")) return;
                  if (
                    (e.target as HTMLElement).tagName === "BUTTON" &&
                    (e.key === "Enter" || e.key === " ")
                  )
                    return;
                  if (
                    [
                      "ArrowRight",
                      "ArrowLeft",
                      "ArrowUp",
                      "ArrowDown",
                      "a",
                      "A",
                      "b",
                      "B",
                      "Enter",
                      "Escape",
                    ].includes(e.key)
                  ) {
                    e.preventDefault();
                    if (e.key === "ArrowRight" || e.key === "ArrowDown")
                      move(1);
                    else if (e.key === "ArrowLeft" || e.key === "ArrowUp")
                      move(-1);
                    else if (
                      e.key === "b" ||
                      e.key === "B" ||
                      e.key === "Escape"
                    ) {
                      setInfo(false);
                      setMode("game");
                    } else setInfo((v) => !v);
                  }
                }}
              >
                <div className="shoulder-controls">
                  <button
                    onClick={() => changeGenre(-1)}
                    aria-label="Previous console genre"
                  >
                    L
                  </button>
                  <span>PLEMU × PSG1</span>
                  <button
                    onClick={() => changeGenre(1)}
                    aria-label="Next console genre"
                  >
                    R
                  </button>
                </div>
                <div className="console-bezel">
                  <div
                    className="console-display"
                    onPointerDown={(e) => {
                      if (
                        !e.isPrimary ||
                        e.button !== 0 ||
                        (e.target as HTMLElement).closest("a")
                      )
                        return;
                      swipe.current = {
                        id: e.pointerId,
                        x: e.clientX,
                        y: e.clientY,
                        dx: 0,
                        locked: false,
                      };
                    }}
                    onPointerMove={(e) => {
                      const d = swipe.current;
                      if (!d || d.id !== e.pointerId) return;
                      const dx = e.clientX - d.x,
                        dy = Math.abs(e.clientY - d.y);
                      if (!d.locked && dy > 10 && dy > Math.abs(dx)) {
                        resetTouch(e.currentTarget);
                        return;
                      }
                      if (Math.abs(dx) < 8 && !d.locked) return;
                      d.locked = true;
                      d.dx = dx;
                      e.currentTarget.setPointerCapture(e.pointerId);
                      if (
                        !window.matchMedia("(prefers-reduced-motion: reduce)")
                          .matches
                      ) {
                        const depth =
                          e.currentTarget.closest<HTMLElement>(
                            ".console-depth",
                          );
                        depth?.style.setProperty(
                          "--touch-x",
                          `${Math.max(-5, Math.min(5, dx * 0.06))}px`,
                        );
                        depth?.style.setProperty(
                          "--touch-tilt",
                          `${Math.max(-1.4, Math.min(1.4, dx * 0.018))}deg`,
                        );
                      }
                    }}
                    onPointerUp={(e) => {
                      const d = swipe.current;
                      if (d?.id === e.pointerId) {
                        if (d.locked && Math.abs(d.dx) >= 36)
                          move(d.dx < 0 ? 1 : -1);
                        else if (!d.locked && Math.abs(e.clientY - d.y) < 10)
                          setInfo(true);
                      }
                      resetTouch(e.currentTarget);
                      if (e.currentTarget.hasPointerCapture(e.pointerId))
                        e.currentTarget.releasePointerCapture(e.pointerId);
                    }}
                    onPointerCancel={(e) => resetTouch(e.currentTarget)}
                    onLostPointerCapture={(e) => resetTouch(e.currentTarget)}
                  >
                    <div className="console-screen-content" key={game.id}>
                      <Image
                        src={game.artwork}
                        alt={`${game.name} on the PSG1 screen`}
                        fill
                        sizes="400px"
                      />
                      <div className="console-screen-shade" />
                      <div className="console-hud">
                        <span>PLEMU OS / PREVIEW</span>
                        <span>▰▰▰</span>
                      </div>
                      <div className="console-screen-title">
                        <small>
                          {genres[category]} · {(index % available.length) + 1}/
                          {available.length}
                        </small>
                        <h3>{game.name}</h3>
                        <span>◀ SWIPE · TAP TO SELECT ▶</span>
                      </div>
                      {info ? (
                        <div className="console-info">
                          <span className="eyebrow">CARTRIDGE INFO</span>
                          <h3>{game.name}</h3>
                          <p>
                            {game.genre} by {game.studio}
                          </p>
                          <Link href={`/game/${game.id}`}>
                            Enter this world <Icon name="arrow" size={14} />
                          </Link>
                          <span className="platform-badge">
                            {game.platform}
                          </span>
                          <small>B TO RETURN</small>
                        </div>
                      ) : null}
                    </div>
                    <div className="screen-scanlines" />
                    <div className="screen-boot" aria-hidden="true" />
                  </div>
                  <span className="console-brand">
                    PLAY SOLANA <i /> PSG1
                  </span>
                </div>
                <div className="console-controls">
                  <div className="dpad">
                    <button
                      className="dpad-up"
                      aria-label="Previous game up"
                      onClick={() => move(-1)}
                    >
                      ▲
                    </button>
                    <button
                      className="dpad-left"
                      aria-label="Previous console game"
                      onClick={() => move(-1)}
                    >
                      ◀
                    </button>
                    <i />
                    <button
                      className="dpad-right"
                      aria-label="Next console game"
                      onClick={() => move(1)}
                    >
                      ▶
                    </button>
                    <button
                      className="dpad-down"
                      aria-label="Next game down"
                      onClick={() => move(1)}
                    >
                      ▼
                    </button>
                  </div>
                  <span className="console-emblem">✳</span>
                  <div className="face-buttons">
                    <button
                      className="face-x"
                      aria-label="X switch game and asset"
                      onClick={() =>
                        setMode((m) => (m === "game" ? "asset" : "game"))
                      }
                    >
                      X
                    </button>
                    <button
                      className="face-y"
                      aria-label="Y save selected game"
                      onClick={() => toggleSaved(game.id)}
                    >
                      Y
                    </button>
                    <button
                      className="face-a"
                      aria-label="A open game information"
                      onClick={() => setInfo(true)}
                    >
                      A
                    </button>
                    <button
                      className="face-b"
                      aria-label="B back to game browser"
                      onClick={() => {
                        setInfo(false);
                        setMode("game");
                      }}
                    >
                      B
                    </button>
                  </div>
                </div>
                <div className="console-start">
                  <button
                    aria-label="Select switch game and asset"
                    onClick={() =>
                      setMode((m) => (m === "game" ? "asset" : "game"))
                    }
                  >
                    <i />
                    SELECT
                  </button>
                  <button
                    aria-label="Start game information"
                    onClick={() => setInfo((v) => !v)}
                  >
                    <i />
                    START
                  </button>
                </div>
                <div className="console-sticks" aria-hidden="true">
                  <i />
                  <span>EXPLORE / PLAY / COLLECT</span>
                  <i />
                </div>
              </div>
            </ConsoleDepth>
            <p className="console-touch-hint">
              Swipe the screen · Tap A for info · L / R for genres
            </p>
            <p className="console-footnote">
              Interactive Plemu concept · PSG1 hardware by Play Solana
            </p>
            <div
              className="console-touch-controls"
              aria-label="Touch game browsing"
            >
              <button
                className="round-control"
                aria-label="Browse previous game"
                onClick={() => move(-1)}
              >
                <Icon name="back" />
              </button>
              <label>
                <span>Choose a genre</span>
                <select
                  aria-label="Console genre"
                  value={category}
                  onChange={(e) => {
                    setCategory(Number(e.target.value));
                    setIndex(0);
                    setInfo(false);
                  }}
                >
                  {genres.map((genre, i) => (
                    <option value={i} key={genre}>
                      {genre}
                    </option>
                  ))}
                </select>
              </label>
              <button
                className="round-control"
                aria-label="Browse next game"
                onClick={() => move(1)}
              >
                <Icon name="arrow" />
              </button>
            </div>
          </div>
          <div
            className="console-companion"
            style={{ "--game-color": game.color } as CSSProperties}
          >
            <div className="console-mode">
              <button
                aria-pressed={mode === "game"}
                onClick={() => setMode("game")}
              >
                01 The game
              </button>
              <button
                aria-pressed={mode === "asset"}
                onClick={() => setMode("asset")}
              >
                02 Its economy <Icon name="arrow" size={14} />
              </button>
            </div>
            <div
              className="console-metadata"
              key={`${game.id}-${mode}`}
              aria-live="polite"
            >
              <span className="eyebrow">
                {mode === "game"
                  ? game.genre + " / " + game.studio
                  : "$" + game.symbol + " / DEMO ASSET"}
              </span>
              <h3>
                {game.name}
                <span>↗</span>
              </h3>
              <GamePrice game={game} />
              {mode === "game" ? (
                <>
                  <p>{game.description.split(". ").slice(0, 2).join(". ")}.</p>
                  <div className="world-tags">
                    <span className="platform-badge">{game.platform}</span>
                    <span>Independent</span>
                    <span>Concept game</span>
                  </div>
                </>
              ) : (
                <>
                  <p>
                    Every game has a story. Its asset is another way to follow
                    it. Explore a simulated market at your own pace.
                  </p>
                  <Chart
                    data={game.chart}
                    positive={game.change >= 0}
                    label={`${game.name} sample asset price`}
                  />
                  <p className="interaction-hint">
                    Sample prices · No real token or transaction
                  </p>
                  <Link
                    className="console-market-link"
                    href={`/game/${game.id}#economy`}
                  >
                    Follow ${game.symbol} in the market{" "}
                    <Icon name="arrow" size={14} />
                  </Link>
                </>
              )}
              <div className="button-row">
                <Link href={`/game/${game.id}`} className="button primary">
                  Explore game <Icon name="arrow" size={16} />
                </Link>
                <button
                  className="round-control"
                  aria-label={
                    saved.includes(game.id)
                      ? "Remove selected game from library"
                      : "Save selected game to library"
                  }
                  aria-pressed={saved.includes(game.id)}
                  onClick={() => toggleSaved(game.id)}
                >
                  <Icon name={saved.includes(game.id) ? "check" : "bookmark"} />
                </button>
              </div>
            </div>
            <div className="control-guide">
              <span>
                <b>↔</b> Browse games
              </span>
              <span>
                <b>A</b> Game info
              </span>
              <span>
                <b>B</b> Go back
              </span>
              <span>
                <b>L / R</b> Genres
              </span>
              <span>
                <b>X</b> Economy
              </span>
              <span>
                <b>Y</b> Save game
              </span>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
