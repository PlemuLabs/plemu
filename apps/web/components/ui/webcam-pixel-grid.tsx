"use client";
import React, { useRef, useEffect, useState, useCallback } from "react";
// Adapted from Aceternity UI's public @aceternity/webcam-pixel-grid registry.
// Plemu changes: externally owned opt-in stream, local sample, bounded render loop,
// capped DPR, resize-only allocation and inline app-managed error states.

type WebcamPixelGridProps = {
  /** Camera is acquired only by the parent's explicit button handler. */
  stream?: MediaStream | null;
  poster?: string;
  sampleMotion?: boolean;
  paused?: boolean;
  fps?: number;
  /** Number of columns in the grid */
  gridCols?: number;
  /** Number of rows in the grid */
  gridRows?: number;
  /** Maximum elevation for motion detection */
  maxElevation?: number;
  /** Motion sensitivity (0-1) */
  motionSensitivity?: number;
  /** Smoothing factor for elevation transitions */
  elevationSmoothing?: number;
  /** Color mode: 'webcam' uses actual colors, 'monochrome' uses single color */
  colorMode?: "webcam" | "monochrome";
  /** Base color when in monochrome mode */
  monochromeColor?: string;
  /** Background color */
  backgroundColor?: string;
  /** Whether to mirror the webcam feed */
  mirror?: boolean;
  /** Gap between cells (0-1, fraction of cell size) */
  gapRatio?: number;
  /** Invert the colors */
  invertColors?: boolean;
  /** Darken factor (0-1, 0 = no darkening, 1 = fully dark) */
  darken?: number;
  /** Border color for cells */
  borderColor?: string;
  /** Border opacity (0-1) */
  borderOpacity?: number;
  /** Additional class name */
  className?: string;
  /** Callback when webcam access is denied */
  onWebcamError?: (error: Error) => void;
  /** Callback when webcam is ready */
  onWebcamReady?: () => void;
};

type PixelData = {
  r: number;
  g: number;
  b: number;
  motion: number;
  targetElevation: number;
  currentElevation: number;
};

export const WebcamPixelGrid: React.FC<WebcamPixelGridProps> = ({
  stream = null,
  poster = "/art/echo.webp",
  sampleMotion = false,
  paused = false,
  fps = 24,
  gridCols = 64,
  gridRows = 48,
  maxElevation = 15,
  motionSensitivity = 0.4,
  elevationSmoothing = 0.1,
  colorMode = "webcam",
  monochromeColor = "#8C76DB",
  backgroundColor = "#19113E",
  mirror = true,
  gapRatio = 0.1,
  invertColors = false,
  darken = 0,
  borderColor = "#8C76DB",
  borderOpacity = 0.08,
  className,
  onWebcamError,
  onWebcamReady,
}) => {
  const videoRef = useRef<HTMLVideoElement>(null);
  const processingCanvasRef = useRef<HTMLCanvasElement>(null);
  const displayCanvasRef = useRef<HTMLCanvasElement>(null);
  const previousFrameRef = useRef<Uint8ClampedArray | null>(null);
  const pixelDataRef = useRef<PixelData[][]>([]);
  const animationRef = useRef<number>(0);
  const [isReady, setIsReady] = useState(false);
  const posterRef = useRef<HTMLImageElement | null>(null);
  const lastFrameRef = useRef(0);

  // Parse monochrome color
  const monoRGB = React.useMemo(() => {
    const hex = monochromeColor.replace("#", "");
    return {
      r: parseInt(hex.slice(0, 2), 16),
      g: parseInt(hex.slice(2, 4), 16),
      b: parseInt(hex.slice(4, 6), 16),
    };
  }, [monochromeColor]);

  // Parse border color
  const borderRGB = React.useMemo(() => {
    const hex = borderColor.replace("#", "");
    return {
      r: parseInt(hex.slice(0, 2), 16),
      g: parseInt(hex.slice(2, 4), 16),
      b: parseInt(hex.slice(4, 6), 16),
    };
  }, [borderColor]);

  // Initialize pixel data
  useEffect(() => {
    pixelDataRef.current = Array.from({ length: gridRows }, () =>
      Array.from({ length: gridCols }, () => ({
        r: 30,
        g: 30,
        b: 30,
        motion: 0,
        targetElevation: 0,
        currentElevation: 0,
      })),
    );
  }, [gridCols, gridRows]);

  // This renderer never requests permission or owns camera tracks.
  useEffect(() => {
    let cancelled = false;
    const video = videoRef.current;
    setIsReady(false);
    previousFrameRef.current = null;
    if (stream && video) {
      video.srcObject = stream;
      video
        .play()
        .then(() => {
          if (!cancelled) {
            setIsReady(true);
            onWebcamReady?.();
          }
        })
        .catch((error: unknown) => {
          if (!cancelled)
            onWebcamError?.(
              error instanceof Error
                ? error
                : new Error("Video playback unavailable"),
            );
        });
    } else {
      const image = new Image();
      image.onload = () => {
        if (!cancelled) {
          posterRef.current = image;
          setIsReady(true);
        }
      };
      image.onerror = () => {
        if (!cancelled)
          onWebcamError?.(new Error("Sample artwork unavailable"));
      };
      image.src = poster;
    }
    return () => {
      cancelled = true;
      if (video) {
        video.pause();
        video.srcObject = null;
      }
    };
  }, [stream, poster, onWebcamError, onWebcamReady]);

  // Main render loop
  const render = useCallback(
    (now: number) => {
      if (paused) return;
      if (now - lastFrameRef.current < 1000 / fps) {
        animationRef.current = requestAnimationFrame(render);
        return;
      }
      lastFrameRef.current = now;
      const video = videoRef.current;
      const processingCanvas = processingCanvasRef.current;
      const displayCanvas = displayCanvasRef.current;

      const source = stream ? video : posterRef.current;
      if (
        !source ||
        !processingCanvas ||
        !displayCanvas ||
        (stream && (!video || video.readyState < 2))
      ) {
        animationRef.current = requestAnimationFrame(render);
        return;
      }

      const procCtx = processingCanvas.getContext("2d", {
        willReadFrequently: true,
      });
      const dispCtx = displayCanvas.getContext("2d");

      if (!procCtx || !dispCtx) {
        animationRef.current = requestAnimationFrame(render);
        return;
      }

      // Set processing canvas size to grid dimensions
      if (
        processingCanvas.width !== gridCols ||
        processingCanvas.height !== gridRows
      ) {
        processingCanvas.width = gridCols;
        processingCanvas.height = gridRows;
        previousFrameRef.current = null;
      }

      // The sample is explicitly labelled in the UI; it is never presented as a webcam.
      const drift =
        !stream && sampleMotion ? Math.sin(now / 950) * gridCols * 0.04 : 0;
      // Draw video to processing canvas (scaled down)
      procCtx.save();
      if (mirror) {
        procCtx.scale(-1, 1);
        procCtx.drawImage(
          source,
          -gridCols - drift - 3,
          -3,
          gridCols + 6,
          gridRows + 6,
        );
      } else {
        procCtx.drawImage(source, drift - 3, -3, gridCols + 6, gridRows + 6);
      }
      procCtx.restore();

      // Get pixel data
      const imageData = procCtx.getImageData(0, 0, gridCols, gridRows);
      const currentData = imageData.data;
      const previousData = previousFrameRef.current;

      // Update pixel data with motion detection
      const pixels = pixelDataRef.current;
      for (let row = 0; row < gridRows; row++) {
        for (let col = 0; col < gridCols; col++) {
          const idx = (row * gridCols + col) * 4;
          const r = currentData[idx];
          const g = currentData[idx + 1];
          const b = currentData[idx + 2];

          const pixel = pixels[row]?.[col];
          if (!pixel) continue;

          // Calculate motion
          let motion = 0;
          if (previousData) {
            const prevR = previousData[idx];
            const prevG = previousData[idx + 1];
            const prevB = previousData[idx + 2];
            const diff =
              Math.abs(r - prevR) + Math.abs(g - prevG) + Math.abs(b - prevB);
            motion = Math.min(
              1,
              diff / 255 / Math.max(0.05, motionSensitivity),
            );
          }

          // Smooth motion
          pixel.motion = pixel.motion * 0.7 + motion * 0.3;

          // Set colors
          let finalR = r;
          let finalG = g;
          let finalB = b;

          if (colorMode === "monochrome") {
            const brightness = (r + g + b) / 3 / 255;
            finalR = Math.round(monoRGB.r * brightness);
            finalG = Math.round(monoRGB.g * brightness);
            finalB = Math.round(monoRGB.b * brightness);
          }

          // Apply invert
          if (invertColors) {
            finalR = 255 - finalR;
            finalG = 255 - finalG;
            finalB = 255 - finalB;
          }

          // Apply darken
          if (darken > 0) {
            const darkenFactor = 1 - darken;
            finalR = Math.round(finalR * darkenFactor);
            finalG = Math.round(finalG * darkenFactor);
            finalB = Math.round(finalB * darkenFactor);
          }

          pixel.r = finalR;
          pixel.g = finalG;
          pixel.b = finalB;

          // Set target elevation
          pixel.targetElevation = pixel.motion * maxElevation;

          // Smooth elevation transition
          pixel.currentElevation +=
            (pixel.targetElevation - pixel.currentElevation) *
            elevationSmoothing;
        }
      }

      // Store current frame for next comparison
      previousFrameRef.current = currentData;

      // Render to display canvas
      const dpr = Math.min(1.5, window.devicePixelRatio || 1);
      const displayWidth = displayCanvas.clientWidth;
      const displayHeight = displayCanvas.clientHeight;

      const pixelWidth = Math.round(displayWidth * dpr);
      const pixelHeight = Math.round(displayHeight * dpr);
      if (
        displayCanvas.width !== pixelWidth ||
        displayCanvas.height !== pixelHeight
      ) {
        displayCanvas.width = pixelWidth;
        displayCanvas.height = pixelHeight;
      }
      dispCtx.setTransform(dpr, 0, 0, dpr, 0, 0);

      // Clear canvas
      dispCtx.fillStyle = backgroundColor;
      dispCtx.fillRect(0, 0, displayWidth, displayHeight);

      // Calculate cell size (always square, cover entire container like object-fit: cover)
      const cellSize = Math.max(
        displayWidth / gridCols,
        displayHeight / gridRows,
      );
      const gap = cellSize * gapRatio;

      // Calculate offset to center the grid (negative offset for overflow, creating cover effect)
      const gridWidth = cellSize * gridCols;
      const gridHeight = cellSize * gridRows;
      const offsetXGrid = (displayWidth - gridWidth) / 2;
      const offsetYGrid = (displayHeight - gridHeight) / 2;

      // Draw cells with 3D effect
      for (let row = 0; row < gridRows; row++) {
        for (let col = 0; col < gridCols; col++) {
          const pixel = pixels[row]?.[col];
          if (!pixel) continue;

          const x = offsetXGrid + col * cellSize;
          const y = offsetYGrid + row * cellSize;
          const elevation = pixel.currentElevation;

          // Calculate 3D offset (isometric-like projection) - MUCH larger effect
          const offsetX = -elevation * 1.2;
          const offsetY = -elevation * 1.8;

          // Draw shadow - larger and more visible
          if (elevation > 0.5) {
            dispCtx.fillStyle = `rgba(0, 0, 0, ${Math.min(0.6, elevation * 0.04)})`;
            dispCtx.fillRect(
              x + gap / 2 + elevation * 1.5,
              y + gap / 2 + elevation * 2.0,
              cellSize - gap,
              cellSize - gap,
            );
          }

          // Draw side faces for 3D effect - thicker sides
          if (elevation > 0.5) {
            // Right side
            dispCtx.fillStyle = `rgb(${Math.max(0, pixel.r - 80)}, ${Math.max(0, pixel.g - 80)}, ${Math.max(0, pixel.b - 80)})`;
            dispCtx.beginPath();
            dispCtx.moveTo(
              x + cellSize - gap / 2 + offsetX,
              y + gap / 2 + offsetY,
            );
            dispCtx.lineTo(x + cellSize - gap / 2, y + gap / 2);
            dispCtx.lineTo(x + cellSize - gap / 2, y + cellSize - gap / 2);
            dispCtx.lineTo(
              x + cellSize - gap / 2 + offsetX,
              y + cellSize - gap / 2 + offsetY,
            );
            dispCtx.closePath();
            dispCtx.fill();

            // Bottom side
            dispCtx.fillStyle = `rgb(${Math.max(0, pixel.r - 50)}, ${Math.max(0, pixel.g - 50)}, ${Math.max(0, pixel.b - 50)})`;
            dispCtx.beginPath();
            dispCtx.moveTo(
              x + gap / 2 + offsetX,
              y + cellSize - gap / 2 + offsetY,
            );
            dispCtx.lineTo(x + gap / 2, y + cellSize - gap / 2);
            dispCtx.lineTo(x + cellSize - gap / 2, y + cellSize - gap / 2);
            dispCtx.lineTo(
              x + cellSize - gap / 2 + offsetX,
              y + cellSize - gap / 2 + offsetY,
            );
            dispCtx.closePath();
            dispCtx.fill();
          }

          // Draw top face (main cell) - brighter when elevated
          const brightness = 1 + elevation * 0.05;
          dispCtx.fillStyle = `rgb(${Math.min(255, Math.round(pixel.r * brightness))}, ${Math.min(255, Math.round(pixel.g * brightness))}, ${Math.min(255, Math.round(pixel.b * brightness))})`;
          dispCtx.fillRect(
            x + gap / 2 + offsetX,
            y + gap / 2 + offsetY,
            cellSize - gap,
            cellSize - gap,
          );

          // Draw light border around top face
          dispCtx.strokeStyle = `rgba(${borderRGB.r}, ${borderRGB.g}, ${borderRGB.b}, ${borderOpacity + elevation * 0.008})`;
          dispCtx.lineWidth = 0.5;
          dispCtx.strokeRect(
            x + gap / 2 + offsetX,
            y + gap / 2 + offsetY,
            cellSize - gap,
            cellSize - gap,
          );
        }
      }

      if (stream || sampleMotion)
        animationRef.current = requestAnimationFrame(render);
    },
    [
      stream,
      sampleMotion,
      paused,
      fps,
      gridCols,
      gridRows,
      mirror,
      motionSensitivity,
      colorMode,
      monoRGB,
      maxElevation,
      elevationSmoothing,
      backgroundColor,
      gapRatio,
      invertColors,
      darken,
      borderRGB,
      borderOpacity,
    ],
  );

  // Start render loop when ready
  useEffect(() => {
    if (!isReady || paused) return;

    lastFrameRef.current = 0;
    animationRef.current = requestAnimationFrame(render);
    const observer = new ResizeObserver(() => {
      cancelAnimationFrame(animationRef.current);
      lastFrameRef.current = 0;
      animationRef.current = requestAnimationFrame(render);
    });
    if (displayCanvasRef.current) observer.observe(displayCanvasRef.current);
    return () => {
      cancelAnimationFrame(animationRef.current);
      observer.disconnect();
    };
  }, [isReady, paused, render]);

  return (
    <div className={`webcam-pixel-grid ${className ?? ""}`} aria-hidden="true">
      <video ref={videoRef} className="pixel-grid-source" playsInline muted />
      <canvas ref={processingCanvasRef} className="pixel-grid-source" />
      <canvas
        ref={displayCanvasRef}
        className="pixel-grid-display"
        style={{ backgroundColor, opacity: isReady ? 1 : 0 }}
      />
    </div>
  );
};

export default WebcamPixelGrid;
