import { Connection, PublicKey } from "@solana/web3.js";
import { getAssociatedTokenAddressSync } from "@solana/spl-token";

const LAMPORTS_PER_SOL = 1_000_000_000n;
const USDC_DECIMALS_FACTOR = 1_000_000n;

function isAccountNotFoundError(err: unknown): boolean {
  if (!err) return false;
  if (typeof err === "object") {
    const errorObj = err as { name?: string; message?: string };
    if (errorObj.name === "TokenAccountNotFoundError") {
      return true;
    }
    const message = errorObj.message || "";
    if (
      message.includes("could not find account") ||
      message.includes("TokenAccountNotFoundError") ||
      message.includes("AccountNotFound")
    ) {
      return true;
    }
  }
  return false;
}

export async function fetchSolBalance(
  connection: Connection,
  publicKey: PublicKey,
): Promise<bigint | null> {
  try {
    const lamports = await connection.getBalance(publicKey);
    return BigInt(lamports);
  } catch {
    return null;
  }
}

export async function fetchUsdcBalance(
  connection: Connection,
  publicKey: PublicKey,
  mintAddress: PublicKey | string,
): Promise<bigint | null> {
  try {
    const mint =
      typeof mintAddress === "string"
        ? new PublicKey(mintAddress)
        : mintAddress;
    const ata = getAssociatedTokenAddressSync(mint, publicKey);
    const balanceResponse = await connection.getTokenAccountBalance(ata);
    if (!balanceResponse || !balanceResponse.value) {
      return 0n;
    }
    return BigInt(balanceResponse.value.amount);
  } catch (err: unknown) {
    if (isAccountNotFoundError(err)) {
      return 0n;
    }
    return null;
  }
}

export function truncateAddress(
  address: string | PublicKey | null | undefined,
  prefixLen = 4,
  suffixLen = 4,
): string {
  if (!address) return "";

  const base58 = typeof address === "string" ? address : address.toBase58();
  if (base58.length <= prefixLen + suffixLen + 3) {
    return base58;
  }

  const start = base58.slice(0, prefixLen);
  const end = base58.slice(-suffixLen);
  return `${start}...${end}`;
}

export function lamportsToSol(lamports: bigint | number): number {
  if (typeof lamports === "bigint") {
    return Number(lamports) / 1e9;
  }
  return lamports / 1e9;
}

export function formatSol(lamports: bigint | number, decimals = 4): string {
  const sol = lamportsToSol(lamports);
  return `${sol.toFixed(decimals)} SOL`;
}

export function usdcUnitsToDecimal(units: bigint | number): number {
  if (typeof units === "bigint") {
    return Number(units) / 1e6;
  }
  return units / 1e6;
}

export function formatUsdcAmount(units: bigint | number, decimals = 2): string {
  const amount = usdcUnitsToDecimal(units);
  return `${amount.toFixed(decimals)} USDC`;
}

export function getExplorerUrl(
  cluster: string,
  type: "address" | "tx",
  identifier: string,
): string {
  const isMainnet = cluster === "mainnet-beta" || cluster === "mainnet";
  const clusterSuffix = isMainnet ? "" : `?cluster=${cluster}`;
  return `https://explorer.solana.com/${type}/${identifier}${clusterSuffix}`;
}
