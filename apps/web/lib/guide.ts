export const guideChapters = [
  {
    id: "start",
    title: "Start here",
    icon: "globe",
    heading: "A little curiosity. A whole new world.",
    intro:
      "Plemu brings games, creators and game assets together. Start with something you want to play. Learn the rest as you go.",
  },
  {
    id: "discover",
    title: "Find your game",
    icon: "play",
    heading: "Find your kind of play.",
    intro:
      "A quiet adventure? One more arcade run? Browse by genre, follow a featured world, or search for a name you already know.",
  },
  {
    id: "tokens",
    title: "Games & tokens",
    icon: "spark",
    heading: "The game comes first.",
    intro:
      "A game can have an associated digital asset, called a token. It has its own name, price and activity. Think of it as another part of that game's world.",
  },
  {
    id: "market",
    title: "Read the market",
    icon: "chart",
    heading: "A few numbers. A clearer picture.",
    intro:
      "The Market brings game assets together. Open a game to see its price, chart and activity. Tap a number below to find out what it means.",
  },
  {
    id: "trading",
    title: "Try a trade",
    icon: "arrow",
    heading: "Learn the moves. Keep it pretend.",
    intro:
      "Choose a game, check its price, enter an amount and review what you would receive. This practice desk changes no wallet, balance or collection.",
  },
  {
    id: "packs",
    title: "Unwrap a pack",
    icon: "grid",
    heading: "Three worlds. One good feeling.",
    intro:
      "A pack is a curated collection of games that belong together. You can see every included world before you open it. No lucky draws.",
  },
  {
    id: "create",
    title: "Make something",
    icon: "code",
    heading: "Every world starts with a what-if.",
    intro:
      "Start with a collection, a game, a remix or an experiment. The current workspaces help you shape and save a concept. No wallet needed.",
  },
  {
    id: "library",
    title: "Your collection",
    icon: "bookmark",
    heading: "Keep the worlds that catch your eye.",
    intro:
      "Your Library is a shortcut back to the games and packs you save. Build a collection, filter it by genre and return whenever curiosity calls.",
  },
  {
    id: "rankings",
    title: "Follow the signals",
    icon: "trophy",
    heading: "Another way to find your next favorite.",
    intro:
      "Rankings help you discover games through different signals. A busy market, a popular world and a rising price tell different stories.",
  },
  {
    id: "wallet",
    title: "Meet your wallet",
    icon: "wallet",
    heading: "Your wallet. Your decisions.",
    intro:
      "A wallet is an app that helps you manage digital assets and approve actions involving them. You can browse, create concepts and save games here without one.",
  },
  {
    id: "journey",
    title: "Your next move",
    icon: "globe",
    heading: "You know the map. Pick a direction.",
    intro:
      "There isn't one right order. Follow a world, collect a few favorites, or start making something of your own.",
  },
] as const;
export type GuideChapter = (typeof guideChapters)[number]["id"];
export function isGuideChapter(value: string): value is GuideChapter {
  return guideChapters.some((chapter) => chapter.id === value);
}
export const guideDestinations = [
  {
    title: "Discover games",
    icon: "play",
    copy: "Find a world by genre, platform or mood.",
    href: "/games",
  },
  {
    title: "Create something",
    icon: "spark",
    copy: "Shape a pack, game, mod or experiment.",
    href: "/create",
  },
  {
    title: "Open a pack",
    icon: "grid",
    copy: "Unwrap a collection of three known worlds.",
    href: "/packs",
  },
  {
    title: "Explore an asset",
    icon: "globe",
    copy: "See how a game and its token fit together.",
    href: "/game/starfall-protocol",
  },
  {
    title: "Follow the market",
    icon: "chart",
    copy: "Compare prices, movement and activity.",
    href: "/market",
  },
  {
    title: "Try demo trading",
    icon: "arrow",
    copy: "Explore buying and selling with pretend funds.",
    href: "/game/starfall-protocol#economy",
  },
  {
    title: "Build a Library",
    icon: "bookmark",
    copy: "Save games and revisit your favorites.",
    href: "/library",
  },
  {
    title: "Explore rankings",
    icon: "trophy",
    copy: "Discover games through different signals.",
    href: "/rankings",
  },
];
