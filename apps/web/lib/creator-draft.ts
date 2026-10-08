export type CreatorDraft = {
  version: 1;
  title: string;
  genre: string;
  description: string;
  artwork: string;
  color: string;
  kind: "game" | "pack";
  updatedAt: string;
};
export const creatorTemplates = [
  {
    title: "A world of your own",
    description:
      "An unexplored place. An unexpected hero. A story only you can tell.",
    genre: "Adventure",
    artwork: "/art/echo.webp",
    color: "#b5dafa",
  },
  {
    title: "One more run",
    description:
      "A small arcade idea with a big heart. Easy to pick up. Hard to put down.",
    genre: "Arcade",
    artwork: "/art/rebels.webp",
    color: "#ff9a75",
  },
  {
    title: "The quiet quest",
    description:
      "Take the scenic route. Meet curious characters. Find a little magic in everyday things.",
    genre: "RPG",
    artwork: "/art/wildwood.webp",
    color: "#ceee91",
  },
];
export function isCreatorDraft(value: unknown): value is CreatorDraft {
  if (!value || typeof value !== "object") return false;
  const d = value as Record<string, unknown>;
  return (
    d.version === 1 &&
    typeof d.title === "string" &&
    d.title.length <= 60 &&
    typeof d.description === "string" &&
    d.description.length <= 300 &&
    typeof d.genre === "string" &&
    ["Adventure", "Action", "Racing", "Arcade", "RPG"].includes(d.genre) &&
    creatorTemplates.some((t) => t.artwork === d.artwork) &&
    typeof d.color === "string" &&
    /^#[a-f0-9]{6}$/i.test(d.color) &&
    (d.kind === "game" || d.kind === "pack") &&
    typeof d.updatedAt === "string"
  );
}
