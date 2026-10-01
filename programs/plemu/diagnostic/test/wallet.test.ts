import assert from 'node:assert/strict';
import { chmod, mkdtemp, readFile, rm, stat, symlink, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import { DevnetError } from '../src/errors.js';
import { createWallet, loadWallet, proveWallet } from '../src/wallet.js';

const hasCode = (code: string) => (error: unknown) => error instanceof DevnetError && error.code === code;

test('private wallet persists, proves ownership and cannot be overwritten', async t => {
  const directory = await mkdtemp(join(tmpdir(), 'plemu-devnet-test-'));
  t.after(() => rm(directory, { recursive: true, force: true }));
  const path = join(directory, 'wallet.json');
  const address = await createWallet(path);
  assert.equal((await stat(path)).mode & 0o777, 0o600);
  const signer = await loadWallet(path);
  assert.equal(signer.address, address);
  assert.equal((await proveWallet(signer)).verified, true);
  const before = await readFile(path);
  await assert.rejects(createWallet(path), hasCode('WalletExists'));
  assert.deepEqual(await readFile(path), before);
});

test('rejects malformed, inconsistent, world-readable and symlinked wallet files', async t => {
  const directory = await mkdtemp(join(tmpdir(), 'plemu-devnet-test-'));
  t.after(() => rm(directory, { recursive: true, force: true }));
  const path = join(directory, 'wallet.json');
  await assert.rejects(loadWallet(path), hasCode('WalletMissing'));
  await createWallet(path);
  const key: number[] = JSON.parse(await readFile(path, 'utf8'));
  key[63] = (key[63]! + 1) % 256;
  await writeFile(path, JSON.stringify(key));
  await assert.rejects(loadWallet(path), hasCode('InvalidWallet'));
  await writeFile(path, '[300]');
  await assert.rejects(loadWallet(path), hasCode('InvalidWallet'));
  await chmod(path, 0o644);
  await assert.rejects(loadWallet(path), hasCode('UnsafeWalletFile'));
  const link = join(directory, 'link.json');
  await symlink(path, link);
  await assert.rejects(loadWallet(link), hasCode('UnsafeWalletFile'));
});
