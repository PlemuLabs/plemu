import React from "react";
import { describe, it, expect } from "vitest";
import { render, screen, act } from "@testing-library/react";
import { useConnection, useWallet } from "@solana/wallet-adapter-react";
import { useWalletModal } from "@solana/wallet-adapter-react-ui";
import { SolanaWalletProvider } from "../SolanaWalletProvider";
import { SOLANA_CONFIG } from "../../lib/solana-config";

function TestConsumer() {
  const { connection } = useConnection();
  const { connected, connecting, wallets, select, disconnect } = useWallet();
  const { visible, setVisible } = useWalletModal();

  return (
    <div>
      <div data-testid="endpoint">{connection.rpcEndpoint}</div>
      <div data-testid="connected">{connected ? "true" : "false"}</div>
      <div data-testid="connecting">{connecting ? "true" : "false"}</div>
      <div data-testid="has-select">{typeof select === "function" ? "true" : "false"}</div>
      <div data-testid="has-disconnect">{typeof disconnect === "function" ? "true" : "false"}</div>
      <div data-testid="wallets-count">{wallets.length}</div>
      <div data-testid="modal-visible">{visible ? "true" : "false"}</div>
      <button data-testid="open-modal" onClick={() => setVisible(true)}>
        Open Modal
      </button>
    </div>
  );
}

describe("SolanaWalletProvider", () => {
  it("renders children without throwing", () => {
    render(
      <SolanaWalletProvider>
        <div data-testid="child-element">Child Content</div>
      </SolanaWalletProvider>
    );

    expect(screen.getByTestId("child-element")).not.toBeNull();
    expect(screen.getByTestId("child-element").textContent).toBe("Child Content");
  });

  it("provides ConnectionContext with default Devnet RPC endpoint", () => {
    render(
      <SolanaWalletProvider>
        <TestConsumer />
      </SolanaWalletProvider>
    );

    expect(screen.getByTestId("endpoint").textContent).toBe(SOLANA_CONFIG.rpcUrl);
  });

  it("allows custom endpoint override via props", () => {
    const customEndpoint = "https://custom-rpc.example.com";
    render(
      <SolanaWalletProvider endpoint={customEndpoint}>
        <TestConsumer />
      </SolanaWalletProvider>
    );

    expect(screen.getByTestId("endpoint").textContent).toBe(customEndpoint);
  });

  it("provides WalletContext with initial disconnected state and standard methods", () => {
    render(
      <SolanaWalletProvider>
        <TestConsumer />
      </SolanaWalletProvider>
    );

    expect(screen.getByTestId("connected").textContent).toBe("false");
    expect(screen.getByTestId("connecting").textContent).toBe("false");
    expect(screen.getByTestId("has-select").textContent).toBe("true");
    expect(screen.getByTestId("has-disconnect").textContent).toBe("true");
    expect(screen.getByTestId("wallets-count").textContent).toBe("0");
  });

  it("provides WalletModalContext allowing modal state changes", () => {
    render(
      <SolanaWalletProvider>
        <TestConsumer />
      </SolanaWalletProvider>
    );

    expect(screen.getByTestId("modal-visible").textContent).toBe("false");

    const button = screen.getByTestId("open-modal");
    act(() => {
      button.click();
    });

    expect(screen.getByTestId("modal-visible").textContent).toBe("true");
  });
it("manages internal mounted state cleanly across re-renders", () => {
  const { unmount } = render(
    <SolanaWalletProvider>
      <div data-testid="mounted-child">Mounted</div>
    </SolanaWalletProvider>
  );

  expect(screen.getByTestId("mounted-child").textContent).toBe("Mounted");
  unmount();
});

it("provides PlemuAuthContext allowing children to consume usePlemuAuth cleanly without errors", async () => {
  const { usePlemuAuth } = await import("../../hooks/usePlemuAuth");

  function PlemuAuthConsumer() {
    const auth = usePlemuAuth();
    return (
      <div data-testid="plemu-auth-status">
        {auth.isConnected ? "connected" : "disconnected"}
      </div>
    );
  }

  render(
    <SolanaWalletProvider>
      <PlemuAuthConsumer />
    </SolanaWalletProvider>
  );

  expect(screen.getByTestId("plemu-auth-status").textContent).toBe("disconnected");
});
});
