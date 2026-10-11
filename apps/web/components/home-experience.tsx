"use client";
import Link from "next/link";
import { GamePrice } from "./game-price";
import Image from "next/image";
import { useState } from "react";
import { games, sortGames, type MarketSort } from "../lib/games";
import { MotionScene, Magnetic } from "./motion";
import { CartridgeBrowser } from "./cartridge";
import { Psg1Experience } from "./psg1-experience";
import { CreateFeature } from "./create-experience";
import { PackFeature } from "./packs-experience";
import { GameCard } from "./game-card";
import { SectionHeading } from "./section-heading";
import { Icon } from "./icons";
import { PixelMagnet } from "./pixel-magnet";
import { ArtworkLight } from "./artwork-light";

export function HomeExperience() {
  const [ranking, setRanking] = useState<MarketSort>("popular");
  return (
    <MotionScene>
      <div className="discovery-home">
        <section className="play-hero">
          <div className="hero-grid-paper" aria-hidden="true" />
          <PixelMagnet exclude=".play-hero-copy > *, .cartridge-slot, .cartridge-caption, .interaction-hint, .hero-stamp, .magnet-invitation" />
          <span className="magnet-invitation" aria-hidden="true">
            MOVE YOUR CURSOR · MAKE PIXELS PLAY
          </span>
          <div className="play-hero-copy">
            <span className="eyebrow">
              <span className="tiny-pixel">✳</span> A NEW HOME FOR YOUR NEXT
              OBSESSION
            </span>
            <h1>
              A new way to
              <br />
              <span className="pixel-text">own the game</span>
            </h1>
            <p>
              A new gaming economy where every game is a token. Collect, trade,
              and play on Solana.
            </p>
            <div className="button-row">
              <Magnetic>
                <Link className="button orange-button" href="/games">
                  Find your next game <Icon name="arrow" size={18} />
                </Link>
              </Magnetic>
              <Link className="hero-create-link" href="/create">
                Or make your own <span>↗</span>
              </Link>
            </div>
          </div>
          <div className="hero-cartridges">
            <CartridgeBrowser compact />
            <span className="hero-stamp">
              EVERY GAME
              <br />
              IS AN ASSET.
              <br />
              <b>
                EVERY PLAYER
                <br />
                BELONGS.
              </b>
            </span>
          </div>
        </section>
        <div className="page-container">
          <Psg1Experience />
          <CreateFeature />
          <PackFeature />
          <section className="section world-discovery">
            <SectionHeading
              eyebrow="05 / PICK YOUR KIND OF GOOD TIME"
              title="Different worlds. Same love of play."
              href="/games"
              link="All games"
            />
            <div className="world-editorials">
              {[games[2], games[3], games[1]].map((g, i) => (
                <Link
                  href={`/game/${g.id}`}
                  className="world-editorial"
                  key={g.id}
                >
                  {i === 0 ? (
                    <ArtworkLight>
                      <Image
                        src={g.artwork}
                        alt={`${g.name} pixel world`}
                        fill
                        sizes="(max-width: 700px) 90vw, 33vw"
                      />
                    </ArtworkLight>
                  ) : (
                    <Image
                      src={g.artwork}
                      alt={`${g.name} pixel world`}
                      fill
                      sizes="(max-width: 700px) 90vw, 33vw"
                    />
                  )}
                  <span className="editorial-index">
                    0{i + 1} / {g.genre}
                  </span>
                  <div className="editorial-caption">
                    <span className="eyebrow">
                      {
                        [
                          "TAKE THE SCENIC ROUTE",
                          "CHASE THE LAST LIGHT",
                          "JUST ONE MORE RUN",
                        ][i]
                      }
                    </span>
                    <h3>{g.name}</h3>
                    <GamePrice game={g} />
                    <span className="platform-badge">{g.platform}</span>
                    <span className="round-control">
                      <Icon name="diagonal" />
                    </span>
                  </div>
                </Link>
              ))}
            </div>
          </section>
          <section className="section home-game-market">
            <div className="market-intro">
              <div>
                <span className="eyebrow">06 / FIND YOUR NEXT FAVORITE</span>
                <h2>
                  Meet your next obsession.
                  <br />
                  <span className="pixel-text">Start with a game.</span>
                </h2>
              </div>
              <p>
                A world catches your eye? Get to know the game, check out its
                token, and save it for later. Pick a favorite and make yourself
                at home.
                <Link href="/market">
                  Explore the game market <Icon name="arrow" size={16} />
                </Link>
              </p>
            </div>
            <div className="section-tabs">
              {(
                [
                  ["popular", "Community favorites"],
                  ["trending", "Trending worlds"],
                  ["gainers", "Making moves"],
                  ["new", "New arrivals"],
                ] as [MarketSort, string][]
              ).map(([id, label]) => (
                <button
                  key={id}
                  aria-pressed={ranking === id}
                  className={ranking === id ? "selected" : ""}
                  onClick={() => setRanking(id)}
                >
                  {label}
                </button>
              ))}
              <Link href="/rankings" className="text-link">
                All rankings ↗
              </Link>
            </div>
            <div className="game-grid home-market-cards">
              {sortGames(games, ranking)
                .slice(0, 4)
                .map((game) => (
                  <GameCard key={game.id} game={game} />
                ))}
            </div>
            <p className="local-note">
              A preview of the Plemu economy. All games, prices, and activity
              shown are sample data.
            </p>
          </section>
          <section className="community-end">
            <span className="pixel-mascot" aria-hidden="true">
              ✳
            </span>
            <span className="eyebrow">PLAYER ONE IS YOU.</span>
            <h2>
              Come for a game.
              <br />
              <span className="pixel-text">Stay for your people.</span>
            </h2>
            <div className="button-row">
              <Link href="/library" className="button primary">
                Build your collection <Icon name="bookmark" size={16} />
              </Link>
              <Link href="/create" className="button secondary">
                Create something <Icon name="spark" size={16} />
              </Link>
            </div>
            <div className="community-notes">
              <span>12 original worlds</span>
              <span>3 curated packs</span>
              <span>Endless “what ifs”</span>
            </div>
          </section>
        </div>
      </div>
    </MotionScene>
  );
}
