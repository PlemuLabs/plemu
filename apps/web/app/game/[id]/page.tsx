import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { games, findGame } from "../../../lib/games";
import { GameDetail } from "../../../components/game-detail";
export function generateStaticParams() {
  return games.map((g) => ({ id: g.id }));
}
export async function generateMetadata({
  params,
}: {
  params: Promise<{ id: string }>;
}): Promise<Metadata> {
  const { id } = await params;
  const game = findGame(id);
  return {
    title: game
      ? `${game.name} · $${game.symbol} — Plemu`
      : "World not found — Plemu",
  };
}
export default async function GamePage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const game = findGame(id);
  if (!game) notFound();
  return <GameDetail game={game} />;
}
