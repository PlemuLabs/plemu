"use client";
import { useEffect, useRef } from "react";

/** Fresh, independent implementation of the public React Bits Pixel Magnet model.
 * The licensed registry was unavailable. No Pro implementation is included.
 * Contracting, grid-snapped squares orbit a spring-driven cursor attractor.
 */
type MagnetConfig = {
  count?: number;
  resolution?: number;
  speed?: number;
  spread?: number;
  sizeSpread?: number;
  falloff?: number;
  falloffCurve?: number;
  color?: string;
  hotColor?: string;
  opacity?: number;
  follow?: number;
  velocityScale?: number;
  idleStrength?: number;
  decay?: number;
  exclude?: string;
};
type Square = {
  x: number;
  y: number;
  vx: number;
  vy: number;
  phase: number;
  angle: number;
  weight: number;
};
const clamp = (v: number, a: number, b: number) => Math.max(a, Math.min(b, v));

export function PixelMagnet({
  count = 32,
  resolution = 96,
  speed = 1,
  spread = 0.72,
  sizeSpread = 1.5,
  falloff = 1.05,
  falloffCurve = 0.5,
  color = "#8C76DB",
  hotColor = "#B6D935",
  opacity = 1,
  follow = 10.5,
  velocityScale = 1.6,
  idleStrength = 0.12,
  decay = 1.2,
  exclude = "[data-pixel-protect]",
}: MagnetConfig) {
  const ref = useRef<HTMLCanvasElement>(null);
  useEffect(() => {
    const surface = ref.current,
      host = surface?.parentElement;
    if (!surface || !host) return;
    const brush = surface.getContext("2d");
    if (!brush) return;
    const media = matchMedia(
      "(min-width: 1000px) and (hover: hover) and (pointer: fine) and (prefers-reduced-motion: no-preference)",
    );
    let enabled = media.matches,
      inView = false,
      raf = 0,
      previousTime = 0;
    let width = 0,
      height = 0,
      phaseClock = 0,
      energy = 0;
    let squares: Square[] = [];
    const cursor = { x: 0, y: 0, time: 0, inside: false };
    const magnet = { x: 0, y: 0, vx: 0, vy: 0 };
    const home = { x: 0, y: 0 };
    const clear = () => brush.clearRect(0, 0, width, height);
    const cancel = () => {
      cancelAnimationFrame(raf);
      raf = 0;
      previousTime = 0;
    };
    const awaken = () => {
      if (!raf && enabled && inView && !document.hidden)
        raf = requestAnimationFrame(paint);
    };
    const paint = (now: number) => {
      raf = 0;
      if (!enabled || !inView || document.hidden) return;
      const dt = clamp(
        (now - (previousTime || now - 16.7)) / 1000,
        0.004,
        0.032,
      );
      previousTime = now;
      energy *= Math.exp(-decay * dt);
      const strength = cursor.inside ? Math.max(idleStrength, energy) : energy;
      // A damped spring, rather than rigid offset following, preserves momentum at turns.
      const stiffness = follow * follow;
      magnet.vx +=
        ((cursor.inside ? cursor.x : home.x) - magnet.x) * stiffness * dt;
      magnet.vy +=
        ((cursor.inside ? cursor.y : home.y) - magnet.y) * stiffness * dt;
      const drag = Math.exp(-follow * 1.55 * dt);
      magnet.vx *= drag;
      magnet.vy *= drag;
      magnet.x += magnet.vx * dt;
      magnet.y += magnet.vy * dt;
      phaseClock += dt * speed * energy * 0.55;
      const bounds = host.getBoundingClientRect();
      const zones = Array.from(host.querySelectorAll(exclude))
        .filter((el) => getComputedStyle(el).visibility !== "hidden")
        .map((el) => {
          const r = el.getBoundingClientRect();
          return {
            x: r.left - bounds.left - 14,
            y: r.top - bounds.top - 14,
            right: r.right - bounds.left + 14,
            bottom: r.bottom - bounds.top + 14,
          };
        });
      const cell = clamp(height / resolution, 5, 9);
      const radius =
        Math.min(220, height * 0.5 * spread) * (0.5 + strength * 0.5);
      clear();
      let unsettled = false;
      squares.forEach((square, index) => {
        const phase = (square.phase + phaseClock * square.weight) % 1;
        const distance = 1 - phase; // Squares contract inward while motion supplies energy.
        const radial = cursor.inside
          ? Math.pow(distance, 0.72) * radius
          : 18 + square.phase * 58;
        const angle = square.angle + phase * 0.36;
        const tx = magnet.x + Math.cos(angle) * radial;
        const ty = magnet.y + Math.sin(angle) * radial * 0.78;
        const spring = 48 + square.weight * 55;
        square.vx =
          (square.vx + (tx - square.x) * spring * dt) * Math.exp(-12 * dt);
        square.vy =
          (square.vy + (ty - square.y) * spring * dt) * Math.exp(-12 * dt);
        square.x += square.vx * dt;
        square.y += square.vy * dt;
        unsettled ||= Math.abs(tx - square.x) + Math.abs(ty - square.y) > 0.3;
        const size = cell * (1 + Math.floor((1 - distance) * sizeSpread));
        const x = Math.round(square.x / cell) * cell,
          y = Math.round(square.y / cell) * cell;
        if (
          x < 2 ||
          y < 2 ||
          x + size > width - 2 ||
          y + size > height - 2 ||
          zones.some(
            (z) =>
              x + size > z.x && x < z.right && y + size > z.y && y < z.bottom,
          )
        )
          return;
        const edge = Math.pow(
          clamp((falloff - distance) / falloffCurve, 0, 1),
          0.7,
        );
        const alpha = cursor.inside
          ? 0.4 + strength * 0.6
          : 0.12 + strength * 0.35;
        brush.globalAlpha = clamp(alpha * edge * opacity, 0, 1);
        brush.fillStyle = energy > 0.5 && index % 5 === 0 ? hotColor : color;
        brush.fillRect(x, y, size, size);
      });
      brush.globalAlpha = 1;
      if (
        energy > 0.008 ||
        unsettled ||
        Math.abs(magnet.vx) + Math.abs(magnet.vy) > 0.2
      )
        awaken();
    };
    const resize = () => {
      width = host.clientWidth;
      height = host.clientHeight;
      const dpr = Math.min(2, devicePixelRatio || 1);
      surface.width = Math.round(width * dpr);
      surface.height = Math.round(height * dpr);
      brush.setTransform(dpr, 0, 0, dpr, 0, 0);
      home.x = width * 0.46;
      home.y = height - 66;
      magnet.x = home.x;
      magnet.y = home.y;
      magnet.vx = magnet.vy = 0;
      squares = Array.from(
        { length: clamp(Math.round(count), 8, 64) },
        (_, i) => ({
          x: home.x,
          y: home.y,
          vx: 0,
          vy: 0,
          phase: (i * 0.61803398875 + 0.17) % 1,
          angle: i * 2.3999632,
          weight: 0.7 + (i % 7) * 0.12,
        }),
      );
      cancel();
      awaken();
    };
    const track = (e: PointerEvent) => {
      if (!enabled || e.pointerType === "touch") return;
      const r = host.getBoundingClientRect();
      const x = e.clientX - r.left,
        y = e.clientY - r.top;
      const elapsed = Math.max(8, e.timeStamp - cursor.time);
      const velocity = cursor.inside
        ? Math.hypot(x - cursor.x, y - cursor.y) / elapsed
        : 0.5;
      energy = Math.max(energy, clamp(velocity / velocityScale, 0.28, 1));
      cursor.x = x;
      cursor.y = y;
      cursor.time = e.timeStamp;
      cursor.inside = true;
      awaken();
    };
    const leave = () => {
      cursor.inside = false;
      awaken();
    };
    const preference = () => {
      enabled = media.matches;
      cancel();
      cursor.inside = false;
      energy = 0;
      if (enabled) awaken();
      else clear();
    };
    const visibility = () => {
      leave();
      if (document.hidden) cancel();
      else awaken();
    };
    const observer = new IntersectionObserver(([entry]) => {
      inView = entry.isIntersecting;
      if (inView) awaken();
      else {
        cursor.inside = false;
        energy = 0;
        cancel();
      }
    });
    const sizeObserver = new ResizeObserver(resize);
    observer.observe(host);
    sizeObserver.observe(host);
    resize();
    host.addEventListener("pointermove", track, { passive: true });
    host.addEventListener("pointerleave", leave);
    host.addEventListener("pointercancel", leave);
    media.addEventListener("change", preference);
    document.addEventListener("visibilitychange", visibility);
    window.addEventListener("blur", leave);
    return () => {
      cancel();
      clear();
      observer.disconnect();
      sizeObserver.disconnect();
      host.removeEventListener("pointermove", track);
      host.removeEventListener("pointerleave", leave);
      host.removeEventListener("pointercancel", leave);
      media.removeEventListener("change", preference);
      document.removeEventListener("visibilitychange", visibility);
      window.removeEventListener("blur", leave);
    };
  }, [
    count,
    resolution,
    speed,
    spread,
    sizeSpread,
    falloff,
    falloffCurve,
    color,
    hotColor,
    opacity,
    follow,
    velocityScale,
    idleStrength,
    decay,
    exclude,
  ]);
  return <canvas ref={ref} className="signature-magnet" aria-hidden="true" />;
}
