"use client";
import { useEffect, useRef } from "react";
import gsap from "gsap";
import { ScrollTrigger } from "gsap/ScrollTrigger";
export function MotionScene({ children }: { children: React.ReactNode }) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    gsap.registerPlugin(ScrollTrigger);
    const match = gsap.matchMedia();
    match.add(
      {
        motion: "(prefers-reduced-motion: no-preference)",
        desktop:
          "(min-width: 1100px) and (min-height: 760px) and (hover: hover) and (pointer: fine)",
      },
      (media) => {
        if (!media.conditions?.motion) return;
        const context = gsap.context(() => {
          gsap.from(".play-hero-copy > *", {
            y: 24,
            opacity: 0,
            stagger: 0.08,
            duration: 0.65,
            ease: "power3.out",
          });
          gsap.from(".hero-cartridges", {
            y: 40,
            rotation: media.conditions?.desktop ? 4 : 0,
            duration: 1,
            ease: "power3.out",
          });
          if (media.conditions?.desktop) {
            gsap.from(".workbench-window", {
              rotation: -5,
              y: 24,
              scrollTrigger: {
                trigger: ".create-feature",
                start: "top 80%",
                end: "center center",
                scrub: 1,
              },
            });
          }
          gsap.from(".pack-fan", {
            y: 32,
            duration: 0.8,
            scrollTrigger: {
              trigger: ".pack-feature",
              start: "top 80%",
              once: true,
            },
          });
          if (media.conditions?.desktop) {
            gsap.fromTo(
              ".screen-boot",
              { scaleY: 1, opacity: 1 },
              {
                scaleY: 0,
                opacity: 0,
                duration: 0.55,
                ease: "steps(5)",
                scrollTrigger: {
                  trigger: ".psg-story",
                  start: "top 65%",
                  once: true,
                },
              },
            );
            const scene = gsap.timeline({
              scrollTrigger: {
                trigger: ".psg-story",
                start: () =>
                  `top ${document.querySelector(".site-header")?.getBoundingClientRect().height ?? 92}px`,
                end: "+=180",
                pin: ".psg-stage",
                scrub: 0.6,
                anticipatePin: 1,
              },
            });
            scene
              .from(
                ".psg-console",
                {
                  y: 30,
                  rotateY: -4,
                  rotation: -5,
                  scale: 0.98,
                  ease: "power2.out",
                },
                0,
              )
              .from(".console-companion", { x: 25 }, 0)
              .from(".control-guide", { y: 15 }, 0.4);
          }
        }, ref);
        return () => context.revert();
      },
    );
    return () => match.revert();
  }, []);
  return <div ref={ref}>{children}</div>;
}
export function Magnetic({
  children,
  className = "",
}: {
  children: React.ReactNode;
  className?: string;
}) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const node = ref.current;
    const target = node?.firstElementChild;
    if (!node || !target) return;
    const media = gsap.matchMedia();
    media.add(
      "(hover: hover) and (pointer: fine) and (prefers-reduced-motion: no-preference)",
      () => {
        const reset = () =>
          gsap.to(target, {
            x: 0,
            y: 0,
            duration: 0.45,
            ease: "power3.out",
            overwrite: true,
          });
        const move = (e: PointerEvent) => {
          if (
            e.pointerType === "touch" ||
            node.contains(document.activeElement)
          )
            return;
          const r = node.getBoundingClientRect();
          gsap.to(target, {
            x: Math.max(
              -4,
              Math.min(4, (e.clientX - r.left - r.width / 2) * 0.045),
            ),
            y: Math.max(
              -3,
              Math.min(3, (e.clientY - r.top - r.height / 2) * 0.08),
            ),
            duration: 0.25,
            ease: "power2.out",
            overwrite: true,
          });
        };
        node.addEventListener("pointermove", move);
        node.addEventListener("pointerleave", reset);
        node.addEventListener("pointercancel", reset);
        node.addEventListener("focusin", reset);
        node.addEventListener("pointerdown", reset);
        return () => {
          node.removeEventListener("pointermove", move);
          node.removeEventListener("pointerleave", reset);
          node.removeEventListener("pointercancel", reset);
          node.removeEventListener("focusin", reset);
          node.removeEventListener("pointerdown", reset);
          gsap.killTweensOf(target);
          gsap.set(target, { clearProps: "transform" });
        };
      },
    );
    return () => media.revert();
  }, []);
  return (
    <div ref={ref} className={`magnetic ${className}`}>
      {children}
    </div>
  );
}
