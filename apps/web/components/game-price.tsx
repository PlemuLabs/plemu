import { type Game, money } from "../lib/games";

/** Always-visible quote shared by discovery, console, and collection previews. */
export function GamePrice({
  game,
  compact = false,
}: {
  game: Game;
  compact?: boolean;
}) {
  return (
    <span
      className={`game-quote ${compact ? "compact-quote" : ""}`}
      aria-label={`${game.symbol} token, ${money(game.price)}, ${game.change >= 0 ? "up" : "down"} ${Math.abs(game.change).toFixed(2)} percent in 24 hours`}
    >
      <span className="quote-symbol">${game.symbol}</span>
      <strong className="quote-value">{money(game.price)}</strong>
      <span className={game.change >= 0 ? "positive" : "negative"}>
        {game.change >= 0 ? "↗" : "↘"} {Math.abs(game.change).toFixed(2)}%{" "}
        <small>24h</small>
      </span>
    </span>
  );
}
