import { describe, it, expect, vi } from "vitest";
import { PublicKey, Connection } from "@solana/web3.js";
import {
  truncateAddress,
  lamportsToSol,
  formatSol,
  usdcUnitsToDecimal,
  formatUsdcAmount,
  getExplorerUrl,
  fetchSolBalance,
  fetchUsdcBalance,
} from "../solana-utils";

describe("solana-utils", () => {
  describe("truncateAddress", () => {
    it("returns empty string when address is null or undefined", () => {
      expect(truncateAddress(null)).toBe("");
      expect(truncateAddress(undefined)).toBe("");
      expect(truncateAddress("")).toBe("");
    });

    it("truncates base58 address string with default 4 characters on each side", () => {
      const address = "Dos7Fv3w3vAEvb6LpBvZ4c5aX8bK9p1q2r3s4t5u6v7w";
      expect(truncateAddress(address)).toBe("Dos7...6v7w");
    });

    it("accepts custom prefix and suffix lengths", () => {
      const address = "Dos7Fv3w3vAEvb6LpBvZ4c5aX8bK9p1q2r3s4t5u6v7w";
      expect(truncateAddress(address, 6, 3)).toBe("Dos7Fv...v7w");
    });

    it("handles PublicKey instance directly", () => {
      const pubkey = new PublicKey("11111111111111111111111111111111");
      expect(truncateAddress(pubkey)).toBe("1111...1111");
    });

    it("returns full string if address length is shorter than prefix + suffix + ellipsis", () => {
      expect(truncateAddress("short")).toBe("short");
    });
  });

  describe("lamportsToSol", () => {
    it("converts 1_000_000_000 lamports to 1 SOL", () => {
      expect(lamportsToSol(1_000_000_000n)).toBe(1);
      expect(lamportsToSol(1_000_000_000)).toBe(1);
    });

    it("converts fractional lamports correctly", () => {
      expect(lamportsToSol(500_000_000n)).toBe(0.5);
      expect(lamportsToSol(123_456_789n)).toBeCloseTo(0.123456789);
    });

    it("converts 0 lamports to 0", () => {
      expect(lamportsToSol(0n)).toBe(0);
      expect(lamportsToSol(0)).toBe(0);
    });
  });

  describe("formatSol", () => {
    it("formats whole and fractional SOL with 4 decimals by default", () => {
      expect(formatSol(1_000_000_000n)).toBe("1.0000 SOL");
      expect(formatSol(1_500_000_000n)).toBe("1.5000 SOL");
      expect(formatSol(0n)).toBe("0.0000 SOL");
    });

    it("respects custom decimal places", () => {
      expect(formatSol(1_234_567_890n, 2)).toBe("1.23 SOL");
      expect(formatSol(1_234_567_890n, 6)).toBe("1.234568 SOL");
    });
  });

  describe("usdcUnitsToDecimal", () => {
    it("converts 1_000_000 units to 1 USDC", () => {
      expect(usdcUnitsToDecimal(1_000_000n)).toBe(1);
      expect(usdcUnitsToDecimal(1_000_000)).toBe(1);
    });

    it("converts fractional micro-units", () => {
      expect(usdcUnitsToDecimal(500_000n)).toBe(0.5);
      expect(usdcUnitsToDecimal(10_250_000n)).toBe(10.25);
    });

    it("converts 0 units to 0", () => {
      expect(usdcUnitsToDecimal(0n)).toBe(0);
    });
  });

  describe("formatUsdcAmount", () => {
    it("formats USDC with 2 decimals by default", () => {
      expect(formatUsdcAmount(1_000_000n)).toBe("1.00 USDC");
      expect(formatUsdcAmount(5_250_000n)).toBe("5.25 USDC");
      expect(formatUsdcAmount(0n)).toBe("0.00 USDC");
    });

    it("respects custom decimals", () => {
      expect(formatUsdcAmount(5_123_456n, 4)).toBe("5.1235 USDC");
    });
  });

  describe("getExplorerUrl", () => {
    it("builds devnet address URL with cluster query parameter", () => {
      const url = getExplorerUrl(
        "devnet",
        "address",
        "Dos7Fv3w3vAEvb6LpBvZ4c5aX8bK9p1q2r3s4t5u6v7w",
      );
      expect(url).toBe(
        "https://explorer.solana.com/address/Dos7Fv3w3vAEvb6LpBvZ4c5aX8bK9p1q2r3s4t5u6v7w?cluster=devnet",
      );
    });

    it("builds mainnet address URL without cluster query parameter", () => {
      const url = getExplorerUrl(
        "mainnet-beta",
        "address",
        "Dos7Fv3w3vAEvb6LpBvZ4c5aX8bK9p1q2r3s4t5u6v7w",
      );
      expect(url).toBe(
        "https://explorer.solana.com/address/Dos7Fv3w3vAEvb6LpBvZ4c5aX8bK9p1q2r3s4t5u6v7w",
      );
    });

    it("builds transaction URL correctly", () => {
      const url = getExplorerUrl("devnet", "tx", "5h6x...txhash");
      expect(url).toBe(
        "https://explorer.solana.com/tx/5h6x...txhash?cluster=devnet",
      );
    });
  });

  describe("fetchSolBalance", () => {
    const mockPubkey = new PublicKey(
      "Dos7Fv3w3vAEvb6LpBvZ4c5aX8bK9p1q2r3s4t5u6v7w",
    );

    it("queries getBalance and returns bigint lamports", async () => {
      const mockConnection = {
        getBalance: vi.fn().mockResolvedValue(2_500_000_000),
      } as unknown as Connection;

      const balance = await fetchSolBalance(mockConnection, mockPubkey);
      expect(mockConnection.getBalance).toHaveBeenCalledWith(mockPubkey);
      expect(balance).toBe(2_500_000_000n);
    });

    it("returns null when getBalance fails with RPC error or rate limit", async () => {
      const mockConnection = {
        getBalance: vi
          .fn()
          .mockRejectedValue(new Error("429 Too Many Requests")),
      } as unknown as Connection;

      const balance = await fetchSolBalance(mockConnection, mockPubkey);
      expect(balance).toBeNull();
    });
  });

  describe("fetchUsdcBalance", () => {
    const mockPubkey = new PublicKey(
      "Dos7Fv3w3vAEvb6LpBvZ4c5aX8bK9p1q2r3s4t5u6v7w",
    );
    const mockUsdcMint = new PublicKey(
      "4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU",
    );

    it("queries getTokenAccountBalance for ATA and returns bigint units", async () => {
      const mockConnection = {
        getTokenAccountBalance: vi.fn().mockResolvedValue({
          value: {
            amount: "15500000",
            decimals: 6,
            uiAmount: 15.5,
            uiAmountString: "15.5",
          },
        }),
      } as unknown as Connection;

      const balance = await fetchUsdcBalance(
        mockConnection,
        mockPubkey,
        mockUsdcMint,
      );
      expect(mockConnection.getTokenAccountBalance).toHaveBeenCalledTimes(1);
      expect(balance).toBe(15_500_000n);
    });

    it("resolves uninitialized ATA with TokenAccountNotFoundError to 0n without throwing", async () => {
      const error = new Error("TokenAccountNotFoundError");
      error.name = "TokenAccountNotFoundError";

      const mockConnection = {
        getTokenAccountBalance: vi.fn().mockRejectedValue(error),
      } as unknown as Connection;

      const balance = await fetchUsdcBalance(
        mockConnection,
        mockPubkey,
        mockUsdcMint,
      );
      expect(balance).toBe(0n);
    });

    it("resolves uninitialized ATA with 'could not find account' RPC error to 0n without throwing", async () => {
      const error = new Error(
        "failed to get token account: Invalid param: could not find account",
      );

      const mockConnection = {
        getTokenAccountBalance: vi.fn().mockRejectedValue(error),
      } as unknown as Connection;

      const balance = await fetchUsdcBalance(
        mockConnection,
        mockPubkey,
        mockUsdcMint,
      );
      expect(balance).toBe(0n);
    });

    it("returns null when an RPC rate limit or network error occurs", async () => {
      const error = new Error("HTTP 429 Too Many Requests");

      const mockConnection = {
        getTokenAccountBalance: vi.fn().mockRejectedValue(error),
      } as unknown as Connection;

      const balance = await fetchUsdcBalance(
        mockConnection,
        mockPubkey,
        mockUsdcMint,
      );
      expect(balance).toBeNull();
    });
  });
});
