import type { Game } from "./games";
export type DemoTrade = {
  id: string;
  gameId: string;
  side: "buy" | "sell";
  amount: number;
  tokens: number;
  time: string;
};
export type Portfolio = {
  balance: number;
  holdings: Record<string, number>;
  trades: DemoTrade[];
};
export type TradeResult =
  | { ok: true; portfolio: Portfolio; trade: DemoTrade }
  | {
      ok: false;
      code:
        | "WALLET_DISCONNECTED"
        | "INVALID_AMOUNT"
        | "INSUFFICIENT_BALANCE"
        | "INSUFFICIENT_HOLDING";
      message: string;
    };
// This is a local simulation. Never use this state to authorize content or settle payments.
export function simulateTrade(
  current: Portfolio,
  game: Pick<Game, "id" | "symbol" | "price">,
  side: "buy" | "sell",
  amount: number,
  connected: boolean,
  meta: { id: string; time: string },
): TradeResult {
  if (!connected)
    return {
      ok: false,
      code: "WALLET_DISCONNECTED",
      message: "Connect the demo wallet to simulate a trade.",
    };
  const cents = Math.round(amount * 100);
  if (
    !Number.isFinite(amount) ||
    amount <= 0 ||
    amount > 1e8 ||
    cents < 1 ||
    Math.abs(amount * 100 - cents) > 0.000001
  )
    return {
      ok: false,
      code: "INVALID_AMOUNT",
      message:
        "Enter an amount greater than zero with at most two decimal places.",
    };
  const tokens = amount / game.price;
  const holding = current.holdings[game.id] ?? 0;
  if (side === "buy" && cents > Math.round(current.balance * 100))
    return {
      ok: false,
      code: "INSUFFICIENT_BALANCE",
      message: "Insufficient demo USDC balance. Try a smaller amount.",
    };
  if (side === "sell" && tokens > holding + 1e-9)
    return {
      ok: false,
      code: "INSUFFICIENT_HOLDING",
      message: `Insufficient ${game.symbol}. Your demo holding is ${holding.toFixed(2)} tokens.`,
    };
  const trade: DemoTrade = { ...meta, gameId: game.id, side, amount, tokens };
  return {
    ok: true,
    trade,
    portfolio: {
      balance:
        (Math.round(current.balance * 100) +
          (side === "buy" ? -cents : cents)) /
        100,
      holdings: {
        ...current.holdings,
        [game.id]: Math.max(0, holding + (side === "buy" ? tokens : -tokens)),
      },
      trades: [trade, ...current.trades].slice(0, 50),
    },
  };
}
