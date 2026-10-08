"use client";
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
} from "react";
import { games, type Game } from "../lib/games";
import { packs } from "../lib/packs";
import {
  simulateTrade,
  type DemoTrade,
  type Portfolio,
} from "../lib/demo-economy";
import {
  createWalletGateway,
  type WalletIdentity,
} from "../lib/wallet-gateway";
const initial: Portfolio = {
  balance: 1000,
  holdings: { "starfall-protocol": 250, "moss-and-ember": 800 },
  trades: [],
};
type Product = {
  collectedPacks: string[];
  collectPack: (id: string) => void;
  saved: string[];
  toggleSaved: (id: string) => void;
  connected: boolean;
  connect: () => Promise<void>;
  disconnect: () => void;
  walletOpen: boolean;
  openWallet: () => void;
  closeWallet: () => void;
  portfolio: Portfolio;
  transact: (
    game: Game,
    side: "buy" | "sell",
    amount: number,
  ) => { ok: boolean; message: string };
  notify: (message: string) => void;
  notice: string;
};
const Context = createContext<Product | null>(null);
export function ProductProvider({ children }: { children: React.ReactNode }) {
  const [saved, setSaved] = useState<string[]>([]);
  const [collectedPacks, setCollectedPacks] = useState<string[]>([]);
  const [walletIdentity, setWalletIdentity] = useState<WalletIdentity | null>(
    null,
  );
  const gateway = useRef<ReturnType<typeof createWalletGateway> | null>(null);
  if (!gateway.current) gateway.current = createWalletGateway();
  // Demo balances/trades can never be authorized by a real wallet session.
  const connected = walletIdentity?.kind === "demo";
  useEffect(
    () => () => {
      void gateway.current
        ?.disconnect()
        .catch(() => console.error("PLEMU_WALLET_CLEANUP_FAILED"));
    },
    [],
  );
  const [walletOpen, setWalletOpen] = useState(false);
  const [portfolio, setPortfolio] = useState<Portfolio>(initial);
  const portfolioRef = useRef(portfolio);
  const [notice, setNotice] = useState("");
  const [ready, setReady] = useState(false);
  const notify = useCallback((message: string) => setNotice(message), []);
  useEffect(() => {
    try {
      const raw = localStorage.getItem("plemu-product-v1");
      if (raw) {
        const data = JSON.parse(raw);
        if (data.version === 1) {
          if (Array.isArray(data.collectedPacks))
            setCollectedPacks(
              data.collectedPacks.filter(
                (id: unknown) =>
                  typeof id === "string" && packs.some((p) => p.id === id),
              ),
            );
          if (Array.isArray(data.saved))
            setSaved(
              data.saved.filter(
                (id: unknown) =>
                  typeof id === "string" && games.some((g) => g.id === id),
              ),
            );
          const p = data.portfolio;
          if (
            p &&
            Number.isFinite(p.balance) &&
            p.balance >= 0 &&
            p.holdings &&
            typeof p.holdings === "object" &&
            Array.isArray(p.trades)
          ) {
            const holdings = Object.fromEntries(
              Object.entries(p.holdings).filter(
                (entry): entry is [string, number] => {
                  const [id, v] = entry;
                  return (
                    games.some((g) => g.id === id) &&
                    typeof v === "number" &&
                    Number.isFinite(v) &&
                    v >= 0
                  );
                },
              ),
            );
            const next = {
              balance: p.balance,
              holdings,
              trades: p.trades
                .filter(
                  (t: DemoTrade) =>
                    t &&
                    typeof t.id === "string" &&
                    games.some((g) => g.id === t.gameId) &&
                    (t.side === "buy" || t.side === "sell") &&
                    Number.isFinite(t.amount) &&
                    t.amount > 0 &&
                    Number.isFinite(t.tokens) &&
                    t.tokens > 0 &&
                    typeof t.time === "string",
                )
                .slice(0, 50),
            };
            setPortfolio(next);
            portfolioRef.current = next;
          }
        }
      }
    } catch (error) {
      console.error("PLEMU_STORAGE_READ_FAILED", error);
      notify("Your browser could not restore your local collection.");
    }
    setReady(true);
  }, [notify]);
  useEffect(() => {
    if (!ready) return;
    try {
      localStorage.setItem(
        "plemu-product-v1",
        JSON.stringify({ version: 1, saved, portfolio, collectedPacks }),
      );
    } catch (error) {
      console.error("PLEMU_STORAGE_WRITE_FAILED", error);
      notify(
        "Local storage is unavailable. Changes will last for this session.",
      );
    }
  }, [ready, saved, portfolio, collectedPacks, notify]);
  useEffect(() => {
    if (!notice) return;
    const timeout = setTimeout(() => setNotice(""), 5000);
    return () => clearTimeout(timeout);
  }, [notice]);
  const toggleSaved = useCallback(
    (id: string) =>
      setSaved((current) =>
        current.includes(id)
          ? current.filter((x) => x !== id)
          : [...current, id],
      ),
    [],
  );
  const transact = useCallback(
    (game: Game, side: "buy" | "sell", amount: number) => {
      const result = simulateTrade(
        portfolioRef.current,
        game,
        side,
        amount,
        connected,
        {
          id: crypto.randomUUID(),
          time: new Date().toISOString(),
        },
      );
      if (!result.ok) return result;
      portfolioRef.current = result.portfolio;
      setPortfolio(result.portfolio);
      const message = `Demo ${side} complete · ${result.trade.tokens.toFixed(2)} ${game.symbol}. No transaction was sent.`;
      notify(message);
      return { ok: true, message };
    },
    [connected, notify],
  );
  return (
    <Context.Provider
      value={{
        collectedPacks,
        collectPack: (id) => {
          const pack = packs.find((p) => p.id === id);
          if (!pack) {
            console.error("PLEMU_UNKNOWN_PACK", { id });
            notify("This pack is unavailable. Choose another collection.");
            return;
          }
          setCollectedPacks((current) => [...new Set([...current, id])]);
          setSaved((current) => [...new Set([...current, ...pack.gameIds])]);
          notify(
            `${pack.name} saved to your library. A local collection, no purchase required.`,
          );
        },
        saved,
        toggleSaved,
        connected,
        connect: async () => {
          const result = await gateway.current!.connect("demo");
          if (!result.ok) {
            notify(result.message);
            return;
          }
          setWalletIdentity(result.identity);
          notify("Demo wallet connected. No real wallet or funds are used.");
        },
        disconnect: () => {
          setWalletIdentity(null);
          void gateway.current!.disconnect().catch(() => {
            console.error("PLEMU_WALLET_DISCONNECT_FAILED");
            notify(
              "Wallet disconnected locally. Check the wallet app to revoke its session.",
            );
          });
          notify("Demo wallet disconnected.");
        },
        walletOpen,
        openWallet: () => setWalletOpen(true),
        closeWallet: () => setWalletOpen(false),
        portfolio,
        transact,
        notify,
        notice,
      }}
    >
      {children}
    </Context.Provider>
  );
}
export function useProduct() {
  const context = useContext(Context);
  if (!context) throw new Error("ProductProvider is required");
  return context;
}
