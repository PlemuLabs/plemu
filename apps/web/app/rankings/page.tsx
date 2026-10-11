import type { Metadata } from "next";
import { RankingsExperience } from "../../components/rankings-experience";
export const metadata: Metadata = { title: "Rankings — Plemu" };
export default function Rankings() {
  return <RankingsExperience />;
}
