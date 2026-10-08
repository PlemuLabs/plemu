import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, act } from "@testing-library/react";
import Home from "../page";
import * as usePlemuAuthModule from "../../hooks/usePlemuAuth";
import * as walletAdapterUiModule from "@solana/wallet-adapter-react-ui";

vi.mock("../../hooks/usePlemuAuth");
vi.mock("@solana/wallet-adapter-react-ui", () => ({
  useWalletModal: vi.fn(),
}));

describe("Home Page with WalletHeaderButton Integration", () => {
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

    vi.spyOn(usePlemuAuthModule, "usePlemuAuth").mockReturnValue(defaultAuthState);
  });

  it("does not render the legacy 'INTEGRAÇÃO EM BREVE' dialog", () => {
    render(<Home />);
    expect(screen.queryByText("INTEGRAÇÃO EM BREVE")).toBeNull();
  });

  it("renders WalletHeaderButton in header in disconnected state", () => {
    render(<Home />);
    const walletBtn = screen.getByRole("button", { name: /CONECTAR CARTEIRA/i });
    expect(walletBtn).toBeDefined();

    fireEvent.click(walletBtn);
    expect(mockSetVisible).toHaveBeenCalledWith(true);
  });

  it("renders connected wallet state in header when wallet is connected", () => {
    vi.spyOn(usePlemuAuthModule, "usePlemuAuth").mockReturnValue({
      ...defaultAuthState,
      isConnected: true,
      base58Address: "Dos7xNqW8vGkE6y2k8D2Vp9k1p4mQz8wR5tY7u3i4o5p",
      shortAddress: "Dos7...4o5p",
      lamports: 2_500_000_000n,
      solBalance: 2.5,
    });

    render(<Home />);
    expect(screen.getByText("ONLINE")).toBeDefined();
    expect(screen.getByText("Dos7...4o5p")).toBeDefined();
    expect(screen.getByText("2.50 SOL")).toBeDefined();
  });
});
