// The Worker gates ACCESS. It does not gate AUTHENTICITY.
// A compromised Worker can leak bytes. It cannot forge content: the device
// re-verifies the Ed25519 manifest signature and re-hashes every artifact
// before launch. Never add a code path that asks the device to trust this
// service's word about what content is valid.

import { Connection, PublicKey } from '@solana/web3.js';
import nacl from 'tweetnacl';
import { S3Client, GetObjectCommand } from '@aws-sdk/client-s3';
import { getSignedUrl } from '@aws-sdk/s3-request-presigner';

export interface Env {
  NONCE_KV: KVNamespace;
  SOLANA_RPC_URL: string;
  SOLANA_RPC_FALLBACK_URL?: string;
  PLEMU_SEATS_PROGRAM_ID: string;
  R2_ACCOUNT_ID?: string;
  R2_ACCESS_KEY_ID?: string;
  R2_SECRET_ACCESS_KEY?: string;
  R2_BUCKET_NAME?: string;
}

// 8-byte Anchor discriminator for `Seat` struct: sha256("account:Seat")[0..8]
export const SEAT_DISCRIMINATOR = new Uint8Array([90, 228, 22, 90, 162, 86, 173, 26]);

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);

    // 1. GET /challenge
    if (url.pathname === '/challenge' && request.method === 'GET') {
      const nonce = crypto.randomUUID();
      const timestamp = Date.now();
      const message = `Plemu Auth Challenge\nNonce: ${nonce}\nTimestamp: ${timestamp}`;
      await env.NONCE_KV.put(`nonce:${nonce}`, JSON.stringify({ timestamp }), { expirationTtl: 300 });
      return new Response(JSON.stringify({ nonce, message }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    }

    // 2. POST /gate/:packId
    if (url.pathname.startsWith('/gate/') && request.method === 'POST') {
      const parts = url.pathname.split('/');
      const packId = parts[2];
      if (!packId) {
        return new Response(JSON.stringify({ error: 'missing-pack-id' }), {
          status: 400,
          headers: { 'Content-Type': 'application/json' },
        });
      }

      let body: { wallet?: string; signature?: string; nonce?: string; sha256?: string };
      try {
        body = (await request.json()) as typeof body;
      } catch {
        return new Response(JSON.stringify({ error: 'invalid-json' }), {
          status: 400,
          headers: { 'Content-Type': 'application/json' },
        });
      }

      if (!body.wallet || !body.signature || !body.nonce || !body.sha256) {
        return new Response(JSON.stringify({ error: 'missing-required-fields' }), {
          status: 400,
          headers: { 'Content-Type': 'application/json' },
        });
      }

      // Step A: Anti-replay Nonce Check
      const stored = await env.NONCE_KV.get(`nonce:${body.nonce}`);
      if (!stored) {
        return new Response(JSON.stringify({ error: 'challenge-expired-or-replayed' }), {
          status: 401,
          headers: { 'Content-Type': 'application/json' },
        });
      }
      await env.NONCE_KV.delete(`nonce:${body.nonce}`);

      // Step B: Ed25519 Signature Verification
      const { timestamp } = JSON.parse(stored);
      const message = `Plemu Auth Challenge\nNonce: ${body.nonce}\nTimestamp: ${timestamp}`;
      const msgBytes = new TextEncoder().encode(message);

      let pubkeyBytes: Uint8Array;
      let walletPubkey: PublicKey;
      let packPubkey: PublicKey;

      try {
        walletPubkey = new PublicKey(body.wallet);
        pubkeyBytes = walletPubkey.toBytes();
        packPubkey = new PublicKey(packId);
      } catch {
        return new Response(JSON.stringify({ error: 'invalid-public-key' }), {
          status: 400,
          headers: { 'Content-Type': 'application/json' },
        });
      }

      let sigBytes: Uint8Array;
      try {
        if (/^[0-9a-fA-F]+$/.test(body.signature) && body.signature.length === 128) {
          sigBytes = Uint8Array.from(Buffer.from(body.signature, 'hex'));
        } else {
          sigBytes = Uint8Array.from(Buffer.from(body.signature, 'base64'));
        }
      } catch {
        return new Response(JSON.stringify({ error: 'invalid-signature-encoding' }), {
          status: 400,
          headers: { 'Content-Type': 'application/json' },
        });
      }

      if (sigBytes.length !== 64 || !nacl.sign.detached.verify(msgBytes, sigBytes, pubkeyBytes)) {
        return new Response(JSON.stringify({ error: 'invalid-signature' }), {
          status: 401,
          headers: { 'Content-Type': 'application/json' },
        });
      }

      // Step C: Solana RPC Validation
      let connection = new Connection(env.SOLANA_RPC_URL, 'confirmed');
      const programId = new PublicKey(env.PLEMU_SEATS_PROGRAM_ID);

      const [seatPda] = PublicKey.findProgramAddressSync(
        [Buffer.from('seat'), packPubkey.toBuffer(), walletPubkey.toBuffer()],
        programId
      );

      let accounts;
      try {
        accounts = await connection.getMultipleAccountsInfo([seatPda, packPubkey], 'confirmed');
      } catch {
        if (env.SOLANA_RPC_FALLBACK_URL) {
          try {
            connection = new Connection(env.SOLANA_RPC_FALLBACK_URL, 'confirmed');
            accounts = await connection.getMultipleAccountsInfo([seatPda, packPubkey], 'confirmed');
          } catch {
            return new Response(JSON.stringify({ error: 'rpc-unavailable' }), {
              status: 503,
              headers: { 'Content-Type': 'application/json' },
            });
          }
        } else {
          return new Response(JSON.stringify({ error: 'rpc-unavailable' }), {
            status: 503,
            headers: { 'Content-Type': 'application/json' },
          });
        }
      }

      const [seatAccount, packAccount] = accounts;

      // Step D: Validate Pack Status
      if (!packAccount || !packAccount.owner.equals(programId)) {
        return new Response(JSON.stringify({ error: 'pack-not-found' }), {
          status: 404,
          headers: { 'Content-Type': 'application/json' },
        });
      }

      // Check revoked flag on pack (offset 72 in Anchor pack layout)
      const packRevoked = packAccount.data.length > 72 && packAccount.data[72] === 1;
      if (packRevoked) {
        return new Response(JSON.stringify({ error: 'pack-revoked' }), {
          status: 410,
          headers: { 'Content-Type': 'application/json' },
        });
      }

      // Step E: Anti-Dusting Seat Account Validation
      if (!seatAccount || !seatAccount.owner.equals(programId)) {
        return new Response(JSON.stringify({ error: 'seat-not-found-or-dusted' }), {
          status: 402,
          headers: { 'Content-Type': 'application/json' },
        });
      }

      const data = seatAccount.data;
      if (data.length < 72) {
        return new Response(JSON.stringify({ error: 'invalid-seat-data' }), {
          status: 402,
          headers: { 'Content-Type': 'application/json' },
        });
      }

      // Verify discriminator
      for (let i = 0; i < 8; i++) {
        if (data[i] !== SEAT_DISCRIMINATOR[i]) {
          return new Response(JSON.stringify({ error: 'invalid-seat-discriminator' }), {
            status: 402,
            headers: { 'Content-Type': 'application/json' },
          });
        }
      }

      const memberInAccount = new PublicKey(data.subarray(8, 40));
      const packInAccount = new PublicKey(data.subarray(40, 72));
      if (!memberInAccount.equals(walletPubkey) || !packInAccount.equals(packPubkey)) {
        return new Response(JSON.stringify({ error: 'seat-member-mismatch' }), {
          status: 402,
          headers: { 'Content-Type': 'application/json' },
        });
      }

      // Step F: Issue R2 Presigned URL (TTL 60 seconds)
      const accountId = env.R2_ACCOUNT_ID || 'dummy-account';
      const bucketName = env.R2_BUCKET_NAME || 'plemu-content';
      const accessKeyId = env.R2_ACCESS_KEY_ID || 'dummy-key';
      const secretAccessKey = env.R2_SECRET_ACCESS_KEY || 'dummy-secret';

      const s3 = new S3Client({
        region: 'auto',
        endpoint: `https://${accountId}.r2.cloudflarestorage.com`,
        credentials: {
          accessKeyId,
          secretAccessKey,
        },
      });

      const getCommand = new GetObjectCommand({
        Bucket: bucketName,
        Key: `artifacts/gated/${body.sha256}`,
      });
      const downloadUrl = await getSignedUrl(s3, getCommand, { expiresIn: 60 });

      return new Response(JSON.stringify({ url: downloadUrl, expiresInSeconds: 60 }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    }

    return new Response(JSON.stringify({ error: 'not-found' }), {
      status: 404,
      headers: { 'Content-Type': 'application/json' },
    });
  },
};
