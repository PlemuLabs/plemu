"use client";

import React, { FC, useState, useEffect, useRef, useCallback } from "react";
import { useWalletModal } from "@solana/wallet-adapter-react-ui";
import { usePlemuAuth } from "../hooks/usePlemuAuth";
import { SOLANA_CONFIG } from "../lib/solana-config";
import { getExplorerUrl, formatSol } from "../lib/solana-utils";

export interface WalletHeaderButtonProps {
  className?: string;
  onConnect?: () => void;
}

export const WalletHeaderButton: FC<WalletHeaderButtonProps> = ({
  className,
  onConnect,
}) => {
  const [mounted, setMounted] = useState(false);
  const [dropdownOpen, setDropdownOpen] = useState(false);
  const [copied, setCopied] = useState(false);

  const copyTimeoutRef = useRef<NodeJS.Timeout | number | null>(null);
  const dropdownRef = useRef<HTMLDivElement>(null);

  const { setVisible } = useWalletModal();
  const {
    isConnected,
    shortAddress,
    base58Address,
    lamports,
    solBalance,
    isLoadingBalance,
    disconnect,
    refreshBalances,
  } = usePlemuAuth();

  useEffect(() => {
    setMounted(true);
    return () => {
      if (copyTimeoutRef.current) {
        clearTimeout(copyTimeoutRef.current);
      }
    };
  }, []);

  useEffect(() => {
    if (!dropdownOpen) return;

    const handleClickOutside = (event: MouseEvent) => {
      if (
        dropdownRef.current &&
        !dropdownRef.current.contains(event.target as Node)
      ) {
        setDropdownOpen(false);
      }
    };

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setDropdownOpen(false);
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    document.addEventListener("keydown", handleKeyDown);

    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [dropdownOpen]);

  const handleConnectClick = useCallback(() => {
    if (typeof document !== "undefined") {
      const openDialogs =
        document.querySelectorAll<HTMLDialogElement>("dialog[open]");
      openDialogs.forEach((dialog) => {
        try {
          dialog.close();
        } catch {
          // Ignore if closing fails or already closed
        }
      });
    }
    onConnect?.();
    setVisible(true);
  }, [onConnect, setVisible]);

  const handleCopy = useCallback(async () => {
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
      setCopied(true);
      if (copyTimeoutRef.current) {
        clearTimeout(copyTimeoutRef.current);
      }
      copyTimeoutRef.current = setTimeout(() => {
        setCopied(false);
      }, 2000);
    } catch {
      // Fallback silently if clipboard permissions are denied
    }
  }, [base58Address]);

  const handleRefresh = useCallback(async () => {
    await refreshBalances(true);
  }, [refreshBalances]);

  const handleDisconnect = useCallback(async () => {
    setDropdownOpen(false);
    await disconnect();
  }, [disconnect]);

  if (!mounted || !isConnected) {
    return (
      <button
        type="button"
        className={`wallet ${className || ""}`.trim()}
        onClick={handleConnectClick}
        disabled={!mounted}
        aria-busy={!mounted}
        suppressHydrationWarning
      >
        CONECTAR CARTEIRA
      </button>
    );
  }

  const displayBalance =
    lamports !== null
      ? formatSol(lamports, 2)
      : solBalance !== null
        ? `${solBalance.toFixed(2)} SOL`
        : "--- SOL";

  const explorerUrl = base58Address
    ? getExplorerUrl(SOLANA_CONFIG.network, "address", base58Address)
    : "#";

  return (
    <div
      ref={dropdownRef}
      className={`wallet-connected-container ${className || ""}`.trim()}
    >
      <button
        type="button"
        className="wallet-pill-button"
        onClick={() => setDropdownOpen((prev) => !prev)}
        aria-expanded={dropdownOpen}
        aria-haspopup="true"
      >
        <span className="wallet-pill-status">
          <span className="wallet-pill-dot" />
          <span className="wallet-pill-status-text">ONLINE</span>
        </span>
        <span className="wallet-pill-address">{shortAddress}</span>
        <span className="wallet-pill-balance">{displayBalance}</span>
        <span className={`wallet-pill-arrow ${dropdownOpen ? "open" : ""}`}>
          ▼
        </span>
      </button>

      {dropdownOpen && (
        <div className="wallet-dropdown-menu" role="menu">
          <button
            type="button"
            className="wallet-dropdown-item"
            role="menuitem"
            onClick={handleCopy}
          >
            <span className="wallet-dropdown-icon">📋</span>
            <span>{copied ? "Copiado!" : "Copiar Endereço"}</span>
          </button>

          <a
            href={explorerUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="wallet-dropdown-item"
            role="menuitem"
            onClick={() => setDropdownOpen(false)}
          >
            <span className="wallet-dropdown-icon">↗</span>
            <span>Ver no Solana Explorer</span>
          </a>

          <button
            type="button"
            className="wallet-dropdown-item"
            role="menuitem"
            onClick={handleRefresh}
            disabled={isLoadingBalance}
          >
            <span className="wallet-dropdown-icon">⟳</span>
            <span>
              {isLoadingBalance ? "Atualizando..." : "Atualizar Saldos"}
            </span>
          </button>

          <button
            type="button"
            className="wallet-dropdown-item disconnect"
            role="menuitem"
            onClick={handleDisconnect}
          >
            <span className="wallet-dropdown-icon">✕</span>
            <span>Desconectar</span>
          </button>
        </div>
      )}
    </div>
  );
};
