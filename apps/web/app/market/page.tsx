import type { Metadata } from "next";
import { MarketExperience } from "../../components/market-experience";
export const metadata: Metadata = { title: "Market — Plemu" };
export default function Market() {
  return <MarketExperience />;
}
