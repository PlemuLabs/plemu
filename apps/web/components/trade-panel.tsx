"use client";
import { useState } from "react";
import { type Game, money } from "../lib/games";
import { Icon } from "./icons";
import { useProduct } from "./product-provider";
export function TradePanel({ game }: { game: Game }) {
  const { connected, openWallet, portfolio, transact } = useProduct();
  const [side, setSide] = useState<"buy" | "sell">("buy");
  const [amount, setAmount] = useState("");
  const [review, setReview] = useState(false);
  const [error, setError] = useState("");
  const [slippage, setSlippage] = useState("1");
  const value = Number(amount);
  const holding = portfolio.holdings[game.id] ?? 0;
  const available = side === "buy" ? portfolio.balance : holding * game.price;
  const tokens = Number.isFinite(value) && value > 0 ? value / game.price : 0;
  function fill(fraction: number) {
    setAmount((Math.floor(available * fraction * 100) / 100).toFixed(2));
    setReview(false);
    setError("");
  }
  function handleReview(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    if (!connected) {
      openWallet();
      return;
    }
    if (
      !/^\d+(\.\d{1,2})?$/.test(amount) ||
      !Number.isFinite(value) ||
      value <= 0
    ) {
      setError(
        "Enter an amount greater than zero with up to two decimal places.",
      );
      return;
    }
    if (value > available + 1e-9) {
      setError(
        `Insufficient demo ${side === "buy" ? "USDC balance" : game.symbol + " holdings"}. Try a smaller amount.`,
      );
      return;
    }
    setReview(true);
  }
  function confirm() {
    const result = transact(game, side, value);
    if (result.ok) {
      setReview(false);
      setAmount("");
      setError("");
    } else {
      setError(result.message);
      setReview(false);
    }
  }
  return (
    <section className={`trade-panel ${side === "sell" ? "selling" : ""}`}>
      <div className="trade-panel-title">
        <h3>Your next move</h3>
        <span>DEMO</span>
      </div>
      <div className="trade-toggle">
        <button
          className={side === "buy" ? "selected" : ""}
          aria-pressed={side === "buy"}
          onClick={() => {
            setSide("buy");
            setReview(false);
            setError("");
          }}
        >
          Buy
        </button>
        <button
          className={side === "sell" ? "selected" : ""}
          aria-pressed={side === "sell"}
          onClick={() => {
            setSide("sell");
            setReview(false);
            setError("");
          }}
        >
          Sell
        </button>
      </div>
      {review ? (
        <div className="trade-review">
          <Icon name="chart" size={30} />
          <h3>Review your demo {side}</h3>
          <p>
            {side === "buy" ? "Spend" : "Receive"}{" "}
            <strong>{money(value)} USDC</strong>
            <br />
            {side === "buy" ? "Receive" : "Sell"}{" "}
            <strong>
              {tokens.toFixed(2)} ${game.symbol}
            </strong>
          </p>
          <small>
            This updates your local demo portfolio. No transaction is sent and
            no real funds are used.
          </small>
          <button className="button primary full" onClick={confirm}>
            Confirm demo {side}
            <Icon name="check" size={16} />
          </button>
          <button
            className="button secondary full"
            onClick={() => setReview(false)}
          >
            Edit amount
          </button>
        </div>
      ) : (
        <form onSubmit={handleReview}>
          <div className="trade-balance">
            <span>
              {side === "buy" ? "Demo USDC balance" : "Your demo holding"}
            </span>
            <strong>
              {connected
                ? side === "buy"
                  ? money(portfolio.balance)
                  : `${holding.toFixed(2)} ${game.symbol}`
                : "—"}
            </strong>
          </div>
          <label className="amount-label" htmlFor={`amount-${game.id}`}>
            {side === "buy" ? "You pay" : "You receive"}
          </label>
          <div className="amount-input">
            <input
              id={`amount-${game.id}`}
              type="text"
              inputMode="decimal"
              autoComplete="off"
              placeholder="0.00"
              value={amount}
              onChange={(e) => {
                setAmount(e.target.value);
                setError("");
              }}
              aria-describedby={error ? "trade-error" : undefined}
            />
            <span>USDC</span>
          </div>
          <div className="amount-shortcuts">
            {[0.25, 0.5, 0.75, 1].map((v) => (
              <button key={v} type="button" onClick={() => fill(v)}>
                {v === 1 ? "MAX" : `${v * 100}%`}
              </button>
            ))}
          </div>
          <div className="quote-arrow">↓</div>
          <div className="trade-output">
            <span>
              {side === "buy" ? "You receive" : "You sell"}
              <strong>
                {tokens.toLocaleString("en-US", { maximumFractionDigits: 2 })}
              </strong>
            </span>
            <span>${game.symbol}</span>
          </div>
          <div className="trade-settings">
            <label>
              Slippage preview
              <select
                aria-label="Slippage preview"
                value={slippage}
                onChange={(e) => setSlippage(e.target.value)}
              >
                <option value="0.5">0.5%</option>
                <option value="1">1%</option>
                <option value="3">3%</option>
              </select>
            </label>
            <div>
              <span>Simulation fee</span>
              <strong>0.00 USDC</strong>
            </div>
            <div>
              <span>Price per ${game.symbol}</span>
              <strong>{money(game.price)}</strong>
            </div>
          </div>
          {error ? (
            <p id="trade-error" role="alert" className="trade-error">
              {error}
            </p>
          ) : null}
          <button className="button primary full" type="submit">
            {connected ? `Preview ${side}` : "Connect demo wallet"}
            <Icon name={connected ? "arrow" : "wallet"} size={16} />
          </button>
        </form>
      )}
      <p className="trade-disclosure">
        Simulation only. Quotes use a fixed sample price. Slippage is a preview
        preference; no order is routed.
      </p>
    </section>
  );
}
