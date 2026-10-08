import type { Metadata } from "next";
import "@solana/wallet-adapter-react-ui/styles.css";
import "../components/wallet-adapter-retro.css";
import "./globals.css";
import { SolanaWalletProvider } from "../providers/SolanaWalletProvider";

export const metadata: Metadata = {
  title: "Plemu — Play. Create. Share.",
  description:
    "Um novo lugar para descobrir, criar e jogar games independentes no navegador.",
  icons: { icon: "/favicon.svg" },
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="pt-BR">
      <body>
        <SolanaWalletProvider>{children}</SolanaWalletProvider>
      </body>
    </html>
  );
}

