"use client";
import { useEffect, useRef, type ReactNode } from "react";

/** Stable link target; only decorative artwork moves under the cursor. */
export function ArtworkLight({ children }: { children: ReactNode }) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const node = ref.current;
    if (!node) return;
    const focusTarget = node.closest("a") ?? node;
    const media = window.matchMedia(
      "(hover: hover) and (pointer: fine) and (prefers-reduced-motion: no-preference)",
    );
    let frame = 0,
      x = 0.5,
      y = 0.5;
    const paint = () => {
      frame = 0;
      node.style.setProperty("--light-x", `${x * 100}%`);
      node.style.setProperty("--light-y", `${y * 100}%`);
      node.style.setProperty("--art-x", `${(x - 0.5) * 8}px`);
      node.style.setProperty("--art-y", `${(y - 0.5) * 8}px`);
    };
    const move = (e: PointerEvent) => {
      if (!media.matches || e.pointerType === "touch") return;
      const r = node.getBoundingClientRect();
      x = Math.max(0, Math.min(1, (e.clientX - r.left) / r.width));
      y = Math.max(0, Math.min(1, (e.clientY - r.top) / r.height));
      if (!frame) frame = requestAnimationFrame(paint);
    };
    const reset = () => {
      cancelAnimationFrame(frame);
      frame = 0;
      x = y = 0.5;
      paint();
    };
    node.addEventListener("pointermove", move, { passive: true });
    node.addEventListener("pointerleave", reset);
    node.addEventListener("pointercancel", reset);
    focusTarget.addEventListener("focusin", reset);
    media.addEventListener("change", reset);
    return () => {
      reset();
      node.removeEventListener("pointermove", move);
      node.removeEventListener("pointerleave", reset);
      node.removeEventListener("pointercancel", reset);
      focusTarget.removeEventListener("focusin", reset);
      media.removeEventListener("change", reset);
    };
  }, []);
  return (
    <div className="artwork-light" ref={ref}>
      {children}
      <span className="artwork-reflection" aria-hidden="true" />
    </div>
  );
}
