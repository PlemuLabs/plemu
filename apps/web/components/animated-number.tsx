"use client";
import { useEffect, useRef } from "react";
export function AnimatedNumber({
  value,
  compact = false,
}: {
  value: number;
  compact?: boolean;
}) {
  const ref = useRef<HTMLSpanElement>(null);
  const format = (n: number) =>
    compact
      ? new Intl.NumberFormat("en-US", {
          style: "currency",
          currency: "USD",
          notation: "compact",
          maximumFractionDigits: 2,
        }).format(n)
      : Math.round(n).toLocaleString("en-US");
  useEffect(() => {
    const el = ref.current;
    if (!el || window.matchMedia("(prefers-reduced-motion: reduce)").matches)
      return;
    let frame = 0;
    const observer = new IntersectionObserver(
      ([entry]) => {
        if (!entry.isIntersecting) return;
        observer.disconnect();
        const start = performance.now();
        const update = (time: number) => {
          const progress = Math.min(1, (time - start) / 1000);
          el.textContent = format(value * (1 - Math.pow(1 - progress, 3)));
          if (progress < 1) frame = requestAnimationFrame(update);
        };
        frame = requestAnimationFrame(update);
      },
      { threshold: 0.5 },
    );
    observer.observe(el);
    return () => {
      observer.disconnect();
      cancelAnimationFrame(frame);
    };
  }, [value, compact]);
  return <span ref={ref}>{format(value)}</span>;
}
