export type Monetization = "free" | "purchase" | "demo" | "pwyw";
export const modalities = [
  { title: "Gratuito", description: "Abra e jogue sem pagamento." },
  {
    title: "Compra individual",
    description: "Pague uma vez em USDC e desbloqueie o jogo na biblioteca.",
  },
  {
    title: "Demo + compra",
    description:
      "Experimente a demo gratuitamente e compre o jogo completo em USDC.",
  },
  {
    title: "Pague quanto quiser",
    description: "Escolha um valor em USDC, respeitando o mínimo do criador.",
  },
  {
    title: "Gorjeta",
    description: "Apoie um jogo gratuito com um pagamento voluntário em USDC.",
  },
];

// Amounts are integers in millionths; floating point is never used for prices.
export function parseUsdc(input: string): bigint | null {
  const normalized = input.trim().replace(",", ".");
  if (!/^\d{1,9}(\.\d{1,6})?$/.test(normalized)) return null;
  const [whole, fraction = ""] = normalized.split(".");
  return BigInt(whole) * 1_000_000n + BigInt(fraction.padEnd(6, "0"));
}

export function formatUsdc(units: bigint): string {
  const whole = units / 1_000_000n;
  const fraction = (units % 1_000_000n)
    .toString()
    .padStart(6, "0")
    .replace(/0+$/, "");
  return `${whole}${fraction ? `,${fraction}` : ""} USDC`;
}
