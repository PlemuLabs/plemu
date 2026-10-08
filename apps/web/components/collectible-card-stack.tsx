"use client";
import { useEffect, useRef, useState, type CSSProperties } from "react";
import gsap from "gsap";
import { flushSync } from "react-dom";
import { type Game } from "../lib/games";
import { CollectibleGameCard, packHit } from "./collectible-game-card";

export function CollectibleCardStack({
  games,
  reducedMotion = false,
}: {
  games: Game[];
  reducedMotion?: boolean;
}) {
  const [order, setOrder] = useState(() => games.map((g) => g.id));
  const root = useRef<HTMLDivElement>(null);
  const drag = useRef<{
    id: number;
    x: number;
    y: number;
    dx: number;
    lastX: number;
    lastAt: number;
    velocity: number;
    locked: boolean;
  } | null>(null);
  const busy = useRef(false);
  const suppressClick = useRef(false);
  const hit = packHit(games);
  const front = games.find((g) => g.id === order[0])!;
  const motionReduced = () =>
    reducedMotion ||
    window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  useEffect(() => {
    const node = root.current;
    return () => {
      if (node) gsap.killTweensOf(node.querySelectorAll(".card-stack-motion"));
    };
  }, []);
  const motion = () =>
    root.current?.querySelector<HTMLElement>(
      '[data-front="true"] .card-stack-motion',
    );
  const cycle = (direction = 1, previous = false) => {
    const node = previous
      ? root.current?.querySelector<HTMLElement>(
          ".collectible-stack-layer:last-child .card-stack-motion",
        )
      : motion();
    if (!node || busy.current || games.length < 2) return;
    busy.current = true;
    root.current?.setAttribute("data-switching", "true");
    const keyboardFocus = !!root.current
      ?.querySelector('[data-front="true"]')
      ?.contains(document.activeElement);
    const reduced = motionReduced();
    const reorder = () =>
      flushSync(() =>
        setOrder((ids) =>
          previous
            ? [ids[ids.length - 1], ...ids.slice(0, -1)]
            : [...ids.slice(1), ids[0]],
        ),
      );
    const finish = () => {
      gsap.set(node, { clearProps: "transform,opacity" });
      busy.current = false;
      root.current?.removeAttribute("data-switching");
      if (keyboardFocus)
        root.current
          ?.querySelector<HTMLElement>('[data-front="true"] a')
          ?.focus({ preventScroll: true });
    };
    if (reduced) {
      reorder();
      finish();
      return;
    }
    const stage = root
      .current!.querySelector<HTMLElement>(".collectible-stack-stage")!
      .getBoundingClientRect();
    const side = previous ? -1 : direction;
    const available = side < 0 ? stage.left : window.innerWidth - stage.right;
    const distance = Math.max(0, Math.min(stage.width * 0.58, available - 58));
    const rotation = Math.min(7, 2 + distance * 0.03);
    // Keep the card visible throughout: draw it aside, change depth, then tuck it behind.
    if (previous) {
      gsap.set(node, { x: -distance, y: -10, rotation: -rotation });
      reorder();
      gsap.to(node, {
        x: 0,
        y: 0,
        rotation: 0,
        duration: 0.48,
        ease: "power3.out",
        overwrite: true,
        onComplete: finish,
      });
    } else {
      gsap.to(node, {
        x: direction * distance,
        y: -12,
        rotation: direction * rotation,
        duration: 0.22,
        ease: "power2.out",
        overwrite: true,
        onComplete: () => {
          reorder();
          gsap.to(node, {
            x: 0,
            y: 0,
            rotation: 0,
            duration: 0.42,
            ease: "power3.out",
            onComplete: finish,
          });
        },
      });
    }
  };
  const cancel = () => {
    drag.current = null;
    const node = motion();
    if (!busy.current && node)
      gsap.to(node, {
        x: 0,
        rotation: 0,
        y: 0,
        duration: motionReduced() ? 0 : 0.42,
        ease: "back.out(1.15)",
        overwrite: true,
      });
  };
  return (
    <div className="collectible-stack" ref={root}>
      <div
        className="collectible-stack-stage"
        role="group"
        aria-label="Pack card stack"
        onDragStart={(e) => e.preventDefault()}
        onClickCapture={(e) => {
          if (suppressClick.current || busy.current) {
            e.preventDefault();
            e.stopPropagation();
          }
        }}
        onKeyDown={(e) => {
          if (e.key === "Enter") suppressClick.current = false;
          if (e.key === "ArrowRight" || e.key === "ArrowLeft") {
            e.preventDefault();
            cycle(e.key === "ArrowRight" ? 1 : -1);
          }
        }}
        onPointerDown={(e) => {
          if (busy.current || e.button !== 0 || !e.isPrimary) return;
          suppressClick.current = false;
          drag.current = {
            id: e.pointerId,
            x: e.clientX,
            y: e.clientY,
            dx: 0,
            lastX: e.clientX,
            lastAt: e.timeStamp,
            velocity: 0,
            locked: false,
          };
        }}
        onPointerMove={(e) => {
          const d = drag.current;
          if (!d || d.id !== e.pointerId) return;
          const dx = e.clientX - d.x,
            dy = Math.abs(e.clientY - d.y);
          if (!d.locked && dy > 10 && dy > Math.abs(dx)) {
            suppressClick.current = true;
            cancel();
            return;
          }
          if (!d.locked && Math.abs(dx) < 8) return;
          d.locked = true;
          const elapsed = Math.max(8, e.timeStamp - d.lastAt);
          d.velocity =
            d.velocity * 0.35 + ((e.clientX - d.lastX) / elapsed) * 0.65;
          d.lastX = e.clientX;
          d.lastAt = e.timeStamp;
          d.dx = dx;
          suppressClick.current = true;
          e.currentTarget.setPointerCapture(e.pointerId);
          const node = motion();
          if (!node) return;
          if (motionReduced()) return;
          const width = e.currentTarget.getBoundingClientRect().width;
          const bounds = e.currentTarget.getBoundingClientRect();
          const available =
            dx < 0 ? bounds.left : window.innerWidth - bounds.right;
          const limit = Math.max(12, available - 18);
          const travel = dx / (1 + (Math.abs(dx) / width) * 0.45);
          const x = Math.sign(dx) * Math.min(Math.abs(travel), limit);
          gsap.to(node, {
            x,
            y: -Math.min(8, Math.abs(x) * 0.035),
            rotation: Math.max(-9, Math.min(9, x * 0.045)),
            duration: 0.1,
            ease: "power2.out",
            overwrite: true,
          });
        }}
        onPointerUp={(e) => {
          const d = drag.current;
          drag.current = null;
          const threshold = Math.max(
            56,
            Math.min(80, e.currentTarget.getBoundingClientRect().width * 0.25),
          );
          const flick =
            d &&
            Math.abs(d.dx) > 24 &&
            e.timeStamp - d.lastAt < 110 &&
            Math.abs(d.velocity) > 0.5;
          if (d && d.locked && (Math.abs(d.dx) >= threshold || flick))
            cycle(d.dx > 0 ? 1 : -1);
          else cancel();
          if (e.currentTarget.hasPointerCapture(e.pointerId))
            e.currentTarget.releasePointerCapture(e.pointerId);
        }}
        onPointerCancel={cancel}
        onLostPointerCapture={cancel}
      >
        {order.map((id, index) => {
          const game = games.find((g) => g.id === id)!;
          return (
            <div
              key={id}
              className="collectible-stack-layer"
              data-front={index === 0}
              inert={index !== 0}
              style={
                {
                  "--stack-index": index,
                  zIndex: order.length - index,
                } as CSSProperties
              }
            >
              <div className="card-stack-motion">
                <CollectibleGameCard game={game} hit={game.id === hit?.id} />
              </div>
            </div>
          );
        })}
      </div>
      <div className="collectible-stack-controls">
        <button aria-label="Previous card" onClick={() => cycle(-1, true)}>
          ←
        </button>
        <span aria-live="polite">
          {front.name}
          <small>
            SWIPE TO SWITCH · {games.findIndex((g) => g.id === front.id) + 1} /{" "}
            {games.length}
          </small>
        </span>
        <button aria-label="Next card" onClick={() => cycle(1)}>
          →
        </button>
      </div>
      <p className="pack-hit-note">✦ HIT · Highest token price in this pack</p>
    </div>
  );
}
