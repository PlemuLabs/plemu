"use client";
import { useEffect, useState, type CSSProperties } from "react";
import Link from "next/link";
import Image from "next/image";
import {
  creatorTemplates,
  isCreatorDraft,
  type CreatorDraft,
} from "../lib/creator-draft";
import { genres } from "../lib/games";
import { Magnetic } from "./motion";
import { CreationLinks } from "./create-hub";
import { PublicationTiers } from "./publication-tiers";
import { type CreationPath } from "../lib/creation-paths";
import { Icon } from "./icons";
import { useProduct } from "./product-provider";

export function CreateFeature() {
  return (
    <section className="create-feature">
      <div className="create-feature-visual">
        <div className="workbench-window">
          <div className="window-bar">
            <span>● ● ●</span>
            <span>YOUR_NEXT_BIG_IDEA.plemu</span>
            <Icon name="spark" size={15} />
          </div>
          <div className="builder-mosaic">
            <Image
              src="/art/echo.webp"
              alt="An original world waiting to be created"
              fill
              sizes="600px"
            />
            <span className="pixel-selection" />
            <span className="editor-cursor">↖ you, making something great</span>
          </div>
          <div className="editor-timeline">
            <i />
            <i />
            <i />
            <i />
            <i />
            <span>01 / THE BEGINNING</span>
          </div>
        </div>
        <span className="sticker create-sticker">
          MADE
          <br />
          BY YOU
          <br />✳
        </span>
      </div>
      <div className="create-feature-copy">
        <span className="eyebrow">03 / PLAYER. MEET CREATOR.</span>
        <h2>
          Your weird idea.
          <br />
          <span className="pixel-text">Our favorite kind.</span>
        </h2>
        <p>
          A game, a tiny universe, a collection with a point of view. Start with
          a spark. Give it a home.
        </p>
        <Magnetic className="create-magnetic">
          <Link href="/create" className="button primary">
            Make something yours <Icon name="arrow" size={18} />
          </Link>
        </Magnetic>
        <span className="interaction-hint">
          Start a concept. No wallet required.
        </span>
        <CreationLinks />
        <Link className="pixel-mirror-entry" href="/create#pixel-mirror">
          Try the Pixel Mirror <span aria-hidden="true">↗</span>
        </Link>
      </div>
    </section>
  );
}
export function CreateExperience({ path }: { path: CreationPath }) {
  const draftKey =
    path.id === "submit"
      ? "plemu-creator-concept-v1"
      : `plemu-creator-${path.id}-v1`;
  const [draft, setDraft] = useState<CreatorDraft>({
    version: 1,
    ...creatorTemplates[0],
    kind: path.kind,
    updatedAt: "",
  });
  const [step, setStep] = useState(0);
  const [loaded, setLoaded] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState("");
  const { notify } = useProduct();
  useEffect(() => {
    try {
      const raw = localStorage.getItem(draftKey);
      if (raw) {
        const parsed: unknown = JSON.parse(raw);
        if (isCreatorDraft(parsed)) setDraft(parsed);
        else {
          console.error("PLEMU_DRAFT_INVALID");
          setError(
            "The saved concept could not be read. Choose a starter to begin again.",
          );
        }
      }
    } catch (cause) {
      console.error("PLEMU_DRAFT_READ_FAILED", cause);
      setError(
        "Your saved concept could not be restored. You can still create a preview.",
      );
    }
    setLoaded(true);
  }, [draftKey]);
  function update(patch: Partial<CreatorDraft>) {
    setDraft((d) => ({ ...d, ...patch }));
    setSaved(false);
    setError("");
  }
  function goToPreview() {
    setStep(2);
    if (window.matchMedia("(max-width: 700px)").matches)
      requestAnimationFrame(() =>
        document.getElementById("concept-workbench")?.scrollIntoView({
          block: "start",
          behavior: window.matchMedia("(prefers-reduced-motion: reduce)")
            .matches
            ? "instant"
            : "smooth",
        }),
      );
  }
  function save() {
    if (!draft.title.trim() || !draft.description.trim()) {
      setError("Add a title and a short description before saving.");
      setStep(1);
      return;
    }
    try {
      const next = {
        ...draft,
        title: draft.title.trim(),
        description: draft.description.trim(),
        updatedAt: new Date().toISOString(),
      };
      localStorage.setItem(draftKey, JSON.stringify(next));
      setDraft(next);
      setSaved(true);
      notify("Concept saved on this browser. Nothing has been published.");
    } catch (cause) {
      console.error("PLEMU_DRAFT_WRITE_FAILED", cause);
      setError(
        "This browser could not save the concept. Keep this page open and try again.",
      );
    }
  }
  return (
    <div className="page-container create-page">
      <Link href="/create" className="workspace-back">
        ← All creation paths
      </Link>
      <div className="page-title">
        <div>
          <span className="eyebrow">{path.label} / CREATION WORKSPACE</span>
          <h1>{path.title}</h1>
          <p>{path.intro}</p>
        </div>
        <Link href="/creator" className="text-link">
          Advanced studio <Icon name="diagonal" size={16} />
        </Link>
      </div>
      <div className="creator-steps" aria-label="Concept steps">
        {["Find your spark", "Make it yours", "Meet your creation"].map(
          (label, i) => (
            <button
              key={label}
              className={step === i ? "active" : ""}
              aria-current={step === i ? "step" : undefined}
              onClick={() => setStep(i)}
            >
              <span>0{i + 1}</span>
              {label}
              <Icon name={i < step ? "check" : "arrow"} size={16} />
            </button>
          ),
        )}
      </div>
      <div
        id="concept-workbench"
        className={`creator-workbench ${step === 2 ? "show-preview" : ""}`}
      >
        <section className="creator-controls">
          <span className="eyebrow">STEP 0{step + 1}</span>
          <h2>
            {
              [
                "Start somewhere good.",
                "Add your signature.",
                "Look what you started.",
              ][step]
            }
          </h2>
          {step === 0 ? (
            <>
              <p>
                Use a starter as a moodboard. You can change the name, story,
                genre, and color next.
              </p>
              <div className="concept-kind">
                <button
                  aria-pressed={draft.kind === "game"}
                  onClick={() => update({ kind: "game" })}
                >
                  <Icon name="play" />
                  Game concept
                </button>
                <button
                  aria-pressed={draft.kind === "pack"}
                  onClick={() => update({ kind: "pack" })}
                >
                  <Icon name="grid" />
                  Pack concept
                </button>
              </div>
              <div className="template-list">
                {creatorTemplates.map((t) => (
                  <button
                    key={t.title}
                    aria-pressed={draft.artwork === t.artwork}
                    onClick={() => update(t)}
                  >
                    <Image src={t.artwork} alt="" width={84} height={64} />
                    <span>
                      <strong>{t.title}</strong>
                      <small>{t.genre} starter</small>
                    </span>
                    <Icon
                      name={draft.artwork === t.artwork ? "check" : "arrow"}
                      size={18}
                    />
                  </button>
                ))}
              </div>
              <button className="button primary" onClick={() => setStep(1)}>
                Make it yours <Icon name="arrow" size={16} />
              </button>
            </>
          ) : step === 1 ? (
            <>
              <label className="creator-field">
                Give it a name
                <input
                  value={draft.title}
                  maxLength={60}
                  onChange={(e) => update({ title: e.target.value })}
                />
              </label>
              <label className="creator-field">
                Choose a genre
                <select
                  value={draft.genre}
                  onChange={(e) => update({ genre: e.target.value })}
                >
                  {genres
                    .filter((g) => g !== "All games")
                    .map((g) => (
                      <option key={g}>{g}</option>
                    ))}
                </select>
              </label>
              <label className="creator-field">
                What’s the story?
                <textarea
                  value={draft.description}
                  maxLength={300}
                  rows={4}
                  onChange={(e) => update({ description: e.target.value })}
                />
                <small>{draft.description.length}/300</small>
              </label>
              <fieldset className="color-choices">
                <legend>Your signature color</legend>
                {["#b5dafa", "#ceee91", "#ff9a75", "#bdadf5"].map((c) => (
                  <button
                    key={c}
                    aria-label={`Use ${c} color`}
                    aria-pressed={draft.color === c}
                    style={{ background: c }}
                    onClick={() => update({ color: c })}
                  >
                    {draft.color === c ? "✓" : ""}
                  </button>
                ))}
              </fieldset>
              <button className="button primary" onClick={goToPreview}>
                Preview concept <Icon name="arrow" size={16} />
              </button>
            </>
          ) : (
            <>
              <p>
                Your concept is ready for a first look. Save it locally, come
                back to refine it, or bring your own game into the advanced
                studio.
              </p>
              <div className="creation-checklist">
                <span>
                  <Icon name="check" size={16} /> A home for your idea
                </span>
                <span>
                  <Icon name="check" size={16} /> Your visual identity
                </span>
                <span>
                  <Icon name="check" size={16} /> A starting point for your
                  community
                </span>
              </div>
              <button
                className="button primary"
                onClick={save}
                disabled={!loaded}
              >
                {saved ? "Saved on this browser" : "Save my concept"}
                <Icon name={saved ? "check" : "bookmark"} size={16} />
              </button>
              <Link href="/creator" className="text-link">
                Bring a real game to the studio{" "}
                <Icon name="diagonal" size={16} />
              </Link>
              <p className="local-note">
                This creates a visual concept only. Game generation, publishing,
                pack contents, and token creation are not connected.
              </p>
            </>
          )}
          {error ? (
            <p role="alert" className="creator-error">
              {error}
            </p>
          ) : null}
        </section>
        <section
          className="creation-preview"
          style={{ "--concept-color": draft.color } as CSSProperties}
        >
          <div className="preview-topline">
            <span>
              <i className="live-dot" /> LIVE CONCEPT PREVIEW
            </span>
            <span>01 / 01</span>
          </div>
          <div
            className={`concept-cartridge ${draft.kind === "pack" ? "concept-pack" : ""}`}
          >
            <div className="concept-hardware">
              <span>PLEMU®</span>
              <span>{draft.kind.toUpperCase()} CONCEPT</span>
            </div>
            <div className="concept-picture">
              <Image
                src={draft.artwork}
                alt="Selected concept moodboard"
                fill
                sizes="600px"
                priority
              />
              <span>{draft.genre}</span>
              <h2>{draft.title || "Your next big idea"}</h2>
            </div>
            <div className="concept-copy">
              <p>{draft.description || "Your story starts here."}</p>
              <span>✳ MADE BY YOU</span>
            </div>
          </div>
          <span className="preview-note">
            A spark today. A whole world tomorrow.
          </span>
        </section>
      </div>
      <PublicationTiers />
    </div>
  );
}
