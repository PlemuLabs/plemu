import type { Metadata } from "next";
import { CreateHub } from "../../components/create-hub";
export const metadata: Metadata = { title: "Create — Plemu" };
export default function Create() {
  return <CreateHub />;
}
