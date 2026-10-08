import React from "react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { PublicKey } from "@solana/web3.js";
import { usePlemuAuth, PlemuAuthProvider } from "../usePlemuAuth";

const mockFetchSolBalance = vi.fn();
const mockFetchUsdcBalance = vi.fn();

vi.mock("../../lib/solana-utils", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../lib/solana-utils")>();
  return {
    ...actual,
    fetchSolBalance: (...args: unknown[]) => mockFetchSolBalance(...args),
    fetchUsdcBalance: (...args: unknown[]) => mockFetchUsdcBalance(...args),
  };
});

const mockSetVisible = vi.fn();
const mockWalletConnect = vi.fn();
const mockWalletDisconnect = vi.fn();
const mockSignMessage = vi.fn();

let mockWalletState = {
  connected: false,
  connecting: false,
  publicKey: null as PublicKey | null,
  wallet: null as { adapter: { name: string } } | null,
  connect: mockWalletConnect,
  disconnect: mockWalletDisconnect,
  signMessage: mockSignMessage as unknown as ((message: Uint8Array) => Promise<Uint8Array>) | undefined,
};

const mockConnection = { rpcEndpoint: "https://api.devnet.solana.com" };

vi.mock("@solana/wallet-adapter-react", () => ({
  useWallet: () => mockWalletState,
  useConnection: () => ({
    connection: mockConnection,
  }),
}));

vi.mock("@solana/wallet-adapter-react-ui", () => ({
  useWalletModal: () => ({
    setVisible: mockSetVisible,
  }),
}));

describe("usePlemuAuth & PlemuAuthProvider", () => {
  const samplePubkey = new PublicKey("Dos7Fv3w3vAEvb6LpBvZ4c5aX8bK9p1q2r3s4t5u6v7w");
  const hookWrapper = ({ children }: { children: React.ReactNode }) => (
    <PlemuAuthProvider>{children}</PlemuAuthProvider>
  );

  beforeEach(() => {
    vi.clearAllMocks();
    mockFetchSolBalance.mockResolvedValue(null);
    mockFetchUsdcBalance.mockResolvedValue(null);
    mockWalletState = {
      connected: false,
      connecting: false,
      publicKey: null,
      wallet: null,
      connect: mockWalletConnect,
      disconnect: mockWalletDisconnect,
      signMessage: mockSignMessage,
    };
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("throws an error when usePlemuAuth is called outside of PlemuAuthProvider", () => {
    expect(() => renderHook(() => usePlemuAuth())).toThrow(
      "usePlemuAuth must be used within a PlemuAuthProvider"
    );
  });

  it("returns initial disconnected state when no wallet is active", () => {
    const { result } = renderHook(() => usePlemuAuth(), { wrapper: hookWrapper });

    expect(result.current.isConnected).toBe(false);
    expect(result.current.isConnecting).toBe(false);
    expect(result.current.publicKey).toBeNull();
    expect(result.current.base58Address).toBeNull();
    expect(result.current.shortAddress).toBeNull();
    expect(result.current.walletName).toBeNull();
    expect(result.current.lamports).toBeNull();
    expect(result.current.solBalance).toBeNull();
    expect(result.current.usdcUnits).toBeNull();
    expect(result.current.usdcBalanceFormatted).toBeNull();
    expect(result.current.isLoadingBalance).toBe(false);
  });

  it("opens wallet modal on connect() if no wallet is selected", async () => {
    const { result } = renderHook(() => usePlemuAuth(), { wrapper: hookWrapper });

    await act(async () => {
      await result.current.connect();
    });

    expect(mockSetVisible).toHaveBeenCalledWith(true);
    expect(mockWalletConnect).not.toHaveBeenCalled();
  });

  it("calls wallet adapter connect() if wallet is already selected", async () => {
    mockWalletState.wallet = { adapter: { name: "Phantom" } };
    mockWalletConnect.mockResolvedValue(undefined);

    const { result } = renderHook(() => usePlemuAuth(), { wrapper: hookWrapper });

    await act(async () => {
      await result.current.connect();
    });

    expect(mockWalletConnect).toHaveBeenCalledTimes(1);
    expect(mockSetVisible).not.toHaveBeenCalled();
  });

  it("calls wallet adapter disconnect() on disconnect()", async () => {
    mockWalletDisconnect.mockResolvedValue(undefined);
    const { result } = renderHook(() => usePlemuAuth(), { wrapper: hookWrapper });

    await act(async () => {
      await result.current.disconnect();
    });

    expect(mockWalletDisconnect).toHaveBeenCalledTimes(1);
  });

  it("fetches SOL and USDC balances upon successful connection", async () => {
    mockFetchSolBalance.mockResolvedValue(1_500_000_000n);
    mockFetchUsdcBalance.mockResolvedValue(25_500_000n);

    mockWalletState = {
      ...mockWalletState,
      connected: true,
      publicKey: samplePubkey,
      wallet: { adapter: { name: "Phantom" } },
    };

    const { result } = renderHook(() => usePlemuAuth(), { wrapper: hookWrapper });

    await act(async () => {
      await Promise.resolve();
    });

    expect(result.current.isConnected).toBe(true);
    expect(result.current.publicKey).toEqual(samplePubkey);
    expect(result.current.base58Address).toBe(samplePubkey.toBase58());
    expect(result.current.shortAddress).toBe("Dos7...6v7w");
    expect(result.current.lamports).toBe(1_500_000_000n);
    expect(result.current.solBalance).toBe(1.5);
    expect(result.current.usdcUnits).toBe(25_500_000n);
    expect(result.current.usdcBalanceFormatted).toBe("25.50 USDC");
    expect(mockFetchSolBalance).toHaveBeenCalledWith(mockConnection, samplePubkey);
    expect(mockFetchUsdcBalance).toHaveBeenCalledWith(
      mockConnection,
      samplePubkey,
      expect.any(String)
    );
  });

  it("shares state and deduplicates balance fetch across multiple consumer components", async () => {
    mockFetchSolBalance.mockResolvedValue(2_000_000_000n);
    mockFetchUsdcBalance.mockResolvedValue(50_000_000n);

    mockWalletState = {
      ...mockWalletState,
      connected: true,
      publicKey: samplePubkey,
      wallet: { adapter: { name: "Phantom" } },
    };

    let consumerAState: any = null;
    let consumerBState: any = null;

    function ConsumerA() {
      consumerAState = usePlemuAuth();
      return null;
    }
    function ConsumerB() {
      consumerBState = usePlemuAuth();
      return null;
    }

    const { render } = await import("@testing-library/react");
    render(
      React.createElement(
        PlemuAuthProvider,
        null,
        React.createElement(ConsumerA, null),
        React.createElement(ConsumerB, null)
      )
    );

    await act(async () => {
      await Promise.resolve();
    });

    expect(consumerAState.isConnected).toBe(true);
    expect(consumerBState.isConnected).toBe(true);
    expect(consumerAState.solBalance).toBe(2);
    expect(consumerBState.solBalance).toBe(2);
    expect(consumerAState.usdcBalanceFormatted).toBe("50.00 USDC");
    expect(consumerBState.usdcBalanceFormatted).toBe("50.00 USDC");
    expect(mockFetchSolBalance).toHaveBeenCalledTimes(1);
    expect(mockFetchUsdcBalance).toHaveBeenCalledTimes(1);
  });

  it("enforces debounce on manual refreshBalances unless force=true", async () => {
    vi.useFakeTimers();
    mockFetchSolBalance.mockResolvedValue(1_000_000_000n);
    mockFetchUsdcBalance.mockResolvedValue(10_000_000n);

    mockWalletState = {
      ...mockWalletState,
      connected: true,
      publicKey: samplePubkey,
      wallet: { adapter: { name: "Phantom" } },
    };

    const { result } = renderHook(() => usePlemuAuth(), { wrapper: hookWrapper });

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1);
    });

    expect(mockFetchSolBalance).toHaveBeenCalledTimes(1);

    // Call refresh immediately without force -> debounced
    await act(async () => {
      await result.current.refreshBalances(false);
    });
    expect(mockFetchSolBalance).toHaveBeenCalledTimes(1);

    // Advance 3 seconds (< 5s) -> debounced
    await vi.advanceTimersByTimeAsync(3000);
    await act(async () => {
      await result.current.refreshBalances(false);
    });
    expect(mockFetchSolBalance).toHaveBeenCalledTimes(1);

    // Advance past 5s -> allowed
    await vi.advanceTimersByTimeAsync(2500);
    await act(async () => {
      await result.current.refreshBalances(false);
    });
    expect(mockFetchSolBalance).toHaveBeenCalledTimes(2);

    // Force=true bypasses debounce
    await act(async () => {
      await result.current.refreshBalances(true);
    });
    expect(mockFetchSolBalance).toHaveBeenCalledTimes(3);
  });

  it("clears balances and state when wallet disconnects", async () => {
    mockFetchSolBalance.mockResolvedValue(1_000_000_000n);
    mockFetchUsdcBalance.mockResolvedValue(10_000_000n);

    mockWalletState = {
      ...mockWalletState,
      connected: true,
      publicKey: samplePubkey,
      wallet: { adapter: { name: "Phantom" } },
    };

    const { result, rerender } = renderHook(() => usePlemuAuth(), { wrapper: hookWrapper });

    await act(async () => {
      await Promise.resolve();
    });

    expect(result.current.isConnected).toBe(true);
    expect(result.current.lamports).toBe(1_000_000_000n);

    // Simulate disconnect
    mockWalletState = {
      ...mockWalletState,
      connected: false,
      publicKey: null,
      wallet: null,
    };

    rerender();

    expect(result.current.isConnected).toBe(false);
    expect(result.current.publicKey).toBeNull();
    expect(result.current.base58Address).toBeNull();
    expect(result.current.shortAddress).toBeNull();
    expect(result.current.lamports).toBeNull();
    expect(result.current.solBalance).toBeNull();
    expect(result.current.usdcUnits).toBeNull();
    expect(result.current.usdcBalanceFormatted).toBeNull();
  });

  describe("signChallenge", () => {
    it("encodes message to UTF-8 and invokes signMessage", async () => {
      const mockSig = new Uint8Array([1, 2, 3, 4, 5]);
      mockSignMessage.mockResolvedValue(mockSig);

      mockWalletState = {
        ...mockWalletState,
        connected: true,
        publicKey: samplePubkey,
        signMessage: mockSignMessage,
      };

      const { result } = renderHook(() => usePlemuAuth(), { wrapper: hookWrapper });

      let signature: Uint8Array | null = null;
      await act(async () => {
        signature = await result.current.signChallenge("Plemu Auth Challenge\nNonce: 12345");
      });

      expect(signature).toEqual(mockSig);
      expect(mockSignMessage).toHaveBeenCalledTimes(1);
      const passedBytes = mockSignMessage.mock.calls[0][0];
      expect(new TextDecoder().decode(passedBytes)).toBe("Plemu Auth Challenge\nNonce: 12345");
    });

    it("returns null gracefully if user rejects signature without throwing", async () => {
      mockSignMessage.mockRejectedValue(new Error("User rejected the request."));

      mockWalletState = {
        ...mockWalletState,
        connected: true,
        publicKey: samplePubkey,
        signMessage: mockSignMessage,
      };

      const { result } = renderHook(() => usePlemuAuth(), { wrapper: hookWrapper });

      let signature: Uint8Array | null = null;
      await act(async () => {
        signature = await result.current.signChallenge("Test Challenge");
      });

      expect(signature).toBeNull();
    });

    it("returns null if wallet adapter does not support signMessage", async () => {
      mockWalletState = {
        ...mockWalletState,
        connected: true,
        publicKey: samplePubkey,
        signMessage: undefined,
      };

      const { result } = renderHook(() => usePlemuAuth(), { wrapper: hookWrapper });

      let signature: Uint8Array | null = null;
      await act(async () => {
        signature = await result.current.signChallenge("Test Challenge");
      });

      expect(signature).toBeNull();
    });
  });
});
