/** Provider boundary only. No key custody, signing implementation, or device detection. */
export type WalletIdentity =
  | { kind: "demo"; provider: "demo" }
  | {
      kind: "solana";
      provider: string;
      address: string;
      chain: "solana:devnet";
    };
export type WalletFailure = {
  ok: false;
  code: "PROVIDER_UNAVAILABLE" | "USER_CANCELLED" | "CONNECTION_FAILED";
  message: string;
};
export type WalletResult =
  { ok: true; identity: WalletIdentity } | WalletFailure;

/** Future MWA/wallet-standard adapters own authorization and signing in the wallet app.
 * Implementations must not return secrets or treat a display name as verified identity.
 * Signing is optional and only accepts explicit payloads on the authorized devnet session.
 */
export interface WalletConnector {
  id: string;
  connect(signal: AbortSignal): Promise<WalletIdentity>;
  disconnect(): Promise<void>;
  signTransaction?: (
    transaction: Uint8Array,
    signal: AbortSignal,
  ) => Promise<Uint8Array>;
  signMessage?: (
    message: Uint8Array,
    signal: AbortSignal,
  ) => Promise<Uint8Array>;
}
const demoConnector: WalletConnector = {
  id: "demo",
  async connect() {
    return { kind: "demo", provider: "demo" };
  },
  async disconnect() {},
};
export const walletOptions = [
  { id: "demo", label: "Demo wallet", available: true },
  {
    id: "mobile-wallet-adapter",
    label: "Solana mobile wallets",
    available: false,
  },
] as const;

export function createWalletGateway(
  connectors: readonly WalletConnector[] = [demoConnector],
) {
  const registry = new Map(
    connectors.map((connector) => [connector.id, connector]),
  );
  let request: AbortController | null = null;
  let active: WalletConnector | null = null;
  return {
    async connect(id: string): Promise<WalletResult> {
      const connector = registry.get(id);
      if (!connector)
        return {
          ok: false,
          code: "PROVIDER_UNAVAILABLE",
          message:
            "This wallet provider is not available yet. Use the demo wallet.",
        };
      if (request || active)
        return {
          ok: false,
          code: "CONNECTION_FAILED",
          message: "Finish or disconnect the current wallet session first.",
        };
      const controller = new AbortController();
      request = controller;
      try {
        const identity = await connector.connect(controller.signal);
        if (controller.signal.aborted) {
          await connector.disconnect();
          return {
            ok: false,
            code: "USER_CANCELLED",
            message: "Wallet connection was cancelled.",
          };
        }
        active = connector;
        return { ok: true, identity };
      } catch (error) {
        if (controller.signal.aborted)
          return {
            ok: false,
            code: "USER_CANCELLED",
            message: "Wallet connection was cancelled.",
          };
        console.error("PLEMU_WALLET_CONNECT_FAILED", {
          provider: id,
          errorType: error instanceof Error ? error.name : "UnknownError",
        });
        return {
          ok: false,
          code: "CONNECTION_FAILED",
          message: "The wallet could not connect. Try again.",
        };
      } finally {
        if (request === controller) request = null;
      }
    },
    async disconnect() {
      request?.abort();
      // Keep a pending request locked until it releases any late authorization.
      const connector = active;
      active = null;
      if (connector) await connector.disconnect();
    },
  };
}
