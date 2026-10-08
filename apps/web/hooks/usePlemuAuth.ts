"use client";

import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  ReactNode,
} from "react";
import { PublicKey } from "@solana/web3.js";
import { useConnection, useWallet } from "@solana/wallet-adapter-react";
import { useWalletModal } from "@solana/wallet-adapter-react-ui";
import { SOLANA_CONFIG } from "../lib/solana-config";
import {
  fetchSolBalance,
  fetchUsdcBalance,
  formatUsdcAmount,
  lamportsToSol,
  truncateAddress,
} from "../lib/solana-utils";

export interface PlemuAuthState {
  isConnected: boolean;
  isConnecting: boolean;
  walletName: string | null;
  publicKey: PublicKey | null;
  base58Address: string | null;
  shortAddress: string | null;

  lamports: bigint | null;
  solBalance: number | null;
  usdcUnits: bigint | null;
  usdcBalanceFormatted: string | null;
  isLoadingBalance: boolean;

  connect: () => Promise<void>;
  disconnect: () => Promise<void>;
  refreshBalances: (force?: boolean) => Promise<void>;
  signChallenge: (message: string) => Promise<Uint8Array | null>;
}

const REFRESH_DEBOUNCE_MS = 5000;

function usePlemuAuthState(): PlemuAuthState {
  const { connection } = useConnection();
  const {
    connected,
    connecting,
    publicKey,
    wallet,
    connect: walletConnect,
    disconnect: walletDisconnect,
    signMessage,
  } = useWallet();
  const { setVisible } = useWalletModal();

  const [lamports, setLamports] = useState<bigint | null>(null);
  const [usdcUnits, setUsdcUnits] = useState<bigint | null>(null);
  const [isLoadingBalance, setIsLoadingBalance] = useState<boolean>(false);

  const lastFetchTimestampRef = useRef<number>(0);
  const activeFetchIdRef = useRef<number>(0);

  const clearBalances = useCallback(() => {
    setLamports(null);
    setUsdcUnits(null);
    setIsLoadingBalance(false);
    lastFetchTimestampRef.current = 0;
  }, []);

  const refreshBalances = useCallback(
    async (force = false) => {
      if (!publicKey || !connection) {
        clearBalances();
        return;
      }

      const now = Date.now();
      if (!force && now - lastFetchTimestampRef.current < REFRESH_DEBOUNCE_MS) {
        return;
      }

      const currentFetchId = ++activeFetchIdRef.current;
      setIsLoadingBalance(true);
      lastFetchTimestampRef.current = now;

      try {
        const [solResult, usdcResult] = await Promise.all([
          fetchSolBalance(connection, publicKey),
          fetchUsdcBalance(connection, publicKey, SOLANA_CONFIG.usdcMint),
        ]);

        if (activeFetchIdRef.current === currentFetchId) {
          if (solResult !== null) {
            setLamports(solResult);
          }
          if (usdcResult !== null) {
            setUsdcUnits(usdcResult);
          }
        }
      } finally {
        if (activeFetchIdRef.current === currentFetchId) {
          setIsLoadingBalance(false);
        }
      }
    },
    [connection, publicKey, clearBalances]
  );

  const refreshBalancesRef = useRef(refreshBalances);
  useEffect(() => {
    refreshBalancesRef.current = refreshBalances;
  }, [refreshBalances]);

  useEffect(() => {
    if (connected && publicKey) {
      refreshBalancesRef.current(true);
    } else {
      clearBalances();
    }
  }, [connected, publicKey, clearBalances]);

  const connect = useCallback(async () => {
    if (!wallet) {
      setVisible(true);
      return;
    }
    try {
      await walletConnect();
    } catch {
      // Handled silently to avoid unhandled rejections
    }
  }, [wallet, setVisible, walletConnect]);

  const disconnect = useCallback(async () => {
    try {
      await walletDisconnect();
    } finally {
      clearBalances();
    }
  }, [walletDisconnect, clearBalances]);

  const signChallenge = useCallback(
    async (message: string): Promise<Uint8Array | null> => {
      if (!signMessage) {
        return null;
      }
      try {
        const messageBytes = new TextEncoder().encode(message);
        const signature = await signMessage(messageBytes);
        return signature;
      } catch {
        return null;
      }
    },
    [signMessage]
  );

  const base58Address = useMemo(() => {
    return publicKey ? publicKey.toBase58() : null;
  }, [publicKey]);

  const shortAddress = useMemo(() => {
    return publicKey ? truncateAddress(publicKey) : null;
  }, [publicKey]);

  const solBalance = useMemo(() => {
    return lamports !== null ? lamportsToSol(lamports) : null;
  }, [lamports]);

  const usdcBalanceFormatted = useMemo(() => {
    return usdcUnits !== null ? formatUsdcAmount(usdcUnits) : null;
  }, [usdcUnits]);

  const walletName = useMemo(() => {
    return wallet?.adapter?.name ?? null;
  }, [wallet]);

  return {
    isConnected: connected,
    isConnecting: connecting,
    walletName,
    publicKey,
    base58Address,
    shortAddress,

    lamports,
    solBalance,
    usdcUnits,
    usdcBalanceFormatted,
    isLoadingBalance,

    connect,
    disconnect,
    refreshBalances,
    signChallenge,
  };
}

export const PlemuAuthContext = createContext<PlemuAuthState | null>(null);

export interface PlemuAuthProviderProps {
  children: ReactNode;
}

export function PlemuAuthProvider({ children }: PlemuAuthProviderProps) {
  const authState = usePlemuAuthState();

  return React.createElement(
    PlemuAuthContext.Provider,
    { value: authState },
    children
  );
}

export function usePlemuAuth(): PlemuAuthState {
  const context = useContext(PlemuAuthContext);
  if (!context) {
    throw new Error("usePlemuAuth must be used within a PlemuAuthProvider");
  }
  return context;
}
