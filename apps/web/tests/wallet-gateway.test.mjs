import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createWalletGateway } from '../lib/wallet-gateway.ts';

test('demo identity carries no real account or signing authority', async () => {
  const gateway = createWalletGateway();
  assert.deepEqual(await gateway.connect('demo'), { ok: true, identity: { kind: 'demo', provider: 'demo' } });
  await gateway.disconnect();
  assert.equal((await gateway.connect('demo')).ok, true);
});
test('unconfigured mobile provider fails closed', async () => {
  const gateway = createWalletGateway();
  const result = await gateway.connect('mobile-wallet-adapter');
  assert.equal(result.ok, false);
  assert.equal(result.code, 'PROVIDER_UNAVAILABLE');
});
test('late wallet authorization after disconnect is released; overlapping handoffs blocked', async () => {
  let resolve;
  let disconnected = 0;
  const gateway = createWalletGateway([{ id: 'test', connect: () => new Promise(r => resolve = r), disconnect: async () => { disconnected++; } }]);
  const pending = gateway.connect('test');
  assert.equal((await gateway.connect('test')).code, 'CONNECTION_FAILED');
  await gateway.disconnect();
  resolve({ kind: 'solana', provider: 'test', address: 'test-public-address', chain: 'solana:devnet' });
  assert.equal((await pending).code, 'USER_CANCELLED');
  assert.equal(disconnected, 1);
});
