"use client";
import { useEffect, useRef, type ReactNode } from "react";
import gsap from "gsap";

export function ConsoleDepth({ children }: { children: ReactNode }) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const node = ref.current;
    const scene = node?.closest(".console-scene");
    if (!node || !scene) return;
    const media = gsap.matchMedia();
    media.add(
      "(min-width: 1000px) and (hover: hover) and (pointer: fine) and (prefers-reduced-motion: no-preference)",
      () => {
        const move = (event: Event) => {
          const e = event as PointerEvent;
          if (e.pointerType !== "mouse" && e.pointerType !== "pen") return;
          // Stop drift under a control so the pointer target stays physically stable.
          if (
            node.contains(document.activeElement) ||
            (e.target as Element).closest("button, a")
          ) {
            gsap.killTweensOf(node);
            return;
          }
          const r = scene.getBoundingClientRect();
          const x = Math.max(
            -1,
            Math.min(1, ((e.clientX - r.left) / r.width) * 2 - 1),
          );
          const y = Math.max(
            -1,
            Math.min(1, ((e.clientY - r.top) / r.height) * 2 - 1),
          );
          gsap.to(node, {
            rotateX: -y * 4,
            rotateY: x * 4,
            x: x * 3,
            y: y * 3,
            duration: 0.55,
            ease: "power3.out",
            overwrite: true,
          });
        };
        const reset = () =>
          gsap.to(node, {
            rotateX: 0,
            rotateY: 0,
            x: 0,
            y: 0,
            duration: 0.7,
            ease: "power3.out",
            overwrite: true,
          });
        scene.addEventListener("pointermove", move);
        scene.addEventListener("focusin", reset);
        scene.addEventListener("pointerleave", reset);
        scene.addEventListener("pointercancel", reset);
        return () => {
          scene.removeEventListener("pointermove", move);
          scene.removeEventListener("focusin", reset);
          scene.removeEventListener("pointerleave", reset);
          scene.removeEventListener("pointercancel", reset);
          gsap.killTweensOf(node);
          gsap.set(node, { clearProps: "transform" });
        };
      },
    );
    return () => media.revert();
  }, []);
  return (
    <div className="console-depth" ref={ref}>
      {children}
    </div>
  );
}
