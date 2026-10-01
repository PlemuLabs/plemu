import {
  address, appendTransactionMessageInstruction, createSolanaRpc, createTransactionMessage, devnet,
  getBase64EncodedWireTransaction, getSignatureFromTransaction, getUtf8Encoder,
  lamports, pipe, setTransactionMessageFeePayerSigner, setTransactionMessageLifetimeUsingBlockhash,
  signTransactionMessageWithSigners, type Rpc, type SolanaRpcApiDevnet, type Signature, type TransactionSigner,
} from '@solana/kit';
import { DevnetError } from './errors.js';

export const DEVNET_RPC = 'https://api.devnet.solana.com';
export const DEVNET_GENESIS = 'EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG';
export const MEMO_PROGRAM = address('MemoSq4gqABAXKb96qnH8TysNcWxMyWCqXgDLGmfcHr');
export const DIAGNOSTIC_MEMO = 'Plemu Labs devnet connection test v1';
type DevnetRpc = Rpc<SolanaRpcApiDevnet>;

export function validateEndpoint(endpoint: string) {
  try {
    const url = new URL(endpoint);
    if (url.protocol !== 'https:' || url.username || url.password || url.hash) throw new Error();
    return endpoint;
  } catch {
    throw new DevnetError('InvalidEndpoint', 'Use an HTTPS Devnet RPC endpoint without userinfo or a fragment.');
  }
}

export async function rpcCall<T>(call: (signal: AbortSignal) => Promise<T>): Promise<T> {
  try {
    return await call(AbortSignal.timeout(15_000));
  } catch {
    throw new DevnetError('RpcUnavailable', 'The Solana RPC request failed or timed out. Check the connection and provider limits.');
  }
}

export class DevnetClient {
  constructor(private readonly rpc: DevnetRpc) {}

  static connect(endpoint = DEVNET_RPC) {
    return new DevnetClient(createSolanaRpc(devnet(validateEndpoint(endpoint))));
  }

  async assertDevnet() {
    const genesis = await rpcCall(abortSignal => this.rpc.getGenesisHash().send({ abortSignal }));
    if (genesis !== DEVNET_GENESIS) {
      throw new DevnetError('WrongCluster', 'The RPC is not Solana Devnet. The operation was refused.');
    }
  }

  async status(walletAddress: string) {
    await this.assertDevnet();
    const { value, context } = await rpcCall(abortSignal => this.rpc
      .getBalance(address(walletAddress), { commitment: 'confirmed' }).send({ abortSignal }));
    return { cluster: 'devnet', address: walletAddress, lamports: value.toString(), slot: context.slot.toString() };
  }

  async confirm(signature: Signature, lastValidBlockHeight?: bigint, timeoutMs = 60_000) {
    const deadline = Date.now() + timeoutMs;
    while (Date.now() < deadline) {
      const { value } = await rpcCall(abortSignal => this.rpc
        .getSignatureStatuses([signature], { searchTransactionHistory: true }).send({ abortSignal }));
      const status = value[0];
      if (status?.err) throw new DevnetError('TransactionFailed', 'The transaction failed on Devnet. Inspect its signature in the explorer.');
      if (status?.confirmationStatus === 'confirmed' || status?.confirmationStatus === 'finalized') return;
      if (lastValidBlockHeight !== undefined) {
        const height = await rpcCall(abortSignal => this.rpc.getBlockHeight({ commitment: 'confirmed' }).send({ abortSignal }));
        if (height > lastValidBlockHeight) throw new DevnetError('TransactionExpired', 'The transaction blockhash expired before confirmation.');
      }
      await new Promise(resolve => setTimeout(resolve, 1_000));
    }
    throw new DevnetError('ConfirmationTimeout', 'Confirmation timed out. Check the reported signature before submitting again.');
  }

  async airdrop(walletAddress: string, onSubmitted: (signature: Signature) => void) {
    await this.assertDevnet();
    let signature: Signature;
    try {
      signature = await rpcCall(abortSignal => this.rpc
        .requestAirdrop(address(walletAddress), lamports(1_000_000_000n)).send({ abortSignal }));
    } catch {
      throw new DevnetError('AirdropUnavailable', 'The test SOL faucet is unavailable or rate limited. Use https://faucet.solana.com with the public wallet address.');
    }
    onSubmitted(signature);
    await this.confirm(signature);
    return signature;
  }

  async memo(signer: TransactionSigner, onSubmitted: (signature: Signature) => void) {
    await this.assertDevnet();
    const { value: balance } = await rpcCall(abortSignal => this.rpc
      .getBalance(signer.address, { commitment: 'confirmed' }).send({ abortSignal }));
    if (balance === 0n) throw new DevnetError('InsufficientFunds', 'Fund this wallet with test SOL before submitting the diagnostic transaction.');
    const { value: lifetime } = await rpcCall(abortSignal => this.rpc
      .getLatestBlockhash({ commitment: 'confirmed' }).send({ abortSignal }));
    const message = pipe(
      createTransactionMessage({ version: 0 }),
      m => setTransactionMessageFeePayerSigner(signer, m),
      m => setTransactionMessageLifetimeUsingBlockhash(lifetime, m),
      m => appendTransactionMessageInstruction({ programAddress: MEMO_PROGRAM, data: getUtf8Encoder().encode(DIAGNOSTIC_MEMO) }, m),
    );
    const transaction = await signTransactionMessageWithSigners(message);
    const signature = getSignatureFromTransaction(transaction);
    onSubmitted(signature);
    await this.assertDevnet();
    await rpcCall(abortSignal => this.rpc.sendTransaction(getBase64EncodedWireTransaction(transaction), {
      encoding: 'base64', preflightCommitment: 'confirmed', skipPreflight: false, maxRetries: 0n,
    }).send({ abortSignal }));
    await this.confirm(signature, lifetime.lastValidBlockHeight);
    return signature;
  }
}
