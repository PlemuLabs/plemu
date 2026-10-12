import React from "react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  render,
  screen,
  fireEvent,
  act,
  waitFor,
} from "@testing-library/react";
import { WalletHeaderButton } from "../WalletHeaderButton";
import * as usePlemuAuthModule from "../../hooks/usePlemuAuth";
import * as walletAdapterUiModule from "@solana/wallet-adapter-react-ui";

vi.mock("../../hooks/usePlemuAuth");
vi.mock("@solana/wallet-adapter-react-ui", () => ({
  useWalletModal: vi.fn(),
}));

describe("WalletHeaderButton", () => {
  const mockSetVisible = vi.fn();
  const mockDisconnect = vi.fn().mockResolvedValue(undefined);
  const mockRefreshBalances = vi.fn().mockResolvedValue(undefined);
  const mockConnect = vi.fn().mockResolvedValue(undefined);

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

    vi.spyOn(usePlemuAuthModule, "usePlemuAuth").mockReturnValue(
      defaultAuthState,
    );
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("renders disconnected button with text 'CONNECT WALLET' when not connected", () => {
    render(<WalletHeaderButton />);
    const button = screen.getByRole("button", { name: /CONNECT WALLET/i });
    expect(button).toBeDefined();
    expect(button.textContent).toContain("CONNECT WALLET");
  });

  it("closes open HTML5 dialogs and opens wallet modal when 'CONNECT WALLET' is clicked", () => {
    const dialog = document.createElement("dialog");
    dialog.setAttribute("open", "");
    const closeSpy = vi.spyOn(dialog, "close");
    document.body.appendChild(dialog);

    render(<WalletHeaderButton />);
    const button = screen.getByRole("button", { name: /CONNECT WALLET/i });
    fireEvent.click(button);

    expect(closeSpy).toHaveBeenCalledTimes(1);
    expect(mockSetVisible).toHaveBeenCalledWith(true);

    document.body.removeChild(dialog);
  });

  it("renders connected pill with online indicator, truncated address, and SOL balance", () => {
    vi.spyOn(usePlemuAuthModule, "usePlemuAuth").mockReturnValue({
      ...defaultAuthState,
      isConnected: true,
      base58Address: "Dos7xNqW8vGkE6y2k8D2Vp9k1p4mQz8wR5tY7u3i4o5p",
      shortAddress: "Dos7...4o5p",
      lamports: 1_250_000_000n,
      solBalance: 1.25,
      usdcUnits: 5_000_000n,
      usdcBalanceFormatted: "5.00 USDC",
    });

    render(<WalletHeaderButton />);

    expect(screen.getByText("ONLINE")).toBeDefined();
    expect(screen.getByText("Dos7...4o5p")).toBeDefined();
    expect(screen.getByText("1.25 SOL")).toBeDefined();
  });

  it("displays placeholder balance '--- SOL' when connected but balance is null", () => {
    vi.spyOn(usePlemuAuthModule, "usePlemuAuth").mockReturnValue({
      ...defaultAuthState,
      isConnected: true,
      base58Address: "Dos7xNqW8vGkE6y2k8D2Vp9k1p4mQz8wR5tY7u3i4o5p",
      shortAddress: "Dos7...4o5p",
      lamports: null,
      solBalance: null,
    });

    render(<WalletHeaderButton />);

    expect(screen.getByText("--- SOL")).toBeDefined();
  });

  it("toggles dropdown menu when clicking the connected pill", () => {
    vi.spyOn(usePlemuAuthModule, "usePlemuAuth").mockReturnValue({
      ...defaultAuthState,
      isConnected: true,
      base58Address: "Dos7xNqW8vGkE6y2k8D2Vp9k1p4mQz8wR5tY7u3i4o5p",
      shortAddress: "Dos7...4o5p",
      solBalance: 1.25,
    });

    render(<WalletHeaderButton />);

    expect(screen.queryByText("Copy Address")).toBeNull();

    const trigger = screen.getByRole("button", { name: /Dos7\.\.\.4o5p/i });
    fireEvent.click(trigger);

    expect(screen.getByText("Copy Address")).toBeDefined();
    expect(screen.getByText(/View on Solana Explorer/i)).toBeDefined();
    expect(screen.getByText("Refresh Balances")).toBeDefined();
    expect(screen.getByText("Disconnect")).toBeDefined();

    // Click again to close
    fireEvent.click(trigger);
    expect(screen.queryByText("Copy Address")).toBeNull();
  });

  it("copies address to clipboard and shows temporary feedback when 'Copy Address' is clicked", async () => {
    vi.spyOn(usePlemuAuthModule, "usePlemuAuth").mockReturnValue({
      ...defaultAuthState,
      isConnected: true,
      base58Address: "Dos7xNqW8vGkE6y2k8D2Vp9k1p4mQz8wR5tY7u3i4o5p",
      shortAddress: "Dos7...4o5p",
      solBalance: 1.25,
    });

    render(<WalletHeaderButton />);
    const trigger = screen.getByRole("button", { name: /Dos7\.\.\.4o5p/i });
    fireEvent.click(trigger);

    const copyBtn = screen.getByText("Copy Address");
    await act(async () => {
      fireEvent.click(copyBtn);
    });

    expect(navigator.clipboard.writeText).toHaveBeenCalledWith(
      "Dos7xNqW8vGkE6y2k8D2Vp9k1p4mQz8wR5tY7u3i4o5p",
    );
    expect(screen.getByText("Copied!")).toBeDefined();

    act(() => {
      vi.advanceTimersByTime(2100);
    });

    expect(screen.getByText("Copy Address")).toBeDefined();
    expect(screen.queryByText("Copied!")).toBeNull();
  });

  it("falls back to document.execCommand when navigator.clipboard is unavailable in WalletHeaderButton", async () => {
    vi.spyOn(usePlemuAuthModule, "usePlemuAuth").mockReturnValue({
      ...defaultAuthState,
      isConnected: true,
      base58Address: "Dos7xNqW8vGkE6y2k8D2Vp9k1p4mQz8wR5tY7u3i4o5p",
      shortAddress: "Dos7...4o5p",
    });

    const originalClipboard = navigator.clipboard;
    // @ts-expect-error simulating legacy browser
    delete navigator.clipboard;

    const execCommandSpy = vi.fn().mockReturnValue(true);
    document.execCommand = execCommandSpy;

    render(<WalletHeaderButton />);
    const trigger = screen.getByRole("button", { name: /Dos7\.\.\.4o5p/i });
    fireEvent.click(trigger);

    const copyBtn = screen.getByText("Copy Address");
    await act(async () => {
      fireEvent.click(copyBtn);
    });

    expect(execCommandSpy).toHaveBeenCalledWith("copy");
    expect(screen.getByText("Copied!")).toBeDefined();

    // Restore
    Object.defineProperty(navigator, "clipboard", {
      value: originalClipboard,
      writable: true,
      configurable: true,
    });
  });

  it("links to Solana Explorer Devnet with correct address and cluster", () => {
    vi.spyOn(usePlemuAuthModule, "usePlemuAuth").mockReturnValue({
      ...defaultAuthState,
      isConnected: true,
      base58Address: "Dos7xNqW8vGkE6y2k8D2Vp9k1p4mQz8wR5tY7u3i4o5p",
      shortAddress: "Dos7...4o5p",
    });

    render(<WalletHeaderButton />);
    fireEvent.click(screen.getByRole("button", { name: /Dos7\.\.\.4o5p/i }));

    const explorerLink = screen.getByRole("menuitem", {
      name: /View on Solana Explorer/i,
    });
    expect(explorerLink).toBeDefined();
    expect(explorerLink.getAttribute("href")).toContain(
      "https://explorer.solana.com/address/Dos7xNqW8vGkE6y2k8D2Vp9k1p4mQz8wR5tY7u3i4o5p?cluster=devnet",
    );
    expect(explorerLink.getAttribute("target")).toBe("_blank");
    expect(explorerLink.getAttribute("rel")).toContain("noopener");
  });

  it("triggers refreshBalances(true) when 'Refresh Balances' is clicked", () => {
    vi.spyOn(usePlemuAuthModule, "usePlemuAuth").mockReturnValue({
      ...defaultAuthState,
      isConnected: true,
      base58Address: "Dos7xNqW8vGkE6y2k8D2Vp9k1p4mQz8wR5tY7u3i4o5p",
      shortAddress: "Dos7...4o5p",
    });

    render(<WalletHeaderButton />);
    fireEvent.click(screen.getByRole("button", { name: /Dos7\.\.\.4o5p/i }));

    const refreshBtn = screen.getByText("Refresh Balances");
    fireEvent.click(refreshBtn);

    expect(mockRefreshBalances).toHaveBeenCalledWith(true);
  });

  it("shows loading state when isLoadingBalance is true", () => {
    vi.spyOn(usePlemuAuthModule, "usePlemuAuth").mockReturnValue({
      ...defaultAuthState,
      isConnected: true,
      base58Address: "Dos7xNqW8vGkE6y2k8D2Vp9k1p4mQz8wR5tY7u3i4o5p",
      shortAddress: "Dos7...4o5p",
      isLoadingBalance: true,
    });

    render(<WalletHeaderButton />);
    fireEvent.click(screen.getByRole("button", { name: /Dos7\.\.\.4o5p/i }));

    expect(screen.getByText(/Refreshing\.\.\./i)).toBeDefined();
  });

  it("calls disconnect() and closes dropdown when 'Disconnect' is clicked", () => {
    vi.spyOn(usePlemuAuthModule, "usePlemuAuth").mockReturnValue({
      ...defaultAuthState,
      isConnected: true,
      base58Address: "Dos7xNqW8vGkE6y2k8D2Vp9k1p4mQz8wR5tY7u3i4o5p",
      shortAddress: "Dos7...4o5p",
    });

    render(<WalletHeaderButton />);
    const trigger = screen.getByRole("button", { name: /Dos7\.\.\.4o5p/i });
    fireEvent.click(trigger);

    const disconnectBtn = screen.getByText("Disconnect");
    fireEvent.click(disconnectBtn);

    expect(mockDisconnect).toHaveBeenCalledTimes(1);
    expect(screen.queryByText("Disconnect")).toBeNull();
  });

  it("closes dropdown when Escape key is pressed", () => {
    vi.spyOn(usePlemuAuthModule, "usePlemuAuth").mockReturnValue({
      ...defaultAuthState,
      isConnected: true,
      base58Address: "Dos7xNqW8vGkE6y2k8D2Vp9k1p4mQz8wR5tY7u3i4o5p",
      shortAddress: "Dos7...4o5p",
    });

    render(<WalletHeaderButton />);
    const trigger = screen.getByRole("button", { name: /Dos7\.\.\.4o5p/i });
    fireEvent.click(trigger);

    expect(screen.getByText("Copy Address")).toBeDefined();

    fireEvent.keyDown(document, { key: "Escape" });
    expect(screen.queryByText("Copy Address")).toBeNull();
  });

  it("closes dropdown when clicking outside the menu", () => {
    vi.spyOn(usePlemuAuthModule, "usePlemuAuth").mockReturnValue({
      ...defaultAuthState,
      isConnected: true,
      base58Address: "Dos7xNqW8vGkE6y2k8D2Vp9k1p4mQz8wR5tY7u3i4o5p",
      shortAddress: "Dos7...4o5p",
    });

    render(
      <div>
        <div data-testid="outside">Outside Area</div>
        <WalletHeaderButton />
      </div>,
    );

    const trigger = screen.getByRole("button", { name: /Dos7\.\.\.4o5p/i });
    fireEvent.click(trigger);
    expect(screen.getByText("Copy Address")).toBeDefined();

    fireEvent.mouseDown(screen.getByTestId("outside"));
    expect(screen.queryByText("Copy Address")).toBeNull();
  });
});
