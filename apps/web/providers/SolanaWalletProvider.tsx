"use client";

import React, { FC, ReactNode, useMemo, useState, useEffect } from "react";
import {
  ConnectionProvider,
  WalletProvider,
} from "@solana/wallet-adapter-react";
import { WalletModalProvider } from "@solana/wallet-adapter-react-ui";
import { SOLANA_CONFIG } from "../lib/solana-config";
import { PlemuAuthProvider } from "../hooks/usePlemuAuth";

export interface SolanaWalletProviderProps {
  children: ReactNode;
  endpoint?: string;
  autoConnect?: boolean;
}

export const SolanaWalletProvider: FC<SolanaWalletProviderProps> = ({
  children,
  endpoint = SOLANA_CONFIG.rpcUrl,
  autoConnect = true,
}) => {
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
  }, []);

  const rpcEndpoint = useMemo(() => endpoint, [endpoint]);
  const wallets = useMemo(() => [], []);

  return (
    <ConnectionProvider endpoint={rpcEndpoint}>
      <WalletProvider wallets={wallets} autoConnect={mounted && autoConnect}>
        <WalletModalProvider>
          <PlemuAuthProvider>{children}</PlemuAuthProvider>
        </WalletModalProvider>
      </WalletProvider>
    </ConnectionProvider>
  );
};
