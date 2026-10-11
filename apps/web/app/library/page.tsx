import type { Metadata } from "next";
import { LibraryExperience } from "../../components/library-experience";
export const metadata: Metadata = { title: "Library — Plemu" };
export default function Library() {
  return <LibraryExperience collection />;
}
