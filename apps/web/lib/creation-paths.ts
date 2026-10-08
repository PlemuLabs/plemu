export const creationPaths = [
  {
    id: "pack",
    title: "Create a Pack",
    icon: "grid",
    label: "CURATE",
    description:
      "Bring worlds together. Make a collection with a point of view.",
    intro:
      "Shape your collection’s identity in the pack workspace. Start with a visual concept, then decide who it’s for.",
    kind: "pack",
  },
  {
    id: "submit",
    title: "Submit a Game",
    icon: "play",
    label: "PUBLISH",
    description: "Give your game a home and choose how to share it.",
    intro:
      "Prepare your game’s identity and description, then continue to the advanced studio for submission preparation.",
    kind: "game",
  },
  {
    id: "mod",
    title: "Mod a Game",
    icon: "spark",
    label: "REMIX",
    description:
      "Take a new direction. Plan a mod for a game you can build on.",
    intro:
      "Sketch a mod concept for a game you have permission to modify. This workspace captures your idea; it does not patch game files.",
    kind: "game",
  },
  {
    id: "lab",
    title: "AI Game Lab",
    icon: "code",
    label: "EXPERIMENT",
    description: "Start with a what-if. Explore the shape of your next game.",
    intro:
      "Develop your game brief and visual direction. This is a concept workspace; AI generation is not connected yet.",
    kind: "game",
  },
] as const;
export type CreationPath = (typeof creationPaths)[number];
export const publicationTiers = [
  {
    title: "Local",
    description:
      "Run your own content on your handheld. Nothing leaves your device.",
    reach: "ONLY YOU",
  },
  {
    title: "Private pack",
    description: "Share with your community through invited seats.",
    reach: "YOUR COMMUNITY",
  },
  {
    title: "Public beta",
    description: "Submit for legal review and open testing.",
    reach: "OPEN TESTING",
  },
  {
    title: "Public main",
    description: "Curated, featured and discoverable by everyone.",
    reach: "EVERYONE",
  },
];
