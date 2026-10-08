"use client";
import { useId, useState } from "react";
import { money } from "../lib/games";
export function Chart({
  data,
  positive = true,
  large = false,
  label = "Price history",
  range = "1D",
}: {
  data: number[];
  positive?: boolean;
  large?: boolean;
  label?: string;
  range?: string;
}) {
  const id = useId().replace(/:/g, "");
  const [active, setActive] = useState<number | null>(null);
  const w = 600,
    h = large ? 240 : 70,
    pad = large ? 18 : 5;
  const min = Math.min(...data) * 0.97,
    max = Math.max(...data) * 1.03;
  const points = data.map((value, i) => ({
    x: (i / (data.length - 1)) * w,
    y: h - pad - ((value - min) / (max - min)) * (h - pad * 2),
  }));
  const d = points
    .map((p, i) => `${i ? "L" : "M"}${p.x.toFixed(1)},${p.y.toFixed(1)}`)
    .join(" ");
  const color = positive ? "var(--green)" : "var(--red)";
  const selected = active !== null ? points[active] : null;
  return (
    <div
      className={`chart ${large ? "chart-large" : "sparkline"}`}
      style={{ color }}
    >
      {large && active !== null ? (
        <span
          className="chart-tooltip"
          role="status"
          aria-live="polite"
          aria-atomic="true"
          style={{
            left: `${Math.min(85, Math.max(5, (active / (data.length - 1)) * 100))}%`,
          }}
        >
          {money(data[active])}
          <small>Sample {active + 1} · demo data</small>
        </span>
      ) : null}
      <svg
        viewBox={`0 0 ${w} ${h}`}
        role="img"
        aria-label={label}
        tabIndex={large ? 0 : undefined}
        onKeyDown={
          large
            ? (event) => {
                if (event.key === "ArrowRight" || event.key === "ArrowLeft") {
                  event.preventDefault();
                  setActive((current) =>
                    Math.min(
                      data.length - 1,
                      Math.max(
                        0,
                        (current ?? 0) + (event.key === "ArrowRight" ? 1 : -1),
                      ),
                    ),
                  );
                }
              }
            : undefined
        }
        onBlur={() => setActive(null)}
        onPointerDown={
          large
            ? (e) => {
                const rect = e.currentTarget.getBoundingClientRect();
                setActive(
                  Math.max(
                    0,
                    Math.min(
                      data.length - 1,
                      Math.round(
                        ((e.clientX - rect.left) / rect.width) *
                          (data.length - 1),
                      ),
                    ),
                  ),
                );
              }
            : undefined
        }
        preserveAspectRatio="none"
        onPointerMove={
          large
            ? (e) => {
                const rect = e.currentTarget.getBoundingClientRect();
                setActive(
                  Math.min(
                    data.length - 1,
                    Math.max(
                      0,
                      Math.round(
                        ((e.clientX - rect.left) / rect.width) *
                          (data.length - 1),
                      ),
                    ),
                  ),
                );
              }
            : undefined
        }
        onPointerLeave={(e) => {
          if (e.pointerType !== "touch") setActive(null);
        }}
      >
        <defs>
          <linearGradient id={`fill-${id}`} x1="0" x2="0" y1="0" y2="1">
            <stop stopColor="currentColor" stopOpacity=".19" />
            <stop offset="1" stopColor="currentColor" stopOpacity="0" />
          </linearGradient>
        </defs>
        {large
          ? Array.from({ length: 5 }, (_, i) => (
              <path
                key={i}
                d={`M0 ${(i * h) / 4}H600`}
                stroke="var(--line)"
                strokeDasharray="3 6"
              />
            ))
          : null}
        <path
          d={`${d} L600 ${h} L0 ${h} Z`}
          fill={`url(#fill-${id})`}
          stroke="none"
        />
        <path
          className="chart-line"
          d={d}
          fill="none"
          stroke="currentColor"
          strokeWidth={large ? 2 : 1.6}
          vectorEffect="non-scaling-stroke"
          pathLength="1"
        />
        {selected ? (
          <>
            <path
              d={`M${selected.x} 0V${h}`}
              stroke="currentColor"
              strokeDasharray="3 4"
              strokeOpacity=".5"
            />
            <circle cx={selected.x} cy={selected.y} r="4" fill="currentColor" />
          </>
        ) : null}
      </svg>
      {large ? (
        <div className="chart-axis">
          {(range === "1H"
            ? ["60m ago", "48m", "36m", "24m", "12m", "Now"]
            : range === "4H"
              ? ["4h ago", "3h", "2h", "1h", "30m", "Now"]
              : range === "1W"
                ? ["7d ago", "6d", "4d", "3d", "1d", "Now"]
                : range === "1M"
                  ? ["30d ago", "24d", "18d", "12d", "6d", "Now"]
                  : range === "ALL"
                    ? ["Launch", "Week 2", "Week 4", "Week 6", "Week 8", "Now"]
                    : ["24h ago", "20h", "16h", "12h", "6h", "Now"]
          ).map((t) => (
            <span key={t}>{t}</span>
          ))}
        </div>
      ) : null}
    </div>
  );
}
