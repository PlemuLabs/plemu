"use client";
import { useEffect, useRef, useState, type ReactNode } from "react";
import gsap from "gsap";

/** Horizontal tear with native vertical scrolling and an independent keyboard action. */
export function TearPack({
  children,
  className = "",
  label,
  onOpen,
  reducedMotion = false,
}: {
  children: ReactNode;
  className?: string;
  label: string;
  onOpen: () => void;
  reducedMotion?: boolean;
}) {
  const root = useRef<HTMLDivElement>(null);
  const drag = useRef<{
    id: number;
    x: number;
    y: number;
    threshold: number;
    dx: number;
    locked: boolean;
  } | null>(null);
  const completing = useRef(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [phase, setPhase] = useState("ready");
  const [used, setUsed] = useState(false);
  const motionReduced = () =>
    reducedMotion ||
    window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  useEffect(() => {
    const node = root.current;
    return () => {
      if (timer.current) clearTimeout(timer.current);
      if (node) gsap.killTweensOf(node);
    };
  }, []);
  const reset = () => {
    drag.current = null;
    if (completing.current) return;
    setPhase("ready");
    gsap.to(root.current, {
      "--tear": 0,
      duration: motionReduced() ? 0 : 0.5,
      ease: "back.out(1.5)",
      overwrite: true,
    });
  };
  const open = () => {
    if (completing.current) return;
    completing.current = true;
    drag.current = null;
    setPhase("opening");
    if (motionReduced()) {
      onOpen();
      return;
    }
    gsap.to(root.current, {
      "--tear": 1.7,
      duration: 0.55,
      ease: "power2.inOut",
      overwrite: true,
    });
    timer.current = setTimeout(onOpen, 580);
  };
  return (
    <div
      ref={root}
      className={`tear-surface ${className}`}
      role="button"
      tabIndex={0}
      aria-label={label}
      aria-busy={phase === "opening"}
      data-phase={phase}
      onDragStart={(e) => e.preventDefault()}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          open();
        }
      }}
      onPointerDown={(e) => {
        if (!e.isPrimary || e.button !== 0 || completing.current) return;
        const r = e.currentTarget.getBoundingClientRect();
        drag.current = {
          id: e.pointerId,
          x: e.clientX,
          y: e.clientY,
          threshold:
            e.pointerType === "touch"
              ? Math.max(56, Math.min(88, r.width * 0.22))
              : Math.max(80, Math.min(140, r.width * 0.3)),
          dx: 0,
          locked: false,
        };
        setPhase("held");
      }}
      onPointerMove={(e) => {
        const d = drag.current;
        if (!d || d.id !== e.pointerId) return;
        const dx = e.clientX - d.x,
          dy = Math.abs(e.clientY - d.y);
        if (!d.locked && dy > 10 && dy > Math.abs(dx)) {
          reset();
          return;
        }
        if (!d.locked && Math.abs(dx) < 8) return;
        d.locked = true;
        d.dx = dx;
        e.currentTarget.setPointerCapture(e.pointerId);
        if (dx > 10) setUsed(true);
        setPhase(dx >= d.threshold ? "armed" : "held");
        gsap.to(root.current, {
          "--tear": Math.max(0, Math.min(1.12, dx / d.threshold)),
          duration: motionReduced() ? 0 : 0.12,
          ease: "power2.out",
          overwrite: true,
        });
      }}
      onPointerUp={(e) => {
        const d = drag.current;
        if (d && d.id === e.pointerId && d.locked && d.dx >= d.threshold)
          open();
        else reset();
        if (e.currentTarget.hasPointerCapture(e.pointerId))
          e.currentTarget.releasePointerCapture(e.pointerId);
      }}
      onPointerCancel={reset}
      onLostPointerCapture={reset}
    >
      {children}
      <span
        className={`tear-hint ${used ? "experienced" : ""}`}
        aria-hidden="true"
      >
        HOLD + DRAG RIGHT →
      </span>
    </div>
  );
}
