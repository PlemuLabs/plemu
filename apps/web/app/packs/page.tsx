import type { Metadata } from "next";
import { PacksExperience } from "../../components/packs-experience";
export const metadata: Metadata = { title: "Packs — Plemu" };
export default function Packs() {
  return <PacksExperience />;
}
