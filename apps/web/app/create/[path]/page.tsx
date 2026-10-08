import { notFound } from "next/navigation";
import { CreateExperience } from "../../../components/create-experience";
import { creationPaths } from "../../../lib/creation-paths";
export function generateStaticParams() {
  return creationPaths.map((path) => ({ path: path.id }));
}
export async function generateMetadata({
  params,
}: {
  params: Promise<{ path: string }>;
}) {
  const { path } = await params;
  return {
    title: `${creationPaths.find((p) => p.id === path)?.title ?? "Create"} — Plemu`,
  };
}
export default async function CreationWorkspace({
  params,
}: {
  params: Promise<{ path: string }>;
}) {
  const { path } = await params;
  const selected = creationPaths.find((p) => p.id === path);
  if (!selected) notFound();
  return <CreateExperience key={selected.id} path={selected} />;
}
