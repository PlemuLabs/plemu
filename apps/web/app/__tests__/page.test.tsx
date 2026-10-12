import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, act } from "@testing-library/react";
import Home from "../page";
import { AppShell } from "../../components/app-shell";
import * as usePlemuAuthModule from "../../hooks/usePlemuAuth";
import * as walletAdapterUiModule from "@solana/wallet-adapter-react-ui";

vi.mock("../../hooks/usePlemuAuth");
vi.mock("next/navigation", () => ({ usePathname: () => "/" }));
vi.mock("../../components/home-experience", () => ({
  HomeExperience: () => <div>Current frontend home</div>,
}));
function renderHome() {
  render(
    <AppShell>
      <Home />
    </AppShell>,
  );
}
vi.mock("@solana/wallet-adapter-react-ui", () => ({
  useWalletModal: vi.fn(),
}));

describe("Shared Frontend Wallet Integration", () => {
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
    window.scrollTo = vi.fn();

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

    vi.mocked(walletAdapterUiModule.useWalletModal).mockReturnValue({
      visible: false,
      setVisible: mockSetVisible,
    });

    vi.spyOn(usePlemuAuthModule, "usePlemuAuth").mockReturnValue(
      defaultAuthState,
    );
  });

  it("does not render the legacy 'INTEGRAÇÃO EM BREVE' dialog", () => {
    renderHome();
    expect(screen.queryByText("INTEGRAÇÃO EM BREVE")).toBeNull();
  });

  it("opens the real wallet picker from the shared wallet dialog", () => {
    renderHome();
    fireEvent.click(screen.getByRole("button", { name: "Wallet" }));
    const walletBtn = screen.getByRole("button", {
      name: /CONNECT WALLET/i,
    });
    expect(walletBtn).toBeDefined();

    fireEvent.click(walletBtn);
    expect(mockSetVisible).toHaveBeenCalledWith(true);
    expect(
      screen.queryByRole("dialog", { name: "Enter the arcade economy" }),
    ).toBeNull();
    expect(screen.queryByRole("button", { name: "Demo wallet" })).toBeNull();
  });

  it("shows the real wallet balance without connecting the demo portfolio", () => {
    vi.spyOn(usePlemuAuthModule, "usePlemuAuth").mockReturnValue({
      ...defaultAuthState,
      isConnected: true,
      base58Address: "Dos7xNqW8vGkE6y2k8D2Vp9k1p4mQz8wR5tY7u3i4o5p",
      shortAddress: "Dos7...4o5p",
      lamports: 2_500_000_000n,
      solBalance: 2.5,
    });

    renderHome();
    fireEvent.click(screen.getByRole("button", { name: "Wallet" }));
    expect(screen.getByText("ONLINE")).toBeDefined();
    expect(screen.getByText("Dos7...4o5p")).toBeDefined();
    expect(screen.getByText("2.50 SOL")).toBeDefined();
  });
});
