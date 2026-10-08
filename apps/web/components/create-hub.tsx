import Link from "next/link";
import { creationPaths } from "../lib/creation-paths";
import { Icon } from "./icons";
import { PixelMirror } from "./pixel-mirror";
import { PublicationTiers } from "./publication-tiers";
export function CreationLinks() {
  return (
    <nav className="creation-path-links" aria-label="Creation paths">
      {creationPaths.map((path) => (
        <Link href={`/create/${path.id}`} key={path.id}>
          {path.title}
          <span aria-hidden="true">↗</span>
        </Link>
      ))}
    </nav>
  );
}
export function CreateHub() {
  return (
    <div className="page-container create-hub">
      <header className="create-hub-heading">
        <span className="eyebrow">PLAY · COLLECT · TRADE · CREATE</span>
        <h1>
          What will you<span className="pixel-text">make next?</span>
        </h1>
        <p>
          Curate a collection. Share a game. Remix a world. Start an experiment.
          Pick your path — give your idea a place to grow.
        </p>
      </header>
      <nav className="creation-paths" aria-label="Choose what to create">
        {creationPaths.map((path, i) => (
          <Link
            href={`/create/${path.id}`}
            key={path.id}
            className={`creation-path path-${path.id}`}
          >
            <span className="path-icon">
              <Icon name={path.icon} size={28} />
            </span>
            <div>
              <span className="eyebrow">
                0{i + 1} / {path.label}
              </span>
              <h2>{path.title}</h2>
              <p>{path.description}</p>
            </div>
            <span className="path-arrow" aria-hidden="true">
              ↗
            </span>
          </Link>
        ))}
      </nav>
      <div className="create-flow">
        <span>Choose a path</span>
        <span aria-hidden="true">→</span>
        <span>Make it yours</span>
        <span aria-hidden="true">→</span>
        <a href="#publication-heading">Choose your reach ↓</a>
      </div>
      <PixelMirror />
      <PublicationTiers />
      <div className="hub-studio-link">
        <span>Already working on something?</span>
        <Link href="/creator">Open advanced studio ↗</Link>
      </div>
    </div>
  );
}
