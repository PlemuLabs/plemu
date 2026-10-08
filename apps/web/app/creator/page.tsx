"use client";

import Link from "next/link";
import { useEffect, useState, useRef } from "react";
import { formatUsdc, parseUsdc } from "../commerce";
import { WalletHeaderButton } from "../../components/WalletHeaderButton";
import { usePlemuAuth } from "../../hooks/usePlemuAuth";
import { useWalletModal } from "@solana/wallet-adapter-react-ui";
import { SOLANA_CONFIG } from "../../lib/solana-config";
import { getExplorerUrl } from "../../lib/solana-utils";
import "./creator.css";

type Tab =
  | "overview"
  | "games"
  | "publish"
  | "sales"
  | "revenue"
  | "analytics"
  | "profile";
type Mode = "free" | "purchase" | "demo" | "pwyw";
type Draft = {
  id: string;
  title: string;
  description: string;
  genre: string;
  mode: Mode;
  price: string;
  tips: boolean;
  rights: boolean;
  cover: string;
  buildName: string;
  demoName: string;
};
const newDraft = (): Draft => ({
  id: "",
  title: "",
  description: "",
  genre: "Arcade",
  mode: "free",
  price: "",
  tips: false,
  rights: false,
  cover: "",
  buildName: "",
  demoName: "",
});
const navigation: { id: Tab; label: string; icon: string }[] = [
  { id: "overview", label: "Visão geral", icon: "▦" },
  { id: "games", label: "Meus jogos", icon: "◈" },
  { id: "publish", label: "Publicar jogo", icon: "+" },
  { id: "sales", label: "Vendas", icon: "≡" },
  { id: "revenue", label: "Receitas e carteira", icon: "◇" },
  { id: "analytics", label: "Estatísticas", icon: "▥" },
  { id: "profile", label: "Perfil do estúdio", icon: "◎" },
];
const steps = ["Informações", "Arquivos", "Monetização", "Direitos", "Revisão"];
const modeLabels: Record<Mode, string> = {
  free: "Gratuito",
  purchase: "Compra individual",
  demo: "Demo + compra",
  pwyw: "Pague quanto quiser",
};
const sales = [
  {
    id: "DEMO-001",
    game: "Moonlit Forest",
    kind: "Compra",
    amount: "5 USDC",
    date: "05/10/2026",
  },
  {
    id: "DEMO-002",
    game: "Neon Orbit",
    kind: "Gorjeta",
    amount: "1 USDC",
    date: "05/10/2026",
  },
  {
    id: "DEMO-003",
    game: "Pixel Quest",
    kind: "Compra",
    amount: "8 USDC",
    date: "04/10/2026",
  },
  {
    id: "DEMO-004",
    game: "Cyber Wave",
    kind: "Valor livre",
    amount: "3 USDC",
    date: "04/10/2026",
  },
];
function validDraft(value: unknown): value is Draft {
  if (!value || typeof value !== "object") return false;
  const d = value as Draft;
  return (
    [
      "id",
      "title",
      "description",
      "genre",
      "price",
      "cover",
      "buildName",
      "demoName",
    ].every((k) => typeof d[k as keyof Draft] === "string") &&
    ["free", "purchase", "demo", "pwyw"].includes(d.mode) &&
    typeof d.tips === "boolean" &&
    typeof d.rights === "boolean"
  );
}
export default function Creator() {
  const [tab, setTab] = useState<Tab>("overview");
  const [drafts, setDrafts] = useState<Draft[]>([]);
  const [draft, setDraft] = useState<Draft>(newDraft);
  const [step, setStep] = useState(0);
  const [feedback, setFeedback] = useState("");
  const [error, setError] = useState("");
  const [build, setBuild] = useState<File | null>(null);
  const [demo, setDemo] = useState<File | null>(null);
  const [search, setSearch] = useState("");
  const [kind, setKind] = useState("Todas");
  const [profile, setProfile] = useState({ name: "", bio: "", website: "" });

  const {
    isConnected,
    base58Address,
    shortAddress,
    solBalance,
    usdcBalanceFormatted,
    isLoadingBalance,
    refreshBalances,
    disconnect,
  } = usePlemuAuth();
  const { setVisible } = useWalletModal();
  const [copiedWallet, setCopiedWallet] = useState(false);
  const copyTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    return () => {
      if (copyTimerRef.current) {
        clearTimeout(copyTimerRef.current);
      }
    };
  }, []);

  const handleCopyWallet = async () => {
    if (!base58Address) return;
    try {
      if (navigator?.clipboard?.writeText) {
        await navigator.clipboard.writeText(base58Address);
      } else if (typeof document !== "undefined") {
        const textarea = document.createElement("textarea");
        textarea.value = base58Address;
        textarea.style.position = "fixed";
        textarea.style.opacity = "0";
        document.body.appendChild(textarea);
        textarea.focus();
        textarea.select();
        document.execCommand("copy");
        document.body.removeChild(textarea);
      }
      setCopiedWallet(true);
      if (copyTimerRef.current) {
        clearTimeout(copyTimerRef.current);
      }
      copyTimerRef.current = setTimeout(() => {
        setCopiedWallet(false);
      }, 2000);
    } catch {
      // Silently handle clipboard write error
    }
  };

  const handleOpenConnect = () => {
    if (typeof document !== "undefined") {
      document.querySelectorAll("dialog[open]").forEach((d) => {
        if (d instanceof HTMLDialogElement) d.close();
      });
    }
    setVisible(true);
  };
  useEffect(() => {
    const sync = () => {
      const target = location.hash.slice(1);
      setTab(
        navigation.some((n) => n.id === target) ? (target as Tab) : "overview",
      );
    };
    sync();
    window.addEventListener("hashchange", sync);
    try {
      const stored: unknown = JSON.parse(
        localStorage.getItem("plemu-creator-drafts") || "[]",
      );
      if (Array.isArray(stored)) setDrafts(stored.filter(validDraft));
      const p = JSON.parse(
        localStorage.getItem("plemu-creator-profile") || "null",
      );
      if (
        p &&
        ["name", "bio", "website"].every((k) => typeof p[k] === "string")
      )
        setProfile(p);
    } catch {
      setFeedback("Não foi possível recuperar os dados locais.");
    }
    return () => window.removeEventListener("hashchange", sync);
  }, []);
  const go = (next: Tab) => {
    location.hash = next;
    setTab(next);
    setError("");
    setFeedback("");
  };
  const patch = (values: Partial<Draft>) =>
    setDraft((old) => ({ ...old, ...values }));
  const begin = (item?: Draft) => {
    setDraft(item || newDraft());
    setBuild(null);
    setDemo(null);
    setStep(0);
    go("publish");
  };
  const save = () => {
    if (!draft.title.trim()) {
      setError("Dê um nome ao jogo antes de salvar o rascunho.");
      return false;
    }
    const item = {
      ...draft,
      title: draft.title.trim(),
      id: draft.id || crypto.randomUUID(),
    };
    const updated = [...drafts.filter((d) => d.id !== item.id), item];
    try {
      localStorage.setItem("plemu-creator-drafts", JSON.stringify(updated));
      setDrafts(updated);
      setDraft(item);
      setFeedback(
        "Rascunho salvo neste navegador. Os arquivos não foram enviados.",
      );
      setError("");
      return true;
    } catch {
      setError(
        "Não foi possível salvar. Verifique o armazenamento do navegador.",
      );
      return false;
    }
  };
  const validate = (current: number) => {
    if (current === 0 && (!draft.title.trim() || !draft.description.trim()))
      return "Preencha o nome e a descrição do jogo.";
    if (current === 1 && !build)
      return "Selecione um pacote ZIP nesta sessão. Nenhum arquivo será enviado.";
    if (current === 1 && draft.mode === "demo" && !demo)
      return "Selecione também o ZIP da demo, separado do jogo completo.";
    if (
      current === 2 &&
      draft.mode !== "free" &&
      (parseUsdc(draft.price) === null || parseUsdc(draft.price)! <= 0n)
    )
      return "Informe um preço maior que zero, com até 6 casas decimais.";
    if (current === 2 && draft.mode === "demo" && !demo)
      return "Para oferecer demo, selecione um ZIP separado na etapa Arquivos.";
    if (current === 3 && !draft.rights)
      return "Confirme que possui os direitos de distribuição.";
    return "";
  };
  const next = () => {
    const message = validate(step);
    setError(message);
    if (!message) setStep((old) => Math.min(4, old + 1));
  };
  const fileChange = (file: File | undefined, isDemo: boolean) => {
    setError("");
    if (!file) {
      if (isDemo) {
        setDemo(null);
        patch({ demoName: "" });
      } else {
        setBuild(null);
        patch({ buildName: "" });
      }
      return;
    }
    if (
      !file.name.toLowerCase().endsWith(".zip") ||
      file.size > 100 * 1024 * 1024 ||
      file.size === 0
    ) {
      setError("Escolha um arquivo ZIP não vazio de até 100 MB.");
      if (isDemo) {
        setDemo(null);
        patch({ demoName: "" });
      } else {
        setBuild(null);
        patch({ buildName: "" });
      }
      return;
    }
    if (isDemo) {
      setDemo(file);
      patch({ demoName: file.name });
    } else {
      setBuild(file);
      patch({ buildName: file.name });
    }
  };
  const total =
    draft.mode === "free"
      ? "Gratuito"
      : parseUsdc(draft.price) === null
        ? "Preço pendente"
        : formatUsdc(parseUsdc(draft.price)!);
  const pageTitle = navigation.find((n) => n.id === tab)?.label;
  return (
    <div className="creator-app" lang="pt-BR">
      <header className="creator-header">
        <Link href="/" className="brand">
          PLEMU<span className="purple-text">✦</span>
        </Link>
        <span className="creator-wordmark">CREATOR STUDIO</span>
        <div className="creator-header-actions">
          <WalletHeaderButton />
          <Link href="/" className="studio-back">
            VOLTAR À LOJA
          </Link>
        </div>
      </header>
      <div className="studio-layout">
        <aside className="studio-sidebar">
          <div className="studio-identity">
            <span className="studio-avatar">P</span>
            <div>
              <strong>{profile.name || "Seu estúdio"}</strong>
              <small>Área do criador</small>
            </div>
          </div>
          <nav aria-label="Área do criador">
            {navigation.map((n) => (
              <a
                key={n.id}
                href={`#${n.id}`}
                aria-current={tab === n.id ? "page" : undefined}
              >
                <span aria-hidden="true">{n.icon}</span>
                {n.label}
              </a>
            ))}
          </nav>
          <div className="sidebar-note">
            <span>USDC / SOLANA</span>
            <p>
              Jogos independentes.
              <br />
              Novas possibilidades.
            </p>
          </div>
        </aside>
        <main className="studio-main">
          <div className="studio-banner">
            <strong>PRÉVIA DO ESTÚDIO</strong>
            <span>
              Vendas e métricas são exemplos. Rascunhos ficam neste navegador.
              Não há publicação ou pagamentos reais.
            </span>
          </div>
          <div className="studio-title">
            <div>
              <span className="eyebrow purple-text">YOUR NEXT LEVEL</span>
              <h1>{pageTitle}</h1>
            </div>
            {tab !== "publish" && (
              <button className="button primary" onClick={() => begin()}>
                + NOVO JOGO
              </button>
            )}
          </div>
          {feedback && (
            <p className="studio-feedback" role="status">
              {feedback}
            </p>
          )}
          {error && (
            <p className="studio-error" role="alert">
              {error}
            </p>
          )}
          {tab === "overview" && (
            <>
              <div className="studio-stats">
                <article>
                  <span>Rascunhos locais</span>
                  <strong>{drafts.length.toString().padStart(2, "0")}</strong>
                  <small>Seus projetos neste navegador</small>
                </article>
                <article>
                  <span>Vendas de exemplo</span>
                  <strong>03</strong>
                  <small>3 compras + 1 gorjeta</small>
                </article>
                <article>
                  <span>Receita de exemplo</span>
                  <strong>
                    17 <em>USDC</em>
                  </strong>
                  <small>Bruta · comissão ainda indefinida</small>
                </article>
                <article>
                  <span>Partidas de exemplo</span>
                  <strong>128</strong>
                  <small>Dados ilustrativos</small>
                </article>
              </div>
              <div className="studio-columns">
                <section className="studio-panel start-panel">
                  <span className="eyebrow purple-text">
                    READY, PLAYER TWO?
                  </span>
                  <h2>
                    Seu próximo jogo
                    <br />
                    começa aqui.
                  </h2>
                  <p>
                    Prepare a página, selecione o pacote web e escolha como
                    monetizar. Salve um rascunho para continuar depois.
                  </p>
                  <button className="button primary" onClick={() => begin()}>
                    CRIAR PRIMEIRO RASCUNHO
                  </button>
                </section>
                <section className="studio-panel">
                  <h2>Checklist do criador</h2>
                  <ul className="studio-checklist">
                    <li>Uma versão que rode no navegador</li>
                    <li>Descrição, gênero e apresentação</li>
                    <li>Preço em USDC ou acesso gratuito</li>
                    <li>Direitos de distribuição confirmados</li>
                  </ul>
                  <a href="#profile" className="text-button">
                    CONFIGURAR MEU ESTÚDIO
                  </a>
                </section>
              </div>
              <section className="studio-panel">
                <h2>Atividade de exemplo</h2>
                <div className="activity-list">
                  {sales.slice(0, 3).map((s) => (
                    <div key={s.id}>
                      <span className="activity-icon">
                        {s.kind === "Gorjeta" ? "♡" : "◇"}
                      </span>
                      <div>
                        <strong>{s.game}</strong>
                        <small>
                          {s.kind} · {s.date}
                        </small>
                      </div>
                      <b>{s.amount}</b>
                    </div>
                  ))}
                </div>
              </section>
            </>
          )}
          {tab === "games" && (
            <>
              <div className="studio-toolbar">
                <label>
                  Buscar rascunho
                  <input
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                    placeholder="Nome do jogo"
                  />
                </label>
                <span className="muted">
                  {drafts.length} rascunho(s) local(is) · 0 publicados
                </span>
              </div>
              {drafts.length === 0 ? (
                <section className="studio-panel studio-empty">
                  <span className="empty-icon">◈</span>
                  <h2>O próximo mundo pode ser seu.</h2>
                  <p>
                    Crie seu primeiro rascunho. Você poderá editar os detalhes e
                    testar as modalidades de venda.
                  </p>
                  <button className="button primary" onClick={() => begin()}>
                    CRIAR JOGO
                  </button>
                </section>
              ) : (
                <div className="draft-grid">
                  {drafts
                    .filter((d) =>
                      d.title.toLowerCase().includes(search.toLowerCase()),
                    )
                    .map((d) => (
                      <article className="studio-panel" key={d.id}>
                        <div className="draft-cover" aria-hidden="true">
                          ◈
                        </div>
                        <span className="draft-status">RASCUNHO LOCAL</span>
                        <h2>{d.title}</h2>
                        <p>
                          {d.genre} · {modeLabels[d.mode]}
                        </p>
                        <button
                          className="button secondary"
                          onClick={() => begin(d)}
                        >
                          EDITAR RASCUNHO
                        </button>
                      </article>
                    ))}
                  {!drafts.some((d) =>
                    d.title.toLowerCase().includes(search.toLowerCase()),
                  ) && <p>Nenhum rascunho encontrado.</p>}
                </div>
              )}
            </>
          )}
          {tab === "publish" && (
            <>
              <ol className="publish-steps" aria-label="Etapas da publicação">
                {steps.map((s, i) => (
                  <li key={s} aria-current={step === i ? "step" : undefined}>
                    <span>{i + 1}</span>
                    {s}
                  </li>
                ))}
              </ol>
              <form
                className="studio-panel publish-form"
                onSubmit={(e) => {
                  e.preventDefault();
                  if (step < 4) next();
                  else save();
                }}
              >
                <h2>{steps[step]}</h2>
                {step === 0 && (
                  <>
                    <p>
                      Apresente seu jogo para quem vai descobrir o próximo
                      universo.
                    </p>
                    <label>
                      Nome do jogo
                      <input
                        maxLength={80}
                        value={draft.title}
                        onChange={(e) => patch({ title: e.target.value })}
                        placeholder="Como se chama sua aventura?"
                      />
                    </label>
                    <label>
                      Descrição
                      <textarea
                        maxLength={2000}
                        rows={5}
                        value={draft.description}
                        onChange={(e) => patch({ description: e.target.value })}
                        placeholder="Conte a proposta e como jogar."
                      />
                    </label>
                    <div className="form-two">
                      <label>
                        Gênero
                        <select
                          value={draft.genre}
                          onChange={(e) => patch({ genre: e.target.value })}
                        >
                          {[
                            "Arcade",
                            "Aventura",
                            "RPG",
                            "Corrida",
                            "Puzzle",
                            "Estratégia",
                            "Outro",
                          ].map((g) => (
                            <option key={g}>{g}</option>
                          ))}
                        </select>
                      </label>
                      <label>
                        Link de apresentação (opcional)
                        <input
                          type="url"
                          value={draft.cover}
                          onChange={(e) => patch({ cover: e.target.value })}
                          placeholder="https://…"
                        />
                      </label>
                    </div>
                  </>
                )}
                {step === 1 && (
                  <>
                    <p>
                      Selecione um ZIP com a página de entrada e os arquivos do
                      jogo HTML5 ou WebAssembly.
                    </p>
                    <div className="upload-box">
                      <span>⬡</span>
                      <label>
                        Pacote do jogo completo
                        <input
                          type="file"
                          accept=".zip,application/zip"
                          onChange={(e) =>
                            fileChange(e.target.files?.[0], false)
                          }
                        />
                      </label>
                      <small>Até 100 MB · entrada prevista: index.html</small>
                      {build && (
                        <strong>
                          {build.name} · {(build.size / 1024).toFixed(1)} KB
                        </strong>
                      )}
                    </div>
                    <label>
                      Pacote da demo (opcional)
                      <input
                        type="file"
                        accept=".zip,application/zip"
                        onChange={(e) => fileChange(e.target.files?.[0], true)}
                      />
                    </label>
                    {demo && <p>{demo.name}</p>}
                    <p className="studio-warning">
                      Os arquivos ficam apenas nesta sessão. O ZIP não é
                      inspecionado nem enviado nesta prévia. Ao reabrir um
                      rascunho, selecione os arquivos novamente.
                    </p>
                    {draft.buildName && !build && (
                      <p className="muted">
                        Arquivo escolhido anteriormente: {draft.buildName}
                      </p>
                    )}
                  </>
                )}
                {step === 2 && (
                  <>
                    <p>
                      Pagamento somente em USDC na Solana. Você define a
                      modalidade.
                    </p>
                    <fieldset className="mode-options">
                      <legend>Modalidade de acesso</legend>
                      {(Object.keys(modeLabels) as Mode[]).map((mode) => (
                        <label key={mode}>
                          <input
                            type="radio"
                            name="mode"
                            checked={draft.mode === mode}
                            onChange={() =>
                              patch({
                                mode,
                                tips: mode === "free" ? draft.tips : false,
                              })
                            }
                          />
                          <span>
                            <strong>{modeLabels[mode]}</strong>
                            <small>
                              {mode === "free"
                                ? "Sem pagamento para jogar"
                                : mode === "purchase"
                                  ? "Uma compra desbloqueia o jogo"
                                  : mode === "demo"
                                    ? "Demo gratuita e jogo completo pago"
                                    : "O jogador escolhe, respeitando seu mínimo"}
                            </small>
                          </span>
                        </label>
                      ))}
                    </fieldset>
                    {draft.mode !== "free" && (
                      <label>
                        {draft.mode === "pwyw"
                          ? "Valor mínimo em USDC"
                          : "Preço em USDC"}
                        <input
                          inputMode="decimal"
                          value={draft.price}
                          onChange={(e) => patch({ price: e.target.value })}
                          placeholder="5,00"
                        />
                        <small>
                          Até 6 casas decimais. Valor maior que zero.
                        </small>
                      </label>
                    )}
                    {draft.mode === "free" && (
                      <label className="checkbox-label">
                        <input
                          type="checkbox"
                          checked={draft.tips}
                          onChange={(e) => patch({ tips: e.target.checked })}
                        />
                        Permitir gorjetas voluntárias em USDC
                      </label>
                    )}
                    <p className="studio-warning">
                      Comissão e regras de repasse ainda serão definidas. Esta
                      prévia não cobra nem cria acesso pago.
                    </p>
                  </>
                )}
                {step === 3 && (
                  <>
                    <p>
                      Informe apenas conteúdo que você tem autorização para
                      distribuir.
                    </p>
                    <div className="rights-copy">
                      <h3>Antes da publicação</h3>
                      <p>
                        Verifique os direitos do código, imagens, músicas,
                        fontes e demais arquivos. A versão publicada também
                        precisará passar pela análise da plataforma.
                      </p>
                    </div>
                    <label className="checkbox-label">
                      <input
                        type="checkbox"
                        checked={draft.rights}
                        onChange={(e) => patch({ rights: e.target.checked })}
                      />
                      Confirmo que tenho os direitos necessários para distribuir
                      este jogo e seus arquivos.
                    </label>
                    <p className="muted">
                      A confirmação fica no rascunho local e não representa
                      aprovação ou revisão legal.
                    </p>
                  </>
                )}
                {step === 4 && (
                  <>
                    <p>Confira sua página antes de salvar.</p>
                    <div className="review-card">
                      <span className="draft-status">RASCUNHO LOCAL</span>
                      <h2>{draft.title}</h2>
                      <p className="review-description">{draft.description}</p>
                      <dl>
                        <div>
                          <dt>Gênero</dt>
                          <dd>{draft.genre}</dd>
                        </div>
                        <div>
                          <dt>Modalidade</dt>
                          <dd>{modeLabels[draft.mode]}</dd>
                        </div>
                        <div>
                          <dt>{draft.mode === "pwyw" ? "Mínimo" : "Preço"}</dt>
                          <dd>{total}</dd>
                        </div>
                        <div>
                          <dt>Gorjetas</dt>
                          <dd>
                            {draft.mode === "free" && draft.tips
                              ? "Permitidas"
                              : "Desativadas"}
                          </dd>
                        </div>
                        <div>
                          <dt>Pacote</dt>
                          <dd>{build?.name || "Selecione novamente"}</dd>
                        </div>
                        <div>
                          <dt>Direitos</dt>
                          <dd>
                            {draft.rights
                              ? "Declarados pelo criador"
                              : "Pendentes"}
                          </dd>
                        </div>
                      </dl>
                    </div>
                    <p className="studio-warning">
                      A publicação real ainda não está disponível. Salvar não
                      envia o jogo para análise nem disponibiliza arquivos na
                      loja.
                    </p>
                  </>
                )}
                <div className="publish-actions">
                  <button
                    type="button"
                    className="button secondary"
                    onClick={() => {
                      setError("");
                      step ? setStep(step - 1) : go("games");
                    }}
                  >
                    VOLTAR
                  </button>
                  <button type="button" className="text-button" onClick={save}>
                    SALVAR RASCUNHO
                  </button>
                  <button type="submit" className="button primary">
                    {step === 4 ? "SALVAR REVISÃO" : "CONTINUAR"}
                  </button>
                </div>
              </form>
            </>
          )}
          {tab === "sales" && (
            <section className="studio-panel">
              <div className="panel-heading">
                <div>
                  <h2>Histórico de exemplo</h2>
                  <p>Nenhuma destas vendas representa uma transação real.</p>
                </div>
                <label>
                  Tipo de operação
                  <select
                    value={kind}
                    onChange={(e) => setKind(e.target.value)}
                  >
                    {["Todas", "Compra", "Gorjeta", "Valor livre"].map((k) => (
                      <option key={k}>{k}</option>
                    ))}
                  </select>
                </label>
              </div>
              <div className="table-scroll">
                <table>
                  <caption>Operações ilustrativas em USDC</caption>
                  <thead>
                    <tr>
                      <th>Pedido</th>
                      <th>Jogo</th>
                      <th>Modalidade</th>
                      <th>Valor bruto</th>
                      <th>Data</th>
                      <th>Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {sales
                      .filter((s) => kind === "Todas" || s.kind === kind)
                      .map((s) => (
                        <tr key={s.id}>
                          <td>{s.id}</td>
                          <td>{s.game}</td>
                          <td>{s.kind}</td>
                          <td className="mint-text">{s.amount}</td>
                          <td>{s.date}</td>
                          <td>
                            <span className="draft-status">EXEMPLO</span>
                          </td>
                        </tr>
                      ))}
                  </tbody>
                </table>
              </div>
            </section>
          )}
          {tab === "revenue" && (
            <>
              <div className="studio-stats">
                <article>
                  <span>Receita bruta de exemplo</span>
                  <strong>
                    17 <em>USDC</em>
                  </strong>
                  <small>Não representa saldo disponível</small>
                </article>
                <article>
                  <span>Comissão Plemu</span>
                  <strong>—</strong>
                  <small>A definir</small>
                </article>
                <article>
                  <span>Receita líquida</span>
                  <strong>—</strong>
                  <small>Depende da comissão e repasse</small>
                </article>
              </div>
              <div className="studio-columns">
                <section className="studio-panel">
                  {!isConnected ? (
                    <>
                      <h2>Carteira de recebimento</h2>
                      <p>
                        As receitas serão em USDC na Solana. A carteira
                        precisará ser conectada e sua titularidade verificada.
                      </p>
                      <div className="wallet-placeholder">
                        ◇ CARTEIRA NÃO CONECTADA
                      </div>
                      <button
                        type="button"
                        className="button primary"
                        onClick={handleOpenConnect}
                      >
                        CONECTAR CARTEIRA DE RECEBIMENTO
                      </button>
                    </>
                  ) : (
                    <>
                      <div className="wallet-panel-header">
                        <h2>Carteira de recebimento</h2>
                        <span className="draft-status connected">
                          ● CONECTADA
                        </span>
                      </div>
                      <p>
                        Sua carteira Solana está conectada e configurada para o
                        recebimento de repasses em USDC na Devnet.
                      </p>
                      <div className="wallet-connected-info">
                        <div className="wallet-detail-row">
                          <span className="wallet-label">ENDEREÇO</span>
                          <div className="wallet-address-container">
                            <strong
                              className="wallet-mono-address"
                              title={base58Address || ""}
                            >
                              {shortAddress}
                            </strong>
                            <button
                              type="button"
                              className="text-button copy-wallet-btn"
                              onClick={handleCopyWallet}
                            >
                              {copiedWallet ? "COPIADO!" : "COPIAR"}
                            </button>
                          </div>
                        </div>
                        <div className="wallet-detail-row">
                          <span className="wallet-label">SOLANA EXPLORER</span>
                          <a
                            href={
                              base58Address
                                ? getExplorerUrl(
                                    SOLANA_CONFIG.network,
                                    "address",
                                    base58Address,
                                  )
                                : "#"
                            }
                            target="_blank"
                            rel="noopener noreferrer"
                            className="text-button explorer-link"
                          >
                            VER NO EXPLORER ↗
                          </a>
                        </div>
                        <div className="wallet-detail-row">
                          <span className="wallet-label">SALDO SOL</span>
                          <strong className="mint-text">
                            {isLoadingBalance
                              ? "CARREGANDO..."
                              : solBalance !== null
                                ? `${solBalance.toFixed(2)} SOL`
                                : "—"}
                          </strong>
                        </div>
                        <div className="wallet-detail-row">
                          <span className="wallet-label">
                            SALDO USDC ({SOLANA_CONFIG.network.toUpperCase()})
                          </span>
                          <strong className="purple-text">
                            {isLoadingBalance
                              ? "CARREGANDO..."
                              : usdcBalanceFormatted || "--- USDC"}
                          </strong>
                        </div>
                      </div>
                      <div className="wallet-actions">
                        <button
                          type="button"
                          className="button secondary"
                          onClick={() => refreshBalances(true)}
                          disabled={isLoadingBalance}
                        >
                          {isLoadingBalance
                            ? "ATUALIZANDO..."
                            : "ATUALIZAR SALDOS"}
                        </button>
                        <button
                          type="button"
                          className="button secondary disconnect-btn"
                          onClick={() => disconnect()}
                        >
                          DESCONECTAR
                        </button>
                      </div>
                    </>
                  )}
                </section>
                <section className="studio-panel">
                  <h2>Repasses</h2>
                  <p>
                    O calendário e as regras ainda serão definidos. Não há saldo
                    real, saque ou transferência nesta prévia.
                  </p>
                  <span className="draft-status">INTEGRAÇÃO PENDENTE</span>
                </section>
              </div>
            </>
          )}
          {tab === "analytics" && (
            <>
              <div className="studio-stats">
                <article>
                  <span>Visitas de exemplo</span>
                  <strong>640</strong>
                  <small>Últimos 7 dias · exemplo</small>
                </article>
                <article>
                  <span>Partidas de exemplo</span>
                  <strong>128</strong>
                  <small>Não coletadas do player</small>
                </article>
                <article>
                  <span>Compras de exemplo</span>
                  <strong>3</strong>
                  <small>Sem gorjetas</small>
                </article>
              </div>
              <section className="studio-panel">
                <h2>Visitas por dia · exemplo</h2>
                <p>
                  Distribuição ilustrativa de 640 visitas. Nenhuma telemetria
                  está integrada.
                </p>
                <div
                  className="visits-chart"
                  role="img"
                  aria-label="Visitas ilustrativas: terça 50, quarta 70, quinta 100, sexta 80, sábado 120, domingo 130, segunda 90."
                >
                  {[50, 70, 100, 80, 120, 130, 90].map((v, i) => (
                    <div key={i}>
                      <span>{v}</span>
                      <div className="chart-bar" style={{ height: `${v}px` }} />
                      <small>
                        {["TER", "QUA", "QUI", "SEX", "SÁB", "DOM", "SEG"][i]}
                      </small>
                    </div>
                  ))}
                </div>
              </section>
            </>
          )}
          {tab === "profile" && (
            <form
              className="studio-panel profile-form"
              onSubmit={(e) => {
                e.preventDefault();
                try {
                  localStorage.setItem(
                    "plemu-creator-profile",
                    JSON.stringify(profile),
                  );
                  setFeedback(
                    "Perfil salvo neste navegador. Não foi publicado.",
                  );
                } catch {
                  setError("Não foi possível salvar o perfil neste navegador.");
                }
              }}
            >
              <h2>A identidade do seu estúdio</h2>
              <p>Estas informações são locais nesta prévia.</p>
              <label>
                Nome do estúdio
                <input
                  required
                  maxLength={80}
                  value={profile.name}
                  onChange={(e) =>
                    setProfile({ ...profile, name: e.target.value })
                  }
                  placeholder="Seu nome ou estúdio"
                />
              </label>
              <label>
                Sobre o estúdio
                <textarea
                  rows={5}
                  maxLength={1000}
                  value={profile.bio}
                  onChange={(e) =>
                    setProfile({ ...profile, bio: e.target.value })
                  }
                  placeholder="Que mundos você gosta de criar?"
                />
              </label>
              <label>
                Site (opcional)
                <input
                  type="url"
                  value={profile.website}
                  onChange={(e) =>
                    setProfile({ ...profile, website: e.target.value })
                  }
                  placeholder="https://…"
                />
              </label>
              <button className="button primary" type="submit">
                SALVAR PERFIL LOCAL
              </button>
            </form>
          )}
        </main>
      </div>
    </div>
  );
}
