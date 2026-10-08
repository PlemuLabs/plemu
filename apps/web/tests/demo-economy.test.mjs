import test from "node:test";
import assert from "node:assert/strict";
import { simulateTrade } from "../lib/demo-economy.ts";
const game = { id: "test-world", symbol: "TEST", price: 0.25 };
const initial = { balance: 100, holdings: { "test-world": 8 }, trades: [] };
const meta = { id: "trade-1", time: "2026-10-08T00:00:00Z" };
test("buy debits cents and credits tokens without mutating the input", () => {
  const result = simulateTrade(initial, game, "buy", 1.01, true, meta);
  assert.equal(result.ok, true);
  assert.equal(result.portfolio.balance, 98.99);
  assert.equal(result.portfolio.holdings[game.id], 12.04);
  assert.equal(result.portfolio.trades[0].id, meta.id);
  assert.equal(initial.balance, 100);
  assert.equal(initial.holdings[game.id], 8);
});
test("sell credits the balance and can liquidate a full holding", () => {
  const result = simulateTrade(initial, game, "sell", 2, true, meta);
  assert.equal(result.ok, true);
  assert.equal(result.portfolio.balance, 102);
  assert.equal(result.portfolio.holdings[game.id], 0);
});
test("disconnected demo wallet fails closed", () => {
  assert.equal(
    simulateTrade(initial, game, "buy", 1, false, meta).code,
    "WALLET_DISCONNECTED",
  );
});
test("invalid and excessive precision amounts are rejected", () => {
  for (const amount of [0, -1, NaN, Infinity, 0.001, 1.999, 1e9])
    assert.equal(
      simulateTrade(initial, game, "buy", amount, true, meta).code,
      "INVALID_AMOUNT",
    );
});
test("a buy cannot overdraft the balance", () => {
  assert.equal(
    simulateTrade(initial, game, "buy", 100.01, true, meta).code,
    "INSUFFICIENT_BALANCE",
  );
});
test("a sell cannot exceed holdings", () => {
  assert.equal(
    simulateTrade(initial, game, "sell", 2.01, true, meta).code,
    "INSUFFICIENT_HOLDING",
  );
});
test("sequential trades use the latest portfolio and preserve cents", () => {
  const buy = simulateTrade(initial, game, "buy", 0.1, true, meta);
  const sell = simulateTrade(buy.portfolio, game, "sell", 0.1, true, {
    ...meta,
    id: "trade-2",
  });
  assert.equal(sell.portfolio.balance, 100);
  assert.equal(sell.portfolio.holdings[game.id], 8);
  assert.equal(sell.portfolio.trades.length, 2);
});
test("trade history stays bounded at 50 newest records", () => {
  const current = {
    ...initial,
    trades: Array.from({ length: 50 }, (_, i) => ({ ...meta, id: String(i) })),
  };
  const result = simulateTrade(current, game, "buy", 1, true, meta);
  assert.equal(result.portfolio.trades.length, 50);
  assert.equal(result.portfolio.trades[0].id, meta.id);
});
