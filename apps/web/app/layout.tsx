import type { Metadata, Viewport } from "next";
import "@fontsource-variable/space-grotesk";
import "@fontsource/dm-sans/400.css";
import "@fontsource/dm-sans/500.css";
import "@fontsource/dm-sans/600.css";
import "@fontsource/dm-mono/400.css";
import "@fontsource/silkscreen/400.css";
import "@solana/wallet-adapter-react-ui/styles.css";
import "../components/wallet-adapter-retro.css";
import "./globals.css";
import "./play-system.css";
import "./handheld.css";
import { SolanaWalletProvider } from "../providers/SolanaWalletProvider";
import { AppShell } from "../components/app-shell";

export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  viewportFit: "cover",
  themeColor: "#121418",
};

export const metadata: Metadata = {
  title: "Plemu — Every game is an asset.",
  description:
    "Discover, play, create, and collect. A new home for games, creators, and curious people.",
  icons: { icon: "/brand/plemu-mark.svg", apple: "/brand/plemu-logo.png" },
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en" data-scroll-behavior="smooth">
      <body>
        <SolanaWalletProvider>
          <AppShell>{children}</AppShell>
        </SolanaWalletProvider>
      </body>
    </html>
  );
}
