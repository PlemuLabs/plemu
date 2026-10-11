import { describe, it, expect, beforeEach, afterEach } from "vitest";
import {
  getSolanaConfig,
  DEVNET_USDC_MINT,
  MAINNET_USDC_MINT,
} from "../solana-config";

describe("solana-config", () => {
  const originalEnv = process.env;

  beforeEach(() => {
    process.env = { ...originalEnv };
  });

  afterEach(() => {
    process.env = originalEnv;
  });

  it("returns default devnet configuration when environment variables are not set", () => {
    delete process.env.NEXT_PUBLIC_SOLANA_NETWORK;
    delete process.env.NEXT_PUBLIC_SOLANA_RPC_URL;
    delete process.env.NEXT_PUBLIC_USDC_MINT;

    const config = getSolanaConfig();

    expect(config.network).toBe("devnet");
    expect(config.rpcUrl).toBe("https://api.devnet.solana.com");
    expect(config.usdcMint).toBe(DEVNET_USDC_MINT);
    expect(config.mainnetUsdcMint).toBe(MAINNET_USDC_MINT);
  });

  it("reads configuration from environment variables when present", () => {
    process.env.NEXT_PUBLIC_SOLANA_NETWORK = "mainnet-beta";
    process.env.NEXT_PUBLIC_SOLANA_RPC_URL = "https://custom-rpc.helius.xyz";
    process.env.NEXT_PUBLIC_USDC_MINT =
      "CustomUsdcMint111111111111111111111111111111";

    const config = getSolanaConfig();

    expect(config.network).toBe("mainnet-beta");
    expect(config.rpcUrl).toBe("https://custom-rpc.helius.xyz");
    expect(config.usdcMint).toBe(
      "CustomUsdcMint111111111111111111111111111111",
    );
  });

  it("dynamically falls back to mainnet defaults when network is mainnet-beta without custom rpc or mint", () => {
    process.env.NEXT_PUBLIC_SOLANA_NETWORK = "mainnet-beta";
    delete process.env.NEXT_PUBLIC_SOLANA_RPC_URL;
    delete process.env.NEXT_PUBLIC_USDC_MINT;

    const config = getSolanaConfig();

    expect(config.network).toBe("mainnet-beta");
    expect(config.rpcUrl).toBe("https://api.mainnet-beta.solana.com");
    expect(config.usdcMint).toBe(MAINNET_USDC_MINT);
  });
});
