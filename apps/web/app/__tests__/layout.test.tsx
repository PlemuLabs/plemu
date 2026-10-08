import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { useConnection } from "@solana/wallet-adapter-react";
import RootLayout from "../layout";

vi.mock("../../components/app-shell", () => ({
  AppShell: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

function TestChild() {
  const { connection } = useConnection();
  return <div data-testid="layout-child">{connection.rpcEndpoint}</div>;
}

describe("RootLayout", () => {
  it("renders children wrapped inside SolanaWalletProvider", () => {
    render(
      <RootLayout>
        <TestChild />
      </RootLayout>,
    );

    const child = screen.getByTestId("layout-child");
    expect(child).not.toBeNull();
    expect(child.textContent).toContain("solana.com");
  });
});
