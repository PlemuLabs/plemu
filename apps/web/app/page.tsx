"use client";

import { useEffect, useRef, useState } from "react";
import { parseUsdc, formatUsdc, type Monetization } from "./commerce";

const games = [
  {
    id: "orbit",
    title: "Neon Orbit",
    genre: "Arcade",
    color: "mint",
    symbol: "✦",
    description:
      "Desvie dos meteoros e encontre seu ritmo no espaço. Um mini game feito para testar o player do Plemu.",
    playable: true,
    monetization: "free" as Monetization,
    price: 0n,
    tips: true,
  },
  {
    id: "forest",
    title: "Moonlit Forest",
    genre: "Aventura",
    color: "purple",
    symbol: "☾",
    description:
      "Uma floresta, muitos caminhos. Conceito de jogo para demonstrar o catálogo.",
    playable: false,
    monetization: "purchase" as Monetization,
    price: 5_000_000n,
    tips: false,
  },
  {
    id: "quest",
    title: "Pixel Quest",
    genre: "RPG",
    color: "amber",
    symbol: "⚔",
    description:
      "Uma nova jornada em cada pixel. Conceito de jogo para demonstrar o catálogo.",
    playable: false,
    monetization: "demo" as Monetization,
    price: 8_000_000n,
    tips: false,
  },
  {
    id: "wave",
    title: "Cyber Wave",
    genre: "Corrida",
    color: "blue",
    symbol: "≋",
    description:
      "Entre no ritmo da cidade. Conceito de jogo para demonstrar o catálogo.",
    playable: false,
    monetization: "pwyw" as Monetization,
    price: 2_000_000n,
    tips: false,
  },
];
type Game = (typeof games)[number];
const priceLabel = (game: Game) =>
  game.monetization === "free"
    ? "GRÁTIS"
    : `${game.monetization === "pwyw" ? "A PARTIR DE " : ""}${formatUsdc(game.price)}`;
type View = "home" | "discover" | "packs" | "create" | "library";

export default function Home() {
  const [view, setView] = useState<View>("home");
  const [filter, setFilter] = useState("Todos");
  const [query, setQuery] = useState("");
  const [saved, setSaved] = useState<string[]>([]);
  const [ready, setReady] = useState(false);
  const [selected, setSelected] = useState<Game | null>(null);
  const [playing, setPlaying] = useState(false);
  const [walletOpen, setWalletOpen] = useState(false);
  const [notice, setNotice] = useState("");
  const [checkout, setCheckout] = useState<"purchase" | "tip" | null>(null);
  const [amount, setAmount] = useState("");
  const dialog = useRef<HTMLDialogElement>(null);
  const walletDialog = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    try {
      const stored: unknown = JSON.parse(
        localStorage.getItem("plemu-library") || "[]",
      );
      if (Array.isArray(stored))
        setSaved(
          stored.filter(
            (id): id is string =>
              typeof id === "string" && games.some((g) => g.id === id),
          ),
        );
    } catch {
      setNotice("Não foi possível recuperar sua biblioteca neste navegador.");
    }
    setReady(true);
  }, []);
  useEffect(() => {
    if (ready) {
      try {
        localStorage.setItem("plemu-library", JSON.stringify(saved));
      } catch {
        setNotice(
          "Seu navegador não permitiu salvar a biblioteca neste dispositivo.",
        );
      }
    }
  }, [saved, ready]);
  useEffect(() => {
    if (selected) dialog.current?.showModal();
    else dialog.current?.close();
  }, [selected]);
  useEffect(() => {
    if (walletOpen) walletDialog.current?.showModal();
    else walletDialog.current?.close();
  }, [walletOpen]);
  const openGame = (game: Game) => {
    setPlaying(false);
    setCheckout(null);
    setAmount(game.price ? (Number(game.price) / 1_000_000).toString() : "1");
    setSelected(game);
  };
  const navigate = (next: View) => {
    setView(next);
    setNotice("");
    window.scrollTo({ top: 0, behavior: "smooth" });
  };
  const visible = games.filter(
    (g) =>
      (view !== "library" || saved.includes(g.id)) &&
      (filter === "Todos" || g.genre === filter) &&
      g.title.toLowerCase().includes(query.toLowerCase()),
  );
  return (
    <>
      <header className="header">
        <button
          className="brand"
          onClick={() => navigate("home")}
          aria-label="Plemu início"
        >
          <span className="brand-icon">
            <i />
            <b />
            <em />
          </span>
          <span>PLEMU</span>
        </button>
        <nav aria-label="Navegação principal">
          {(
            [
              ["home", "HOME"],
              ["discover", "DISCOVER"],
              ["packs", "PACKS"],
              ["create", "CREATE"],
              ["library", "LIBRARY"],
            ] as [View, string][]
          ).map(([id, label]) => (
            <button
              key={id}
              className={view === id ? "active" : ""}
              aria-current={view === id ? "page" : undefined}
              onClick={() =>
                id === "create"
                  ? window.location.assign("/creator")
                  : navigate(id)
              }
            >
              {label}
            </button>
          ))}
        </nav>
        <div className="header-status">
          <span className="build-status">■ WEB ALPHA</span>
          <button className="wallet" onClick={() => setWalletOpen(true)}>
            CONECTAR CARTEIRA
          </button>
        </div>
      </header>
      <main>
        {view === "home" && (
          <>
            <section className="hero" aria-labelledby="hero-title">
              <div
                className="hero-art"
                role="img"
                aria-label="Paisagem noturna em pixel art com console roxo, montanhas e uma lua crescente"
              />
              <div className="hero-content">
                <div className="eyebrow">
                  <span className="online-dot" /> PLAYER 01{" "}
                  <span className="level">LV. 01</span>
                </div>
                <h1 id="hero-title">
                  PLAY.
                  <br />
                  <span>CREATE.</span>
                  <br />
                  SHARE.
                </h1>
                <p>
                  Seu próximo universo começa aqui.
                  <br />
                  Descubra jogos indie, encontre sua comunidade
                  <br className="desktop-break" /> e jogue direto no navegador.
                </p>
                <div className="hero-actions">
                  <button
                    className="button primary"
                    onClick={() => openGame(games[0])}
                  >
                    PRESS START <span>▶</span>
                  </button>
                  <button
                    className="button secondary"
                    onClick={() => navigate("discover")}
                  >
                    SELECT GAME
                  </button>
                </div>
                <div className="hero-facts">
                  <div>
                    <strong>HTML5</strong>
                    <span>JOGUE NA WEB</span>
                  </div>
                  <div>
                    <strong>INDIE</strong>
                    <span>CRIE SEU MUNDO</span>
                  </div>
                  <div>
                    <strong>NO DOWNLOAD</strong>
                    <span>É SÓ DAR PLAY</span>
                  </div>
                </div>
              </div>
              <span className="hero-caption">
                A NEW WORLD IS LOADING<span>01 / WEB ERA</span>
              </span>
            </section>
            <div className="ticker" aria-hidden="true">
              <span>PLAY IN YOUR BROWSER</span>
              <i>✦</i>
              <span>INDEPENDENT WORLDS</span>
              <i>✦</i>
              <span>MADE BY CREATORS</span>
              <i>✦</i>
              <span>POWERED BY YOU</span>
            </div>
          </>
        )}
        {(view === "home" || view === "discover" || view === "library") && (
          <section className="catalog section" aria-labelledby="catalog-title">
            <div className="section-heading">
              <div>
                <span className="eyebrow purple-text">
                  {view === "library"
                    ? "YOUR COLLECTION"
                    : "CHOOSE YOUR NEXT ADVENTURE"}
                </span>
                <h2 id="catalog-title">
                  {view === "library"
                    ? "Sua biblioteca"
                    : view === "discover"
                      ? "Descubra seu próximo jogo"
                      : "Pequenos jogos. Grandes mundos."}
                </h2>
              </div>
              {view === "home" && (
                <button
                  className="text-button"
                  onClick={() => navigate("discover")}
                >
                  EXPLORAR CATÁLOGO +
                </button>
              )}
            </div>
            <div className="catalog-note">
              <span>CATÁLOGO DEMONSTRATIVO</span> Neon Orbit é jogável. Os
              outros títulos são conceitos para esta prévia.
            </div>
            {view !== "home" && (
              <div className="catalog-tools">
                <div className="filters" aria-label="Filtrar por gênero">
                  {["Todos", "Arcade", "Aventura", "RPG", "Corrida"].map(
                    (f) => (
                      <button
                        key={f}
                        aria-pressed={f === filter}
                        onClick={() => setFilter(f)}
                      >
                        {f}
                      </button>
                    ),
                  )}
                </div>
                <input
                  type="search"
                  value={query}
                  onChange={(e) => setQuery(e.target.value)}
                  placeholder="Buscar um jogo…"
                  aria-label="Buscar um jogo"
                />
              </div>
            )}
            <div className="game-grid">
              {visible.map((game, index) => (
                <button
                  className="game-card"
                  key={game.id}
                  onClick={() => openGame(game)}
                >
                  <div className={`game-cover ${game.color}`}>
                    <span className="cover-index">0{index + 1} / PLEMU</span>
                    <span className="cover-symbol">{game.symbol}</span>
                    <span className="cover-title">
                      {game.title.toUpperCase()}
                    </span>
                    <span className="cover-tag">
                      {game.playable ? "JOGAR AGORA" : "CONCEITO"}
                    </span>
                  </div>
                  <div className="game-info">
                    <div>
                      <h3>{game.title}</h3>
                      <span>{game.genre} · Web</span>
                    </div>
                    <span className={game.playable ? "price free" : "price"}>
                      {priceLabel(game)}
                    </span>
                  </div>
                </button>
              ))}
            </div>
            {visible.length === 0 && (
              <div className="empty">
                <h3>
                  {view === "library" && saved.length === 0
                    ? "Sua primeira aventura está esperando."
                    : "Nenhum jogo encontrado."}
                </h3>
                <p>
                  {view === "library" && saved.length === 0
                    ? "Abra um jogo no catálogo e adicione à sua biblioteca neste dispositivo."
                    : "Experimente outro nome ou gênero."}
                </p>
                <button
                  className="button primary"
                  onClick={() => {
                    setQuery("");
                    setFilter("Todos");
                    navigate("discover");
                  }}
                >
                  EXPLORAR JOGOS
                </button>
              </div>
            )}
          </section>
        )}
        {view === "packs" && (
          <section className="section feature-page">
            <span className="eyebrow purple-text">BETTER TOGETHER</span>
            <h1>
              Colecione
              <br />
              <span>novos mundos.</span>
            </h1>
            <p>
              Packs vão reunir jogos em coleções organizadas por criadores e
              comunidades.
            </p>
            <div className="coming-soon">
              <span className="level">EM BREVE</span>
              <h2>Uma coleção. Muitas aventuras.</h2>
              <p>
                Packs estão fora do escopo inicial de vendas. Nesta versão, você
                já pode explorar o jogo de demonstração.
              </p>
              <button
                className="button primary"
                onClick={() => navigate("discover")}
              >
                VER CATÁLOGO
              </button>
            </div>
          </section>
        )}
        {notice && (
          <p className="notice" role="status">
            {notice}
          </p>
        )}
      </main>
      <footer>
        <button className="brand footer-brand" onClick={() => navigate("home")}>
          PLEMU<span className="purple-text">✦</span>
        </button>
        <span>PLAY. CREATE. SHARE.</span>
        <small>Prévia web · Plemu Labs · 2026</small>
      </footer>
      <dialog
        ref={dialog}
        className="game-dialog"
        onCancel={() => {
          setSelected(null);
          setPlaying(false);
        }}
        onClose={() => {
          setSelected(null);
          setPlaying(false);
        }}
      >
        {selected && (
          <>
            <div className="dialog-heading">
              <div>
                <span className="eyebrow purple-text">
                  {playing ? "NOW PLAYING" : selected.genre.toUpperCase()}
                </span>
                <h2>{selected.title}</h2>
              </div>
              <button
                className="close-button"
                onClick={() => {
                  setSelected(null);
                  setPlaying(false);
                }}
                aria-label="Fechar jogo"
              >
                ✕
              </button>
            </div>
            {playing ? (
              <>
                <iframe
                  title="Neon Orbit — jogo de demonstração"
                  src="/games/neon-orbit.html"
                  sandbox="allow-scripts"
                  allow="fullscreen"
                  className="player-frame"
                />
                <p className="player-help">
                  Use ← → ou A/D. No celular, toque nos controles. O jogo recebe
                  foco ao clicar na área.
                </p>
              </>
            ) : (
              <>
                <div className={`dialog-cover game-cover ${selected.color}`}>
                  <span className="cover-symbol">{selected.symbol}</span>
                  <span className="cover-title">
                    {selected.title.toUpperCase()}
                  </span>
                </div>
                <p>{selected.description}</p>
                <div className="purchase-summary">
                  <strong>{priceLabel(selected)}</strong>
                  <span>
                    {selected.monetization === "free"
                      ? "Jogo gratuito · gorjetas opcionais"
                      : selected.monetization === "demo"
                        ? "Demo gratuita + compra do jogo completo"
                        : selected.monetization === "pwyw"
                          ? "Você escolhe o valor, a partir do mínimo"
                          : "Compra única · acesso ao jogo completo"}
                  </span>
                </div>
                {checkout && (
                  <section
                    className="checkout-preview"
                    aria-label="Prévia do pagamento"
                  >
                    <span className="level">
                      PRÉVIA · PAGAMENTO INDISPONÍVEL
                    </span>
                    <h3>
                      {checkout === "tip"
                        ? "Apoiar o criador"
                        : "Comprar " + selected.title}
                    </h3>
                    {checkout === "tip" || selected.monetization === "pwyw" ? (
                      <>
                        <label htmlFor="usdc-amount">
                          Valor em USDC{" "}
                          {checkout === "purchase" &&
                            `(mínimo ${formatUsdc(selected.price)})`}
                        </label>
                        <input
                          id="usdc-amount"
                          type="text"
                          inputMode="decimal"
                          value={amount}
                          onChange={(e) => setAmount(e.target.value)}
                          aria-describedby="amount-feedback"
                        />
                        <p id="amount-feedback" role="status">
                          {parseUsdc(amount) === null
                            ? "Informe um valor válido com até 6 casas decimais."
                            : parseUsdc(amount)! <
                                (checkout === "tip" ? 1n : selected.price)
                              ? checkout === "tip"
                                ? "A gorjeta deve ser maior que zero."
                                : "O valor está abaixo do mínimo do criador."
                              : `Total: ${formatUsdc(parseUsdc(amount)!)}`}
                        </p>
                      </>
                    ) : (
                      <p>Total: {formatUsdc(selected.price)}</p>
                    )}
                    <p>
                      Pagamento em USDC na Solana. A transação também exige SOL
                      para a taxa de rede. Nenhuma cobrança será feita nesta
                      prévia.
                    </p>
                    <button className="button secondary" disabled>
                      INTEGRAÇÃO EM PREPARAÇÃO
                    </button>
                    <button
                      className="text-button"
                      onClick={() => setCheckout(null)}
                    >
                      VOLTAR AO JOGO
                    </button>
                  </section>
                )}
                <div className="dialog-actions">
                  {selected.playable ? (
                    <button
                      className="button primary"
                      onClick={() => setPlaying(true)}
                    >
                      JOGAR AGORA ▶
                    </button>
                  ) : (
                    <span className="level">CONCEITO · AINDA NÃO JOGÁVEL</span>
                  )}
                  {selected.monetization !== "free" && (
                    <button
                      className="button primary"
                      onClick={() => setCheckout("purchase")}
                    >
                      {selected.monetization === "pwyw"
                        ? "ESCOLHER VALOR"
                        : "VER COMPRA EM USDC"}
                    </button>
                  )}
                  {selected.tips && (
                    <button
                      className="button secondary"
                      onClick={() => {
                        setAmount("1");
                        setCheckout("tip");
                      }}
                    >
                      DAR GORJETA
                    </button>
                  )}
                  {selected.monetization === "demo" && (
                    <span className="muted">
                      Demo indisponível neste título demonstrativo.
                    </span>
                  )}
                  <button
                    className="button secondary"
                    onClick={() =>
                      setSaved((old) =>
                        old.includes(selected.id)
                          ? old.filter((id) => id !== selected.id)
                          : [...old, selected.id],
                      )
                    }
                  >
                    {saved.includes(selected.id)
                      ? "REMOVER DA BIBLIOTECA"
                      : "+ BIBLIOTECA"}
                  </button>
                </div>
                <small className="muted">
                  Esta biblioteca local guarda favoritos. Jogos pagos só serão
                  desbloqueados após confirmação do pagamento pelo servidor.
                </small>
              </>
            )}
          </>
        )}
      </dialog>
      <dialog
        ref={walletDialog}
        className="wallet-dialog"
        onCancel={() => setWalletOpen(false)}
        onClose={() => setWalletOpen(false)}
      >
        <div className="dialog-heading">
          <h2>Carteira Solana</h2>
          <button
            className="close-button"
            onClick={() => setWalletOpen(false)}
            aria-label="Fechar"
          >
            ✕
          </button>
        </div>
        <span className="level">INTEGRAÇÃO EM BREVE</span>
        <p>
          Os pagamentos do Plemu serão somente em USDC na Solana. Você precisará
          de USDC para a compra e SOL para a taxa de rede. A conexão e os
          pagamentos ainda não estão disponíveis nesta prévia.
        </p>
        <button
          className="button primary"
          onClick={() => {
            setWalletOpen(false);
            openGame(games[0]);
          }}
        >
          TESTAR O PLAYER
        </button>
      </dialog>
    </>
  );
}
