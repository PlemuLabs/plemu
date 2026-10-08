export type SolanaNetwork = "devnet" | "testnet" | "mainnet-beta";

export interface SolanaConfig {
  network: SolanaNetwork;
  rpcUrl: string;
  usdcMint: string;
  mainnetUsdcMint: string;
}

export const DEVNET_USDC_MINT = "4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU";
export const MAINNET_USDC_MINT = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";
export const DEFAULT_DEVNET_RPC = "https://api.devnet.solana.com";
export const DEFAULT_MAINNET_RPC = "https://api.mainnet-beta.solana.com";

export function getSolanaConfig(): SolanaConfig {
  const network =
    (process.env.NEXT_PUBLIC_SOLANA_NETWORK as SolanaNetwork) || "devnet";
  const defaultRpc =
    network === "mainnet-beta" ? DEFAULT_MAINNET_RPC : DEFAULT_DEVNET_RPC;
  const defaultUsdcMint =
    network === "mainnet-beta" ? MAINNET_USDC_MINT : DEVNET_USDC_MINT;

  const rpcUrl = process.env.NEXT_PUBLIC_SOLANA_RPC_URL || defaultRpc;
  const usdcMint = process.env.NEXT_PUBLIC_USDC_MINT || defaultUsdcMint;

  return {
    network,
    rpcUrl,
    usdcMint,
    mainnetUsdcMint: MAINNET_USDC_MINT,
  };
}

export const SOLANA_CONFIG = getSolanaConfig();
