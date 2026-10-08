import { games } from "./games";

export type GamePack = {
  id: string;
  name: string;
  subtitle: string;
  description: string;
  color: string;
  edition: string;
  gameIds: string[];
};
export const packs: GamePack[] = [
  {
    id: "off-the-grid",
    name: "Off the grid",
    subtitle: "A little lost. A lot to find.",
    description:
      "Three worlds for the curious. Ancient signals, quiet forests, and stories hiding just beyond the map.",
    color: "#ceee91",
    edition: "EXPLORER SERIES / 01",
    gameIds: ["starfall-protocol", "moss-and-ember", "wildwood"],
  },
  {
    id: "after-hours",
    name: "After hours",
    subtitle: "For your one-more-run era.",
    description:
      "Rain-soaked rooftops, neon circuits, and the open road. A collection with a very late bedtime.",
    color: "#bdadf5",
    edition: "NIGHT SHIFT / 02",
    gameIds: ["neon-ronin", "ghost-circuit", "midnight-drift"],
  },
  {
    id: "insert-energy",
    name: "Insert energy",
    subtitle: "Small pixels. Big main-character energy.",
    description:
      "Fast fingers, fearless runs, and a little friendly chaos. Meet your next high-score obsession.",
    color: "#ff9a75",
    edition: "ARCADE CLUB / 03",
    gameIds: ["void-runner", "pixel-rebels", "redline-zero"],
  },
];
export function packGames(pack: GamePack) {
  return pack.gameIds.flatMap((id) => games.find((g) => g.id === id) ?? []);
}
