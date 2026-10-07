import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  generateKeyPairSigner, getCompiledTransactionMessageDecoder, getTransactionDecoder,
  signature, verifySignature, type Rpc, type SolanaRpcApiDevnet,
} from '@solana/kit';
import { DEVNET_GENESIS, DIAGNOSTIC_MEMO, MEMO_PROGRAM, DevnetClient, validateEndpoint } from '../src/devnet.js';
import { DevnetError, publicError } from '../src/errors.js';

type Call = { method: string; args: unknown[] };
function fakeRpc(overrides: Record<string, unknown> = {}) {
  const calls: Call[] = [];
  const responses: Record<string, unknown> = {
    getGenesisHash: DEVNET_GENESIS,
    getBalance: { value: 100_000n, context: { slot: 123n } },
    getLatestBlockhash: { value: { blockhash: '11111111111111111111111111111111', lastValidBlockHeight: 100n } },
    getSignatureStatuses: { value: [{ err: null, confirmationStatus: 'confirmed' }] },
    getBlockHeight: 99n,
    sendTransaction: 'unused',
    ...overrides,
  };
  const rpc = new Proxy({}, {
    get: (_target, method: string) => (...args: unknown[]) => ({
      send: async ({ abortSignal }: { abortSignal: AbortSignal }) => {
        assert.ok(abortSignal instanceof AbortSignal);
        calls.push({ method, args });
        const response = responses[method];
        if (response instanceof Error) throw response;
        return typeof response === 'function' ? response() : response;
      },
    }),
  }) as Rpc<SolanaRpcApiDevnet>;
  return { client: new DevnetClient(rpc), calls };
}
const hasCode = (code: string) => (error: unknown) => error instanceof DevnetError && error.code === code;

test('refuses insecure and malformed endpoints without echoing credentials', () => {
  for (const endpoint of ['http://localhost:8899', 'https://user:secret@example.com', 'invalid', 'https://example.com/#token']) {
    assert.throws(() => validateEndpoint(endpoint), hasCode('InvalidEndpoint'));
  }
});

test('rejects a non-Devnet genesis before reading a wallet or sending an airdrop', async () => {
  const { client, calls } = fakeRpc({ getGenesisHash: 'mainnet-genesis' });
  await assert.rejects(client.status('11111111111111111111111111111111'), hasCode('WrongCluster'));
  await assert.rejects(client.airdrop('11111111111111111111111111111111', () => assert.fail()), hasCode('WrongCluster'));
  assert.deepEqual(calls.map(c => c.method), ['getGenesisHash', 'getGenesisHash']);
});

test('RPC errors are typed and do not disclose provider credentials', async () => {
  const { client } = fakeRpc({ getGenesisHash: new Error('https://provider/?api-key=secret') });
  await assert.rejects(client.assertDevnet(), error => {
    assert.equal(JSON.stringify(publicError(error)).includes('secret'), false);
    return hasCode('RpcUnavailable')(error);
  });
  assert.equal(JSON.stringify(publicError(new Error('/private/wallet.json'))).includes('/private'), false);
});

test('status preserves integer precision and requests confirmed state', async () => {
  const { client, calls } = fakeRpc({ getBalance: { value: 18_446_744_073_709_551_615n, context: { slot: 123n } } });
  const result = await client.status('11111111111111111111111111111111');
  assert.equal(result.lamports, '18446744073709551615');
  assert.deepEqual(calls[1]?.args[1], { commitment: 'confirmed' });
});

test('zero balance never signs or broadcasts a memo', async () => {
  const { client, calls } = fakeRpc({ getBalance: { value: 0n } });
  await assert.rejects(client.memo(await generateKeyPairSigner(), () => assert.fail()), hasCode('InsufficientFunds'));
  assert.equal(calls.some(c => c.method === 'sendTransaction'), false);
});

test('memo signs a real transaction, publishes signature, and confirms with preflight enabled', async () => {
  const { client, calls } = fakeRpc();
  const signer = await generateKeyPairSigner();
  const pending: string[] = [];
  const result = await client.memo(signer, sig => pending.push(sig));
  assert.equal(pending[0], result);
  assert.equal(result.length > 64, true);
  const send = calls.find(c => c.method === 'sendTransaction');
  const transaction = getTransactionDecoder().decode(Buffer.from(send?.args[0] as string, 'base64'));
  assert.ok(await verifySignature(signer.keyPair.publicKey, transaction.signatures[signer.address]!, transaction.messageBytes));
  const message = getCompiledTransactionMessageDecoder().decode(transaction.messageBytes);
  assert.equal(message.version, 0);
  assert.equal(message.instructions.length, 1);
  const instruction = message.instructions[0]!;
  assert.equal(message.staticAccounts[instruction.programAddressIndex], MEMO_PROGRAM);
  assert.equal(new TextDecoder().decode(new Uint8Array(instruction.data!)), DIAGNOSTIC_MEMO);
  assert.deepEqual(send?.args[1], {
    encoding: 'base64', preflightCommitment: 'confirmed', skipPreflight: false, maxRetries: 0n,
  });
  assert.equal(calls.filter(c => c.method === 'getGenesisHash').length, 2);
});

test('an endpoint changing clusters is rejected before submission', async () => {
  let reads = 0;
  const { client, calls } = fakeRpc({ getGenesisHash: () => ++reads === 1 ? DEVNET_GENESIS : 'other-cluster' });
  await assert.rejects(client.memo(await generateKeyPairSigner(), () => {}), hasCode('WrongCluster'));
  assert.equal(calls.some(c => c.method === 'sendTransaction'), false);
});

test('a submission timeout preserves the signature and does not resubmit', async () => {
  const { client, calls } = fakeRpc({ sendTransaction: new Error('timeout') });
  const pending: string[] = [];
  await assert.rejects(client.memo(await generateKeyPairSigner(), sig => pending.push(sig)), hasCode('RpcUnavailable'));
  assert.equal(pending.length, 1);
  assert.equal(calls.filter(c => c.method === 'sendTransaction').length, 1);
});

const testSignature = signature('1'.repeat(64));
test('failed, expired and timed out transactions never report confirmation', async () => {
  await assert.rejects(fakeRpc({ getSignatureStatuses: { value: [{ err: { InstructionError: [0, 'error'] } }] } })
    .client.confirm(testSignature), hasCode('TransactionFailed'));
  await assert.rejects(fakeRpc({ getSignatureStatuses: { value: [null] }, getBlockHeight: 101n })
    .client.confirm(testSignature, 100n), hasCode('TransactionExpired'));
  await assert.rejects(fakeRpc().client.confirm(testSignature, undefined, 0), hasCode('ConfirmationTimeout'));
});

test('airdrop rate limits give an actionable error without retries', async () => {
  const { client, calls } = fakeRpc({ requestAirdrop: new Error('429') });
  await assert.rejects(client.airdrop('11111111111111111111111111111111', () => assert.fail()), hasCode('AirdropUnavailable'));
  assert.equal(calls.filter(c => c.method === 'requestAirdrop').length, 1);
});
