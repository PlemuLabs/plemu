"use client";
import Link from "next/link";
import { GamePrice } from "./game-price";
import Image from "next/image";
import { usePathname } from "next/navigation";
import { useEffect, useState } from "react";
import { ProductProvider, useProduct } from "./product-provider";
import { Dialog } from "./dialog";
import { Icon } from "./icons";
import { WalletHeaderButton } from "./WalletHeaderButton";
import { games, money } from "../lib/games";
const navigation = [
  { href: "/", label: "Discover" },
  { href: "/market", label: "Market" },
  { href: "/create", label: "Create" },
  { href: "/packs", label: "Packs" },
  { href: "/games", label: "Games" },
  { href: "/rankings", label: "Ranking" },
  { href: "/library", label: "Library" },
  { href: "/guide", label: "Guide" },
];
export function Logo() {
  return (
    <span className="logo">
      <Image
        className="brand-mark"
        src="/brand/plemu-mark.svg"
        width={34}
        height={34}
        alt=""
        aria-hidden="true"
        priority
      />
      <span>
        plemu<span className="logo-dot">®</span>
      </span>
    </span>
  );
}
export function AppShell({ children }: { children: React.ReactNode }) {
  return (
    <ProductProvider>
      <Shell>{children}</Shell>
    </ProductProvider>
  );
}
function Shell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const activePath = pathname.startsWith("/game/") ? "/games" : pathname;
  const [searchOpen, setSearchOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [mobileOpen, setMobileOpen] = useState(false);
  const { connected, openWallet, notice } = useProduct();
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (
        e.key === "/" &&
        !(e.target instanceof HTMLInputElement) &&
        !(e.target instanceof HTMLTextAreaElement)
      ) {
        e.preventDefault();
        setSearchOpen(true);
      }
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, []);
  useEffect(() => {
    setMobileOpen(false);
    setSearchOpen(false);
  }, [pathname]);
  const results = games.filter((g) =>
    `${g.name} ${g.symbol} ${g.genre}`
      .toLowerCase()
      .includes(search.toLowerCase()),
  );
  return (
    <>
      <a className="skip-link" href="#main">
        Skip to content
      </a>
      <header className="site-header">
        <Link href="/" aria-label="Plemu home">
          <Logo />
        </Link>
        <nav className="desktop-nav" aria-label="Main navigation">
          {navigation.map((n) => (
            <Link
              href={n.href}
              key={n.href}
              className={`${n.href === "/guide" ? "guide-nav-link " : ""}${
                (
                  n.href === "/"
                    ? activePath === "/"
                    : activePath.startsWith(n.href)
                )
                  ? "active"
                  : ""
              }`}
              aria-current={
                (
                  n.href === "/"
                    ? activePath === "/"
                    : activePath.startsWith(n.href)
                )
                  ? "page"
                  : undefined
              }
            >
              {n.label}
              {n.label === "Create" ? <span className="nav-dot" /> : null}
            </Link>
          ))}
        </nav>
        <div className="header-actions">
          <button
            className="search-trigger"
            onClick={() => setSearchOpen(true)}
            aria-label="Search games and tokens"
          >
            <Icon name="search" />
            <span>Search</span>
            <kbd>/</kbd>
          </button>
          <button className="wallet-button" onClick={openWallet}>
            <Icon name="wallet" size={16} />
            <span>{connected ? "Demo wallet" : "Wallet"}</span>
          </button>
          <button
            className="icon-button mobile-menu"
            aria-label="Toggle navigation"
            aria-expanded={mobileOpen}
            onClick={() => setMobileOpen((v) => !v)}
          >
            <Icon name={mobileOpen ? "close" : "menu"} />
          </button>
        </div>
      </header>
      <nav className="handheld-nav" aria-label="Handheld navigation">
        {[
          { href: "/", label: "Discover", icon: "globe" },
          { href: "/games", label: "Games", icon: "play" },
          { href: "/create", label: "Create", icon: "spark" },
          { href: "/packs", label: "Packs", icon: "grid" },
        ].map((n) => {
          const active =
            n.href === "/" ? activePath === "/" : activePath.startsWith(n.href);
          return (
            <Link
              key={n.href}
              href={n.href}
              aria-current={active ? "page" : undefined}
            >
              <Icon name={n.icon} size={21} />
              <span>{n.label}</span>
            </Link>
          );
        })}
        <button
          onClick={() => setMobileOpen(true)}
          aria-label="More destinations"
          aria-expanded={mobileOpen}
          className={
            ["/market", "/rankings", "/library", "/guide"].includes(activePath)
              ? "active"
              : ""
          }
        >
          <Icon name="menu" size={21} />
          <span>More</span>
        </button>
      </nav>
      <Dialog
        open={mobileOpen}
        onClose={() => setMobileOpen(false)}
        title="Your Plemu"
      >
        <nav className="handheld-menu" aria-label="All destinations">
          {navigation.map((n) => (
            <Link
              key={n.href}
              href={n.href}
              aria-current={
                (
                  n.href === "/"
                    ? activePath === "/"
                    : activePath.startsWith(n.href)
                )
                  ? "page"
                  : undefined
              }
              onClick={() => setMobileOpen(false)}
            >
              {n.label}
              <Icon name="arrow" />
            </Link>
          ))}
        </nav>
        <button
          className="button secondary full"
          onClick={() => {
            setMobileOpen(false);
            openWallet();
          }}
        >
          <Icon name="wallet" />
          {connected ? "Your demo wallet" : "Wallet"}
        </button>
      </Dialog>
      <main id="main" className="page-enter" key={pathname}>
        {children}
      </main>
      <footer className="site-footer">
        <div>
          <Link href="/">
            <Logo />
          </Link>
          <p>Good games. Great company.</p>
        </div>
        <nav>
          <Link href="/games">Games</Link>
          <Link href="/packs">Packs</Link>
          <Link href="/market">Market</Link>
          <Link href="/library">Your collection</Link>
          <Link href="/guide">Guide</Link>
          <Link href="/create">
            Create <Icon name="diagonal" size={12} />
          </Link>
        </nav>
        <div className="footer-note">
          <span>
            <i className="live-dot" /> Built for Solana
          </span>
          <small>© 2026 Plemu Labs · V0 / all market data simulated</small>
        </div>
      </footer>
      <Dialog
        open={searchOpen}
        onClose={() => setSearchOpen(false)}
        title="Find your next world"
        wide
      >
        <label className="dialog-search">
          <Icon name="search" />
          <input
            autoFocus
            aria-label="Search games and tokens"
            placeholder="Search a game, token, or genre"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </label>
        <div className="search-results">
          {results.length ? (
            results.map((g) => (
              <Link
                key={g.id}
                href={`/game/${g.id}`}
                onClick={() => setSearchOpen(false)}
              >
                <Image src={g.artwork} alt="" width={56} height={40} />
                <span>
                  <strong>{g.name}</strong>
                  <small>
                    ${g.symbol} · {g.genre}
                  </small>
                  <GamePrice game={g} compact />
                </span>
                <Icon name="diagonal" size={16} />
              </Link>
            ))
          ) : (
            <p className="empty-copy">
              No worlds found. Try a different name or token.
            </p>
          )}
        </div>
      </Dialog>
      <WalletDialog />
      {notice ? (
        <div className="toast" role="status">
          <Icon name="spark" size={18} />
          {notice}
        </div>
      ) : null}
    </>
  );
}
function WalletDialog() {
  const { walletOpen, closeWallet, connected, connect, disconnect, portfolio } =
    useProduct();
  const total =
    portfolio.balance +
    games.reduce(
      (sum, g) => sum + (portfolio.holdings[g.id] ?? 0) * g.price,
      0,
    );
  return (
    <Dialog
      open={walletOpen}
      onClose={closeWallet}
      title={connected ? "Your demo wallet" : "Enter the arcade economy"}
    >
      <div className="wallet-graphic">
        <Icon name="wallet" size={36} />
        <span className="wallet-orbit" />
      </div>
      <h2>{connected ? "Your next move." : "A world worth collecting."}</h2>
      <p className="muted">
        {connected
          ? "Your simulated portfolio, all in one place."
          : "Explore the full trading experience with a local demo wallet. No extension needed."}
      </p>
      <div className="demo-note">
        <span className="live-dot" /> DEMO MODE · No real funds or transactions
      </div>
      {connected ? (
        <>
          <div className="wallet-total">
            <small>PORTFOLIO VALUE</small>
            <strong>{money(total)}</strong>
            <span>{money(portfolio.balance)} demo USDC available</span>
          </div>
          <div className="wallet-holdings">
            {games
              .filter((g) => (portfolio.holdings[g.id] ?? 0) > 0)
              .map((g) => (
                <Link href={`/game/${g.id}`} key={g.id} onClick={closeWallet}>
                  <Image src={g.artwork} alt="" width={40} height={40} />
                  <span>
                    {g.name}
                    <small>
                      {(portfolio.holdings[g.id] ?? 0).toFixed(2)} ${g.symbol}{" "}
                      held
                    </small>
                    <GamePrice game={g} compact />
                  </span>
                  <strong>
                    {money((portfolio.holdings[g.id] ?? 0) * g.price)}
                  </strong>
                </Link>
              ))}
          </div>
          <WalletHeaderButton onConnect={closeWallet} />
          <button
            className="button secondary full"
            onClick={() => {
              disconnect();
              closeWallet();
            }}
          >
            Disconnect demo wallet
          </button>
        </>
      ) : (
        <>
          <button
            className="button primary full"
            onClick={async () => {
              await connect();
              closeWallet();
            }}
          >
            Connect demo wallet <Icon name="arrow" />
          </button>
          <div className="wallet-provider-note">
            <strong>Solana wallet · Devnet</strong>
            <small>
              Connect a real wallet to view its Devnet balances. Demo trades
              remain local and never use real funds.
            </small>
            <WalletHeaderButton onConnect={closeWallet} />
          </div>
          <p className="wallet-help">
            Starts with 1,000 demo USDC and two example holdings.
            <br />
            Solana connection is separate from the demo portfolio.
          </p>
        </>
      )}
    </Dialog>
  );
}
