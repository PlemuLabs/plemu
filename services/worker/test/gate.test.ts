import { describe, it, expect, vi, beforeEach } from 'vitest';
import worker, { Env, SEAT_DISCRIMINATOR } from '../src/index';
import { Keypair, PublicKey } from '@solana/web3.js';
import nacl from 'tweetnacl';

// Mock AWS S3 client and presigner
vi.mock('@aws-sdk/client-s3', () => ({
  S3Client: vi.fn().mockImplementation(() => ({})),
  GetObjectCommand: vi.fn().mockImplementation((args) => args),
}));

vi.mock('@aws-sdk/s3-request-presigner', () => ({
  getSignedUrl: vi.fn().mockResolvedValue('https://fake-r2.cloudflarestorage.com/presigned-rom?token=xyz'),
}));

// Mock Solana Web3 Connection
const mockGetMultipleAccountsInfo = vi.fn();
vi.mock('@solana/web3.js', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@solana/web3.js')>();
  return {
    ...actual,
    Connection: vi.fn().mockImplementation(() => ({
      getMultipleAccountsInfo: mockGetMultipleAccountsInfo,
    })),
  };
});

describe('The Seat Gate Worker', () => {
  let mockKv: Map<string, string>;
  let env: Env;
  const programId = new PublicKey('11111111111111111111111111111111');

  beforeEach(() => {
    mockKv = new Map<string, string>();
    env = {
      NONCE_KV: {
        get: vi.fn(async (key: string) => mockKv.get(key) || null),
        put: vi.fn(async (key: string, val: string) => {
          mockKv.set(key, val);
        }),
        delete: vi.fn(async (key: string) => {
          mockKv.delete(key);
        }),
      } as unknown as KVNamespace,
      SOLANA_RPC_URL: 'https://api.devnet.solana.com',
      PLEMU_SEATS_PROGRAM_ID: programId.toBase58(),
      R2_ACCOUNT_ID: 'test-account',
      R2_ACCESS_KEY_ID: 'test-key',
      R2_SECRET_ACCESS_KEY: 'test-secret',
      R2_BUCKET_NAME: 'plemu-content',
    };
    mockGetMultipleAccountsInfo.mockReset();
  });

  it('GET /challenge returns a nonce and stores in KV with 300s TTL', async () => {
    const req = new Request('https://worker.plemu.xyz/challenge', { method: 'GET' });
    const res = await worker.fetch(req, env);
    expect(res.status).toBe(200);

    const body = (await res.json()) as { nonce: string; message: string };
    expect(body.nonce).toBeDefined();
    expect(body.message).toContain('Plemu Auth Challenge');
    expect(body.message).toContain(body.nonce);

    expect(env.NONCE_KV.put).toHaveBeenCalledWith(
      `nonce:${body.nonce}`,
      expect.any(String),
      { expirationTtl: 300 }
    );
  });

  it('POST /gate/:packId rejects unknown or expired nonces (anti-replay)', async () => {
    const packId = Keypair.generate().publicKey.toBase58();
    const req = new Request(`https://worker.plemu.xyz/gate/${packId}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        wallet: Keypair.generate().publicKey.toBase58(),
        signature: Buffer.alloc(64).toString('hex'),
        nonce: 'non-existent-nonce',
        sha256: 'abcd1234abcd1234abcd1234abcd1234abcd1234abcd1234abcd1234abcd1234',
      }),
    });

    const res = await worker.fetch(req, env);
    expect(res.status).toBe(401);
    const body = (await res.json()) as { error: string };
    expect(body.error).toBe('challenge-expired-or-replayed');
  });

  it('POST /gate/:packId rejects invalid Ed25519 signatures', async () => {
    const walletKeypair = Keypair.generate();
    const packKeypair = Keypair.generate();
    const nonce = 'test-nonce-1';
    const timestamp = Date.now();

    mockKv.set(`nonce:${nonce}`, JSON.stringify({ timestamp }));

    // Bad signature
    const badSig = Buffer.alloc(64, 0x99).toString('hex');

    const req = new Request(`https://worker.plemu.xyz/gate/${packKeypair.publicKey.toBase58()}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        wallet: walletKeypair.publicKey.toBase58(),
        signature: badSig,
        nonce,
        sha256: '1111222233334444555566667777888899990000aaaabbbbccccddddeeeeffff',
      }),
    });

    const res = await worker.fetch(req, env);
    expect(res.status).toBe(401);
    const body = (await res.json()) as { error: string };
    expect(body.error).toBe('invalid-signature');
    expect(env.NONCE_KV.delete).toHaveBeenCalledWith(`nonce:${nonce}`);
  });

  it('POST /gate/:packId returns 404 when pack account does not exist or has wrong owner', async () => {
    const walletKeypair = Keypair.generate();
    const packKeypair = Keypair.generate();
    const nonce = 'test-nonce-2';
    const timestamp = Date.now();
    mockKv.set(`nonce:${nonce}`, JSON.stringify({ timestamp }));

    const message = `Plemu Auth Challenge\nNonce: ${nonce}\nTimestamp: ${timestamp}`;
    const sig = nacl.sign.detached(new TextEncoder().encode(message), walletKeypair.secretKey);

    // RPC returns null for both accounts
    mockGetMultipleAccountsInfo.mockResolvedValue([null, null]);

    const req = new Request(`https://worker.plemu.xyz/gate/${packKeypair.publicKey.toBase58()}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        wallet: walletKeypair.publicKey.toBase58(),
        signature: Buffer.from(sig).toString('hex'),
        nonce,
        sha256: '1111222233334444555566667777888899990000aaaabbbbccccddddeeeeffff',
      }),
    });

    const res = await worker.fetch(req, env);
    expect(res.status).toBe(404);
    const body = (await res.json()) as { error: string };
    expect(body.error).toBe('pack-not-found');
  });

  it('POST /gate/:packId returns 410 when pack is revoked', async () => {
    const walletKeypair = Keypair.generate();
    const packKeypair = Keypair.generate();
    const nonce = 'test-nonce-revoked';
    const timestamp = Date.now();
    mockKv.set(`nonce:${nonce}`, JSON.stringify({ timestamp }));

    const message = `Plemu Auth Challenge\nNonce: ${nonce}\nTimestamp: ${timestamp}`;
    const sig = nacl.sign.detached(new TextEncoder().encode(message), walletKeypair.secretKey);

    // Pack account data: 73 bytes, byte 72 = 1 (revoked)
    const packData = Buffer.alloc(100, 0);
    packData[72] = 1;

    mockGetMultipleAccountsInfo.mockResolvedValue([
      null,
      { owner: programId, data: packData },
    ]);

    const req = new Request(`https://worker.plemu.xyz/gate/${packKeypair.publicKey.toBase58()}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        wallet: walletKeypair.publicKey.toBase58(),
        signature: Buffer.from(sig).toString('hex'),
        nonce,
        sha256: '1111222233334444555566667777888899990000aaaabbbbccccddddeeeeffff',
      }),
    });

    const res = await worker.fetch(req, env);
    expect(res.status).toBe(410);
    const body = (await res.json()) as { error: string };
    expect(body.error).toBe('pack-revoked');
  });

  it('POST /gate/:packId enforces Anti-Dusting (rejects incorrect owner or invalid discriminator/member)', async () => {
    const walletKeypair = Keypair.generate();
    const packKeypair = Keypair.generate();
    const nonce = 'test-nonce-anti-dust';
    const timestamp = Date.now();
    mockKv.set(`nonce:${nonce}`, JSON.stringify({ timestamp }));

    const message = `Plemu Auth Challenge\nNonce: ${nonce}\nTimestamp: ${timestamp}`;
    const sig = nacl.sign.detached(new TextEncoder().encode(message), walletKeypair.secretKey);

    const validPackData = Buffer.alloc(100, 0); // packRevoked = false

    // Dusted seat: owned by a different program/wallet instead of Plemu Seats Program
    mockGetMultipleAccountsInfo.mockResolvedValue([
      { owner: Keypair.generate().publicKey, data: Buffer.alloc(10) },
      { owner: programId, data: validPackData },
    ]);

    const req = new Request(`https://worker.plemu.xyz/gate/${packKeypair.publicKey.toBase58()}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        wallet: walletKeypair.publicKey.toBase58(),
        signature: Buffer.from(sig).toString('hex'),
        nonce,
        sha256: '1111222233334444555566667777888899990000aaaabbbbccccddddeeeeffff',
      }),
    });

    const res = await worker.fetch(req, env);
    expect(res.status).toBe(402);
    const body = (await res.json()) as { error: string };
    expect(body.error).toBe('seat-not-found-or-dusted');
  });

  it('POST /gate/:packId returns 200 with R2 presigned URL when all checks pass', async () => {
    const walletKeypair = Keypair.generate();
    const packKeypair = Keypair.generate();
    const nonce = 'test-nonce-success';
    const timestamp = Date.now();
    mockKv.set(`nonce:${nonce}`, JSON.stringify({ timestamp }));

    const message = `Plemu Auth Challenge\nNonce: ${nonce}\nTimestamp: ${timestamp}`;
    const sig = nacl.sign.detached(new TextEncoder().encode(message), walletKeypair.secretKey);

    const validPackData = Buffer.alloc(100, 0);

    // Build valid Seat account buffer (8 byte discriminator + 32 byte member + 32 byte pack)
    const seatData = Buffer.alloc(80, 0);
    seatData.set(SEAT_DISCRIMINATOR, 0);
    seatData.set(walletKeypair.publicKey.toBytes(), 8);
    seatData.set(packKeypair.publicKey.toBytes(), 40);

    mockGetMultipleAccountsInfo.mockResolvedValue([
      { owner: programId, data: seatData },
      { owner: programId, data: validPackData },
    ]);

    const req = new Request(`https://worker.plemu.xyz/gate/${packKeypair.publicKey.toBase58()}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        wallet: walletKeypair.publicKey.toBase58(),
        signature: Buffer.from(sig).toString('hex'),
        nonce,
        sha256: '1111222233334444555566667777888899990000aaaabbbbccccddddeeeeffff',
      }),
    });

    const res = await worker.fetch(req, env);
    expect(res.status).toBe(200);

    const body = (await res.json()) as { url: string; expiresInSeconds: number };
    expect(body.url).toContain('https://fake-r2.cloudflarestorage.com/presigned-rom');
    expect(body.expiresInSeconds).toBe(60);
  });
});
