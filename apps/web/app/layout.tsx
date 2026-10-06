import type { Metadata } from "next";
import "./globals.css";

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
      <body>{children}</body>
    </html>
  );
}
