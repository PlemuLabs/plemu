export type Genre = "Adventure" | "Action" | "Racing" | "Arcade" | "RPG";
export type Game = {
  id: string;
  name: string;
  symbol: string;
  artwork: string;
  genre: Genre;
  platform: "Nintendo" | "PlayStation" | "Sega" | "PC" | "Arcade";
  price: number;
  change: number;
  volume: number;
  marketCap: number;
  rank: number;
  holders: number;
  players: number;
  added: number;
  color: string;
  studio: string;
  description: string;
  chart: number[];
  activity: Trade[];
  status: "Trending" | "New arrival" | "Community pick";
};
export type Trade = {
  id: string;
  side: "buy" | "sell";
  wallet: string;
  amount: number;
  tokens: number;
  minutes: number;
};
// Fictional catalog: platform assignments are sample metadata, not compatibility claims.
const seeds: Omit<Game, "chart" | "activity" | "rank">[] = [
  {
    id: "starfall-protocol",
    name: "Starfall Protocol",
    symbol: "STAR",
    artwork: "/art/starfall.webp",
    genre: "Adventure",
    platform: "PC",
    price: 0.0842,
    change: 24.68,
    volume: 842600,
    marketCap: 8420000,
    holders: 4821,
    players: 1284,
    added: 1,
    color: "#b7efb1",
    studio: "Orbit House",
    status: "Trending",
    description:
      "Somewhere beyond the last signal, a forgotten world is waiting. Explore ancient alien ruins, piece together a lost civilization, and find your way home in an atmospheric pixel adventure. Every expedition tells a different story.",
  },
  {
    id: "neon-ronin",
    name: "Neon Ronin",
    symbol: "RONIN",
    artwork: "/art/ronin.webp",
    genre: "Action",
    platform: "PlayStation",
    price: 0.0317,
    change: 18.42,
    volume: 621400,
    marketCap: 3170000,
    holders: 3290,
    players: 976,
    added: 2,
    color: "#f28a73",
    studio: "Redshift Games",
    status: "Trending",
    description:
      "One blade. A city that never sleeps. Master precision combat across the rain-soaked rooftops of a sprawling future metropolis. Your choices shape the city, and the city remembers.",
  },
  {
    id: "moss-and-ember",
    name: "Moss & Ember",
    symbol: "MOSS",
    artwork: "/art/moss.webp",
    genre: "RPG",
    platform: "Nintendo",
    price: 0.0156,
    change: 12.87,
    volume: 284100,
    marketCap: 1560000,
    holders: 2148,
    players: 842,
    added: 3,
    color: "#d8d690",
    studio: "Fern & Fable",
    status: "Community pick",
    description:
      "Follow a flickering lantern into a forest that has forgotten its own name. Befriend ancient guardians, craft curious tools, and uncover a gentle world of hidden stories. Take your time. The forest will wait.",
  },
  {
    id: "midnight-drift",
    name: "Midnight Drift",
    symbol: "DRIFT",
    artwork: "/art/drift.webp",
    genre: "Racing",
    platform: "PlayStation",
    price: 0.0624,
    change: -3.21,
    volume: 418700,
    marketCap: 6240000,
    holders: 3765,
    players: 1102,
    added: 4,
    color: "#d9a3f3",
    studio: "Afterhours",
    status: "Trending",
    description:
      "Chase the horizon on an endless coastal highway. Build your dream ride, find the perfect racing line, and lose yourself in a late-night soundtrack. No finish line. Just one more turn.",
  },
  {
    id: "void-runner",
    name: "Void Runner",
    symbol: "VOID",
    artwork: "/art/void.webp",
    genre: "Arcade",
    platform: "Arcade",
    price: 0.0089,
    change: 32.15,
    volume: 192300,
    marketCap: 890000,
    holders: 1638,
    players: 593,
    added: 12,
    color: "#8fc9ed",
    studio: "Zero Gravity",
    status: "New arrival",
    description:
      "Thread your ship through a collapsing universe. A fast, precise arcade challenge where every millisecond matters and every run brings you closer to the perfect score.",
  },
  {
    id: "ember-knights",
    name: "Ember Knights",
    symbol: "EMBER",
    artwork: "/art/ember.webp",
    genre: "Action",
    platform: "Nintendo",
    price: 0.0241,
    change: 8.64,
    volume: 173200,
    marketCap: 2410000,
    holders: 2890,
    players: 647,
    added: 5,
    color: "#eda96c",
    studio: "Fern & Fable",
    status: "Community pick",
    description:
      "Gather your party and descend into a shifting labyrinth. An original cooperative fantasy adventure filled with wild combinations, unexpected allies, and unforgettable boss battles.",
  },
  {
    id: "ghost-circuit",
    name: "Ghost Circuit",
    symbol: "GHOST",
    artwork: "/art/ghost.webp",
    genre: "Arcade",
    platform: "PC",
    price: 0.0053,
    change: -8.72,
    volume: 98600,
    marketCap: 530000,
    holders: 925,
    players: 301,
    added: 11,
    color: "#b0b4f0",
    studio: "Signal Lost",
    status: "New arrival",
    description:
      "A haunted arcade cabinet is sending signals from another dimension. Decode the patterns and outrun the ghosts in a surreal rhythm-driven adventure.",
  },
  {
    id: "sunset-rally",
    name: "Sunset Rally",
    symbol: "RALLY",
    artwork: "/art/rally.webp",
    genre: "Racing",
    platform: "Sega",
    price: 0.0198,
    change: 6.32,
    volume: 132800,
    marketCap: 1980000,
    holders: 1830,
    players: 528,
    added: 6,
    color: "#f0c285",
    studio: "Afterhours",
    status: "Community pick",
    description:
      "From sandy coastlines to mountain passes, race your friends through a world painted by the setting sun. Learn every shortcut and make every corner count.",
  },
  {
    id: "astral-echo",
    name: "Astral Echo",
    symbol: "ECHO",
    artwork: "/art/echo.webp",
    genre: "RPG",
    platform: "PlayStation",
    price: 0.0427,
    change: 5.91,
    volume: 214500,
    marketCap: 4270000,
    holders: 2460,
    players: 712,
    added: 7,
    color: "#96d8d1",
    studio: "Orbit House",
    status: "Community pick",
    description:
      "Memories are constellations in this turn-based space odyssey. Assemble a crew of unlikely heroes and journey between worlds in search of a signal from the past.",
  },
  {
    id: "wildwood",
    name: "Wildwood",
    symbol: "WILD",
    artwork: "/art/wildwood.webp",
    genre: "Adventure",
    platform: "Nintendo",
    price: 0.0112,
    change: -1.84,
    volume: 76300,
    marketCap: 1120000,
    holders: 1478,
    players: 436,
    added: 8,
    color: "#aecba0",
    studio: "Little Planet",
    status: "New arrival",
    description:
      "Make a home beneath the branches. Grow strange plants, trade stories with woodland neighbors, and build a little corner of a much bigger world.",
  },
  {
    id: "redline-zero",
    name: "Redline Zero",
    symbol: "ZERO",
    artwork: "/art/zero.webp",
    genre: "Action",
    platform: "Sega",
    price: 0.0076,
    change: 15.24,
    volume: 158900,
    marketCap: 760000,
    holders: 1120,
    players: 397,
    added: 10,
    color: "#e99090",
    studio: "Redshift Games",
    status: "New arrival",
    description:
      "Outrun the system in a high-speed action roguelite. Every escape changes the map. Upgrade your vehicle and find the gaps in an ever-evolving city.",
  },
  {
    id: "pixel-rebels",
    name: "Pixel Rebels",
    symbol: "REBEL",
    artwork: "/art/rebels.webp",
    genre: "Arcade",
    platform: "Arcade",
    price: 0.0362,
    change: 2.48,
    volume: 246700,
    marketCap: 3620000,
    holders: 3172,
    players: 805,
    added: 9,
    color: "#dfc99b",
    studio: "Signal Lost",
    status: "Community pick",
    description:
      "A pixel-perfect love letter to the arcade era. Battle through a hand-built city with a cast of unlikely heroes, inventive combos, and a soundtrack that refuses to sit still.",
  },
];
export const games: Game[] = seeds.map((g, index) => ({
  ...g,
  rank: index + 1,
  chart: Array.from({ length: 48 }, (_, i) =>
    Math.max(
      0.001,
      g.price *
        (1 / (1 + g.change / 100) +
          (i / 47) * (1 - 1 / (1 + g.change / 100)) +
          Math.sin((Math.PI * i) / 47) *
            (Math.sin(i * 1.7 + index) * 0.025 +
              Math.sin(i * 0.4 + index) * 0.04)),
    ),
  ),
  activity: Array.from({ length: 8 }, (_, i) => ({
    id: `${g.id}-${i}`,
    side: i % 3 === 1 ? "sell" : "buy",
    wallet: ["7xQp…2kF8", "Bm4R…9aT1", "Ds6N…4vW3", "3pZu…8mH2"][i % 4],
    amount: Number((37 + i * 28.6 + index * 11.2).toFixed(2)),
    tokens: Math.round((37 + i * 28.6 + index * 11.2) / g.price),
    minutes: i * 3 + 1,
  })),
}));
export const genres = [
  "All games",
  "Adventure",
  "Action",
  "RPG",
  "Racing",
  "Arcade",
] as const;
export function money(value: number, compact = false) {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
    notation: compact ? "compact" : "standard",
    maximumFractionDigits: compact ? 2 : value < 1 ? 4 : 2,
  }).format(value);
}
export function findGame(id: string) {
  return games.find((g) => g.id === id);
}
export type MarketSort =
  "trending" | "gainers" | "losers" | "volume" | "cap" | "new" | "popular";
export function sortGames(list: Game[], sort: MarketSort) {
  return [...list].sort((a, b) =>
    sort === "gainers"
      ? b.change - a.change
      : sort === "losers"
        ? a.change - b.change
        : sort === "volume"
          ? b.volume - a.volume
          : sort === "cap"
            ? b.marketCap - a.marketCap
            : sort === "new"
              ? b.added - a.added
              : sort === "popular"
                ? b.players - a.players
                : a.rank - b.rank,
  );
}
