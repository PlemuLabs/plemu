import React from "react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, act } from "@testing-library/react";
import CreatorPage from "../page";
import * as usePlemuAuthModule from "../../../hooks/usePlemuAuth";
import * as walletAdapterUiModule from "@solana/wallet-adapter-react-ui";

vi.mock("../../../hooks/usePlemuAuth");
vi.mock("@solana/wallet-adapter-react-ui", () => ({
  useWalletModal: vi.fn(),
}));

describe("Creator Studio Page - Web3 & Wallet Integration", () => {
  const mockSetVisible = vi.fn();
  const mockConnect = vi.fn().mockResolvedValue(undefined);
  const mockDisconnect = vi.fn().mockResolvedValue(undefined);
  const mockRefreshBalances = vi.fn().mockResolvedValue(undefined);

  const defaultAuthState: usePlemuAuthModule.PlemuAuthState = {
    isConnected: false,
    isConnecting: false,
    walletName: null,
    publicKey: null,
    base58Address: null,
    shortAddress: null,
    lamports: null,
    solBalance: null,
    usdcUnits: null,
    usdcBalanceFormatted: null,
    isLoadingBalance: false,
    connect: mockConnect,
    disconnect: mockDisconnect,
    refreshBalances: mockRefreshBalances,
    signChallenge: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ toFake: ["setTimeout", "clearTimeout"] });
    window.location.hash = "";

    if (!HTMLDialogElement.prototype.showModal) {
      HTMLDialogElement.prototype.showModal = function () {
        this.setAttribute("open", "");
      };
    }
    if (!HTMLDialogElement.prototype.close) {
      HTMLDialogElement.prototype.close = function () {
        this.removeAttribute("open");
      };
    }

    Object.assign(navigator, {
      clipboard: {
        writeText: vi.fn().mockResolvedValue(undefined),
      },
    });

    vi.mocked(walletAdapterUiModule.useWalletModal).mockReturnValue({
      visible: false,
      setVisible: mockSetVisible,
    });

    vi.spyOn(usePlemuAuthModule, "usePlemuAuth").mockReturnValue(defaultAuthState);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("renders WalletHeaderButton in creator-header across tabs", () => {
    render(<CreatorPage />);
    const headerWalletBtn = screen.getByRole("button", { name: /CONECTAR CARTEIRA/i });
    expect(headerWalletBtn).toBeDefined();
  });

  it("renders disconnected revenue tab with active 'CONECTAR CARTEIRA DE RECEBIMENTO' button", () => {
    window.location.hash = "#revenue";
    render(<CreatorPage />);

    expect(screen.getByText(/CARTEIRA NÃO CONECTADA/i)).toBeDefined();

    const connectBtn = screen.getByRole("button", {
      name: /CONECTAR CARTEIRA DE RECEBIMENTO/i,
    });
    expect(connectBtn).toBeDefined();
    expect((connectBtn as HTMLButtonElement).disabled).toBe(false);

    fireEvent.click(connectBtn);
    expect(mockSetVisible).toHaveBeenCalledWith(true);
  });

  it("renders connected wallet details, balances and explorer link in revenue tab", () => {
    window.location.hash = "#revenue";
    vi.spyOn(usePlemuAuthModule, "usePlemuAuth").mockReturnValue({
      ...defaultAuthState,
      isConnected: true,
      base58Address: "Dos7xNqW8vGkE6y2k8D2Vp9k1p4mQz8wR5tY7u3i4o5p",
      shortAddress: "Dos7...4o5p",
      lamports: 3_450_000_000n,
      solBalance: 3.45,
      usdcBalanceFormatted: "150.00 USDC",
    });

    render(<CreatorPage />);

    expect(screen.getByText("● CONECTADA")).toBeDefined();
    expect(screen.getAllByText("Dos7...4o5p").length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText("3.45 SOL").length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText("150.00 USDC")).toBeDefined();

    const explorerLink = screen.getByRole("link", { name: /VER NO EXPLORER/i });
    expect(explorerLink).toBeDefined();
    expect(explorerLink.getAttribute("href")).toContain(
      "https://explorer.solana.com/address/Dos7xNqW8vGkE6y2k8D2Vp9k1p4mQz8wR5tY7u3i4o5p?cluster=devnet"
    );
  });

  it("copies address to clipboard with temporary feedback in connected revenue tab", async () => {
    window.location.hash = "#revenue";
    vi.spyOn(usePlemuAuthModule, "usePlemuAuth").mockReturnValue({
      ...defaultAuthState,
      isConnected: true,
      base58Address: "Dos7xNqW8vGkE6y2k8D2Vp9k1p4mQz8wR5tY7u3i4o5p",
      shortAddress: "Dos7...4o5p",
      solBalance: 2.0,
      usdcBalanceFormatted: "50.00 USDC",
    });

    render(<CreatorPage />);
    const copyBtn = screen.getByRole("button", { name: /^COPIAR$/i });
    expect(copyBtn).toBeDefined();

    await act(async () => {
      fireEvent.click(copyBtn);
    });

    expect(navigator.clipboard.writeText).toHaveBeenCalledWith(
      "Dos7xNqW8vGkE6y2k8D2Vp9k1p4mQz8wR5tY7u3i4o5p"
    );
    expect(screen.getByText("COPIADO!")).toBeDefined();

    act(() => {
      vi.advanceTimersByTime(2100);
    });

    expect(screen.getByRole("button", { name: /^COPIAR$/i })).toBeDefined();
  });

  it("calls refreshBalances and disconnect from connected revenue tab", () => {
    window.location.hash = "#revenue";
    vi.spyOn(usePlemuAuthModule, "usePlemuAuth").mockReturnValue({
      ...defaultAuthState,
      isConnected: true,
      base58Address: "Dos7xNqW8vGkE6y2k8D2Vp9k1p4mQz8wR5tY7u3i4o5p",
      shortAddress: "Dos7...4o5p",
      solBalance: 2.0,
      usdcBalanceFormatted: "50.00 USDC",
    });

    render(<CreatorPage />);

    const refreshBtn = screen.getByRole("button", { name: /ATUALIZAR SALDOS/i });
    fireEvent.click(refreshBtn);
    expect(mockRefreshBalances).toHaveBeenCalledWith(true);

    const disconnectBtn = screen.getByRole("button", { name: /DESCONECTAR/i });
    fireEvent.click(disconnectBtn);
    expect(mockDisconnect).toHaveBeenCalled();
  });

  it("displays CARREGANDO... when isLoadingBalance is true in connected revenue tab", () => {
    window.location.hash = "#revenue";
    vi.spyOn(usePlemuAuthModule, "usePlemuAuth").mockReturnValue({
      ...defaultAuthState,
      isConnected: true,
      base58Address: "Dos7xNqW8vGkE6y2k8D2Vp9k1p4mQz8wR5tY7u3i4o5p",
      shortAddress: "Dos7...4o5p",
      isLoadingBalance: true,
      solBalance: null,
      usdcBalanceFormatted: null,
    });

    render(<CreatorPage />);

    expect(screen.getAllByText("CARREGANDO...").length).toBeGreaterThanOrEqual(1);
    const updateBtn = screen.getByRole("button", { name: /ATUALIZANDO.../i });
    expect(updateBtn).toBeDefined();
    expect(updateBtn.hasAttribute("disabled")).toBe(true);
  });

  it("displays '--- USDC' fallback when usdcBalanceFormatted is null and not loading", () => {
    window.location.hash = "#revenue";
    vi.spyOn(usePlemuAuthModule, "usePlemuAuth").mockReturnValue({
      ...defaultAuthState,
      isConnected: true,
      base58Address: "Dos7xNqW8vGkE6y2k8D2Vp9k1p4mQz8wR5tY7u3i4o5p",
      shortAddress: "Dos7...4o5p",
      isLoadingBalance: false,
      solBalance: null,
      usdcBalanceFormatted: null,
    });

    render(<CreatorPage />);

    expect(screen.getByText("--- USDC")).toBeDefined();
  });

  it("falls back to document.execCommand when navigator.clipboard is unavailable", async () => {
    window.location.hash = "#revenue";
    vi.spyOn(usePlemuAuthModule, "usePlemuAuth").mockReturnValue({
      ...defaultAuthState,
      isConnected: true,
      base58Address: "Dos7xNqW8vGkE6y2k8D2Vp9k1p4mQz8wR5tY7u3i4o5p",
      shortAddress: "Dos7...4o5p",
    });

    const originalClipboard = navigator.clipboard;
    // @ts-expect-error simulating legacy browser without navigator.clipboard
    delete navigator.clipboard;

    const execCommandSpy = vi.fn().mockReturnValue(true);
    document.execCommand = execCommandSpy;

    render(<CreatorPage />);
    const copyBtn = screen.getByRole("button", { name: /^COPIAR$/i });

    await act(async () => {
      fireEvent.click(copyBtn);
    });

    expect(execCommandSpy).toHaveBeenCalledWith("copy");
    expect(screen.getByText("COPIADO!")).toBeDefined();

    // Restore
    Object.defineProperty(navigator, "clipboard", {
      value: originalClipboard,
      writable: true,
      configurable: true,
    });
  });
});
