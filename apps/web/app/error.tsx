"use client";
import { useEffect } from "react";
export default function ErrorPage({
  error,
  reset,
}: {
  error: Error;
  reset: () => void;
}) {
  useEffect(() => {
    console.error("PLEMU_RENDER_FAILED", error);
  }, [error]);
  return (
    <div className="page-container empty-state">
      <span className="eyebrow">SIGNAL INTERRUPTED</span>
      <h1>We lost the connection to this world.</h1>
      <p>Try loading the view again.</p>
      <button className="button primary" onClick={reset}>
        Try again
      </button>
    </div>
  );
}
