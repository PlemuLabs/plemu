"use client";
import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { TearPack } from "./tear-pack";
import { PackObject } from "./packs-experience";
import { CollectibleCardStack } from "./collectible-card-stack";
import { packs, packGames } from "../lib/packs";
import { GamePrice } from "./game-price";
import { Icon } from "./icons";

export function GuidePackDemo({
  reducedMotion = false,
}: {
  reducedMotion?: boolean;
}) {
  const [opened, setOpened] = useState(false);
  const result = useRef<HTMLDivElement>(null);
  const stage = useRef<HTMLDivElement>(null);
  const tried = useRef(false);
  useEffect(() => {
    if (opened) {
      result.current?.focus({ preventScroll: true });
      tried.current = true;
    } else if (tried.current)
      stage.current
        ?.querySelector<HTMLElement>(".guide-tear")
        ?.focus({ preventScroll: true });
  }, [opened]);
  const pack = packs[0],
    contents = packGames(pack);
  return (
    <div className="guide-pack-demo">
      <div className="guide-pack-stage" ref={stage}>
        {opened ? (
          <div
            ref={result}
            tabIndex={-1}
            aria-label="Pack opened. Three known worlds revealed."
          >
            <CollectibleCardStack
              games={contents}
              reducedMotion={reducedMotion}
            />
            <button
              className="text-link guide-pack-reset"
              onClick={() => setOpened(false)}
            >
              ↻ Wrap it up and try again
            </button>
          </div>
        ) : (
          <>
            <TearPack
              reducedMotion={reducedMotion}
              className="pack-open-object guide-tear"
              label="Practice opening Off the grid. Drag right or press Enter."
              onOpen={() => setOpened(true)}
            >
              <PackObject pack={pack} />
            </TearPack>
            <button
              className="button secondary"
              onClick={() => setOpened(true)}
            >
              Open without dragging <Icon name="spark" />
            </button>
          </>
        )}
      </div>
      <div className="guide-pack-instructions">
        <span className="eyebrow">OFF THE GRID / KNOWN CONTENTS</span>
        <h3>Here's what is inside.</h3>
        <div className="guide-pack-contents">
          {contents.map((game) => (
            <Link key={game.id} href={`/game/${game.id}`}>
              <strong>{game.name}</strong>
              <GamePrice game={game} compact />
            </Link>
          ))}
        </div>
        <ol className="guide-numbered">
          <li>
            <strong>Hold, drag right, release.</strong>
            <span>
              A short drag springs back. You can also use Enter or the Open
              button.
            </span>
          </li>
          <li>
            <strong>Meet the worlds.</strong>
            <span>
              Swipe a card to the back of the stack. Tap it to explore that
              game.
            </span>
          </li>
          <li>
            <strong>Keep exploring.</strong>
            <span>
              On the Packs page, reveal each card and choose Save pack to
              library to save the pack's games to your Library.
            </span>
          </li>
        </ol>
        <p className="guide-small">
          This lesson doesn't save anything. The foil HIT marks the highest
          sample token price in the pack; it is not a random rarity or reward.
          Opening a preview transfers no tokens and grants no game access.
        </p>
      </div>
    </div>
  );
}
