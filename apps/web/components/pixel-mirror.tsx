"use client";
import { useCallback, useEffect, useRef, useState } from "react";
import dynamic from "next/dynamic";
import { createCameraSession } from "../lib/camera-session";

const WebcamPixelGrid = dynamic(() => import("./ui/webcam-pixel-grid"), {
  ssr: false,
});
type CameraState =
  "idle" | "requesting" | "starting" | "live" | "error" | "paused";

export function PixelMirror() {
  const section = useRef<HTMLElement>(null);
  const camera = useRef<ReturnType<typeof createCameraSession> | null>(null);
  const [stream, setStream] = useState<MediaStream | null>(null);
  const [status, setStatus] = useState<CameraState>("idle");
  const [message, setMessage] = useState("");
  const [visible, setVisible] = useState(false);
  const [sample, setSample] = useState(false);
  const [color, setColor] = useState("#8C76DB");
  const [reduced, setReduced] = useState(false);
  const [density, setDensity] = useState(64);
  const stop = useCallback((next: CameraState = "idle") => {
    camera.current?.stop();
    setStream(null);
    setSample(false);
    setStatus(next);
  }, []);

  useEffect(() => {
    camera.current = createCameraSession();
    const media = matchMedia("(prefers-reduced-motion: reduce)");
    const small = matchMedia("(max-width: 600px)");
    const tablet = matchMedia("(max-width: 900px)");
    const sync = () => {
      setReduced(media.matches);
      setDensity(small.matches ? 48 : tablet.matches ? 56 : 64);
    };
    sync();
    [media, small, tablet].forEach((query) =>
      query.addEventListener("change", sync),
    );
    const observer = new IntersectionObserver(
      ([entry]) => {
        setVisible(entry.isIntersecting && !document.hidden);
        if (!entry.isIntersecting) stop("paused");
      },
      { threshold: 0.05 },
    );
    if (section.current) observer.observe(section.current);
    const hidden = () => {
      if (document.hidden) {
        setVisible(false);
        stop("paused");
      } else if (section.current) {
        const r = section.current.getBoundingClientRect();
        setVisible(r.bottom > 0 && r.top < innerHeight);
      }
    };
    const pagehide = () => stop("paused");
    document.addEventListener("visibilitychange", hidden);
    window.addEventListener("pagehide", pagehide);
    return () => {
      camera.current?.stop();
      observer.disconnect();
      [media, small, tablet].forEach((query) =>
        query.removeEventListener("change", sync),
      );
      document.removeEventListener("visibilitychange", hidden);
      window.removeEventListener("pagehide", pagehide);
    };
  }, [stop]);

  const failed = useCallback(
    (error: Error) => {
      console.warn("Pixel Mirror camera unavailable", { reason: error.name });
      stop("error");
      const denied =
        error.name === "NotAllowedError" || error.name === "SecurityError";
      setMessage(
        denied
          ? "Camera access wasn't granted. You can still explore the sample, or allow access in your browser and try again."
          : "Camera unavailable here. Try another browser or explore the sample — everything else still works.",
      );
    },
    [stop],
  );
  const ready = useCallback(() => {
    if (camera.current?.active) setStatus("live");
  }, []);
  const enable = async () => {
    if (!camera.current) return;
    setSample(false);
    setMessage("");
    setStatus("requesting");
    try {
      if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia)
        throw new Error("Camera API unavailable");
      const acquired = await camera.current.request(() =>
        navigator.mediaDevices.getUserMedia({
          audio: false,
          video: {
            width: { ideal: 640 },
            height: { ideal: 480 },
            facingMode: "user",
            frameRate: { ideal: 24, max: 30 },
          },
        }),
      );
      if (!acquired) return;
      acquired
        .getVideoTracks()
        .forEach((track) =>
          track.addEventListener("ended", () => stop("paused"), { once: true }),
        );
      setStream(acquired);
      setStatus("starting");
    } catch (error) {
      failed(error instanceof Error ? error : new Error("Camera unavailable"));
    }
  };
  const busy = status === "requesting" || status === "starting";
  const live = status === "live";
  const statusText = live
    ? "CAMERA LIVE · LOCAL ONLY"
    : busy
      ? "WAITING FOR YOUR CAMERA"
      : sample && !reduced
        ? "MOVING SAMPLE · CAMERA OFF"
        : "SAMPLE WORLD · CAMERA OFF";

  return (
    <section
      className="pixel-mirror"
      id="pixel-mirror"
      ref={section}
      aria-labelledby="pixel-mirror-heading"
    >
      <div className="pixel-mirror-copy">
        <span className="eyebrow">PLEMU PLAYGROUND / 01</span>
        <h2 id="pixel-mirror-heading">
          Your world.
          <br />
          <span className="pixel-text">In pixels.</span>
        </h2>
        <p>
          Meet your pixel self. Move, wave, make a face — and watch a little
          world take shape.
        </p>
        <div className="mirror-actions">
          {busy || live ? (
            <button className="button secondary" onClick={() => stop()}>
              {busy ? "Cancel camera" : "Turn camera off"}
              <span aria-hidden="true">×</span>
            </button>
          ) : (
            <button className="button orange-button" onClick={enable}>
              Enable camera<span aria-hidden="true">↗</span>
            </button>
          )}
          {!busy && !live ? (
            <button
              className="mirror-sample-button"
              aria-pressed={sample}
              onClick={() => {
                setSample((v) => !v);
                setMessage("");
              }}
            >
              {sample ? "Still sample" : "Try a sample"}
              <span aria-hidden="true">{sample ? "Ⅱ" : "▷"}</span>
            </button>
          ) : null}
        </div>
        <p className="mirror-privacy">
          Only in your browser. No uploads. No microphone.
        </p>
        <div className="mirror-feedback" role="status" aria-live="polite">
          {message ||
            (busy
              ? "Choose whether to allow camera access in your browser. You can cancel at any time."
              : status === "paused"
                ? "Camera is off. Enable it whenever you're ready."
                : reduced
                  ? "Reduced motion: a calm pixel image, without raised tiles."
                  : "A small experiment. Your next idea starts with play.")}
        </div>
      </div>
      <div className="pixel-mirror-console">
        <div className="mirror-chrome">
          <span>PIXEL MIRROR</span>
          <span className={live ? "mirror-live" : ""}>
            <i />
            {live ? "LIVE" : "CAMERA OFF"}
          </span>
        </div>
        <div
          className="mirror-screen"
          role="img"
          aria-label={
            live
              ? "Your live camera, rendered locally as a pixel grid"
              : "Sample pixel world. Camera is off."
          }
        >
          {visible ? (
            <WebcamPixelGrid
              stream={stream}
              sampleMotion={sample && !reduced}
              paused={!visible}
              gridCols={density}
              gridRows={(density * 3) / 4}
              maxElevation={reduced ? 0 : density === 48 ? 3 : 5}
              motionSensitivity={0.38}
              elevationSmoothing={0.16}
              colorMode="monochrome"
              monochromeColor={color}
              backgroundColor="#19113E"
              mirror={!!stream}
              gapRatio={0.12}
              darken={0}
              invertColors={false}
              borderColor={color}
              borderOpacity={0.09}
              fps={reduced ? 12 : density === 48 ? 18 : 24}
              onWebcamReady={ready}
              onWebcamError={failed}
            />
          ) : null}
          <span className="mirror-screen-corner" aria-hidden="true">
            ＋
          </span>
        </div>
        <div className="mirror-toolbar">
          <span role="status">{statusText}</span>
          <div className="mirror-colors" role="group" aria-label="Pixel color">
            <button
              aria-label="Violet pixels"
              aria-pressed={color === "#8C76DB"}
              onClick={() => setColor("#8C76DB")}
            >
              <i style={{ background: "#8C76DB" }} />
            </button>
            <button
              aria-label="Lime pixels"
              aria-pressed={color === "#B6D935"}
              onClick={() => setColor("#B6D935")}
            >
              <i style={{ background: "#B6D935" }} />
            </button>
          </div>
        </div>
        <div className="mirror-caption">
          REAL WORLD <span>→</span> PIXEL WORLD <span>→</span> YOUR NEXT IDEA
        </div>
      </div>
    </section>
  );
}
