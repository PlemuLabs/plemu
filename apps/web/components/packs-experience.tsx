"use client";
import { useEffect, useRef, useState, type CSSProperties } from "react";
import Link from "next/link";
import { TearPack } from "./tear-pack";
import { GamePrice } from "./game-price";
import Image from "next/image";
import { packs, packGames, type GamePack } from "../lib/packs";
import { CollectibleCardStack } from "./collectible-card-stack";
import { CollectibleGameCard, packHit } from "./collectible-game-card";
import { Icon } from "./icons";
import { useProduct } from "./product-provider";

function PackArtwork({ pack }: { pack: GamePack }) {
  const contents = packGames(pack);
  return (
    <div
      className="pack-object"
      style={{ "--pack-color": pack.color } as CSSProperties}
    >
      <div className="pack-seal" />
      <div className="pack-object-top">
        <span>PLEMU®</span>
        <span>COLLECT / 03</span>
      </div>
      <div className="pack-art">
        <Image src={contents[0].artwork} alt="" fill sizes="360px" />
        <div className="pack-art-shade" />
        <span className="pack-edition">{pack.edition}</span>
        <strong>{pack.name}</strong>
      </div>
      <div className="pack-object-bottom">
        <span>
          THREE WORLDS.
          <br />
          ONE GOOD FEELING.
        </span>
        <span className="pack-pixel">✳</span>
      </div>
      <div className="pack-seal bottom" />
    </div>
  );
}
export function PackObject({ pack }: { pack: GamePack }) {
  return (
    <div className="pack-sleeve">
      <div className="pack-inside" aria-hidden="true">
        <Image src={packGames(pack)[0].artwork} alt="" fill sizes="285px" />
      </div>
      <div className="pack-torn-base">
        <PackArtwork pack={pack} />
      </div>
      <div className="pack-torn-top" aria-hidden="true">
        <PackArtwork pack={pack} />
      </div>
    </div>
  );
}
export function PackFeature() {
  const [opened, setOpened] = useState(false);
  const revealFocus = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (opened) revealFocus.current?.focus({ preventScroll: true });
  }, [opened]);
  return (
    <section className="pack-feature" id="pack-club">
      <div className="pack-feature-copy">
        <span className="eyebrow">04 / GOOD THINGS COME IN PACKS</span>
        <h2>
          Open a pack.
          <br />
          <span className="pixel-text">Find your people.</span>
        </h2>
        <p>
          A mixtape of worlds that belong together. Curated collections to open,
          explore, and make your own.
        </p>
        <Link href="/packs" className="button dark-button">
          Explore packs <Icon name="arrow" />
        </Link>
        <span className="pack-promise">
          Known contents. No lucky draws. Just great worlds.
        </span>
      </div>
      {opened ? (
        <div
          className="home-pack-reveal"
          aria-live="polite"
          ref={revealFocus}
          tabIndex={-1}
        >
          <span className="eyebrow">THREE WORLDS. YOUR NEXT ADVENTURE.</span>
          <CollectibleCardStack games={packGames(packs[2])} />
          <button className="text-link" onClick={() => setOpened(false)}>
            ↻ Open again
          </button>
        </div>
      ) : (
        <div className="home-pack-interaction">
          <TearPack
            className="pack-fan"
            label="Tear open the Plemu pack preview. Drag right or press Enter."
            onOpen={() => setOpened(true)}
          >
            {packs.map((p) => (
              <div key={p.id}>
                <PackObject pack={p} />
              </div>
            ))}
            <span className="sticker">
              GOOD
              <br />
              THINGS
              <br />
              INSIDE ↗
            </span>
          </TearPack>
          <button
            className="text-link pack-keyboard-open"
            onClick={() => setOpened(true)}
          >
            Open pack <Icon name="spark" size={14} />
          </button>
        </div>
      )}
    </section>
  );
}
export function PacksExperience() {
  const [selected, setSelected] = useState(0);
  const [opened, setOpened] = useState(false);
  const revealFocus = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (opened) revealFocus.current?.focus({ preventScroll: true });
  }, [opened]);
  const [revealed, setRevealed] = useState(0);
  const { collectedPacks, collectPack } = useProduct();
  const pack = packs[selected];
  const contents = packGames(pack);
  const collected = collectedPacks.includes(pack.id);
  const complete = revealed === contents.length;
  return (
    <div className="page-container packs-page">
      <div className="page-title">
        <div>
          <span className="eyebrow">THE PLEMU PACK CLUB</span>
          <h1>
            Good things.
            <br />
            <span className="pixel-text">Better together.</span>
          </h1>
          <p>
            Curated worlds, ready to unwrap. Pick a pack and see what’s inside.
          </p>
        </div>
        <span className="club-stamp">
          100%
          <br />
          GOOD
          <br />
          PIXELS
        </span>
      </div>
      <div className="pack-selector" aria-label="Choose a pack">
        {packs.map((p, i) => (
          <button
            key={p.id}
            aria-pressed={selected === i}
            style={{ "--pack-color": p.color } as CSSProperties}
            onClick={() => {
              setSelected(i);
              setOpened(false);
              setRevealed(0);
            }}
          >
            <span>0{i + 1}</span>
            {p.name}
            <Icon
              name={collectedPacks.includes(p.id) ? "check" : "spark"}
              size={16}
            />
          </button>
        ))}
      </div>
      <section
        className={`pack-opening ${opened ? "is-open" : ""}`}
        style={{ "--pack-color": pack.color } as CSSProperties}
      >
        <div className="pack-theatre" key={pack.id}>
          {!opened ? (
            <TearPack
              className="pack-open-object"
              label={`Tear open ${pack.name}. Drag right or press Enter.`}
              onOpen={() => {
                setOpened(true);
                setRevealed(1);
              }}
            >
              <PackObject pack={pack} />
            </TearPack>
          ) : complete ? (
            <CollectibleCardStack games={contents} />
          ) : (
            <div className="single-pack-reveal" key={contents[revealed - 1].id}>
              <CollectibleGameCard
                game={contents[revealed - 1]}
                hit={contents[revealed - 1].id === packHit(contents)?.id}
              />
              <span className="edition-label">
                CURATED / {revealed} OF {contents.length}
              </span>
            </div>
          )}
        </div>
        <div className="pack-details">
          <span className="eyebrow">{pack.edition} · FREE PREVIEW</span>
          <h2>{pack.name}</h2>
          <h3>{pack.subtitle}</h3>
          <p>{pack.description}</p>
          <div className="pack-contents">
            <span className="eyebrow">EVERY PACK CONTAINS THESE 3 WORLDS</span>
            {contents.map((g, i) => (
              <Link key={g.id} href={`/game/${g.id}`}>
                <span>{opened && i < revealed ? "✓" : "0" + (i + 1)}</span>
                <Image src={g.artwork} alt="" width={46} height={34} />
                <strong>
                  {g.name}
                  <small>
                    {g.genre} · {g.platform}
                  </small>
                  <GamePrice game={g} compact />
                </strong>
                <Icon name="diagonal" size={15} />
              </Link>
            ))}
          </div>
          <div
            className="pack-progress"
            role="status"
            ref={revealFocus}
            tabIndex={-1}
          >
            {opened
              ? complete
                ? "All three worlds revealed. Make them part of your collection."
                : `World ${revealed} of ${contents.length} · ${contents[revealed - 1].name}`
              : "No surprises about what you get. Only where it takes you."}
          </div>
          {!opened ? (
            <button
              className="button primary"
              onClick={() => {
                setOpened(true);
                setRevealed(1);
              }}
            >
              Open this pack <Icon name="spark" size={16} />
            </button>
          ) : !complete ? (
            <button
              className="button primary"
              onClick={() =>
                setRevealed((v) => Math.min(v + 1, contents.length))
              }
            >
              Reveal next world <Icon name="arrow" size={16} />
            </button>
          ) : (
            <div className="button-row">
              <button
                className="button primary"
                onClick={() => collectPack(pack.id)}
                disabled={collected}
              >
                {collected ? "In your library" : "Save pack to library"}
                <Icon name={collected ? "check" : "bookmark"} size={16} />
              </button>
              <Link className="text-link" href="/library">
                View library <Icon name="arrow" size={16} />
              </Link>
            </div>
          )}
          {opened ? (
            <button
              className="text-link replay-pack"
              onClick={() => {
                setOpened(false);
                setRevealed(0);
              }}
            >
              ↻ Open again
            </button>
          ) : null}
          <p className="local-note">
            A free, local collection of concept games. No payment, random
            rewards, token allocation, or game access is included.
          </p>
        </div>
      </section>
      <div className="pack-footer-note">
        <Icon name="users" size={24} />
        <div>
          <h3>Someone’s perfect collection starts with you.</h3>
          <p>Have a world in mind? Give it a home in Plemu.</p>
        </div>
        <Link href="/create/pack" className="button secondary">
          Create your own pack <Icon name="arrow" size={16} />
        </Link>
      </div>
    </div>
  );
}
