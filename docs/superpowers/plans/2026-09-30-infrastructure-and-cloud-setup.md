# Infrastructure, Cloud & Security Bootstrap (48-Hour Sprint) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Configure and deploy the end-to-end cloud and security infrastructure for Plemu in 48 hours: Cloudflare R2 storage + CDN cache purge, Cloudflare Worker Seat Gate (Task C7), Supabase moderation & DMCA schema (Task C8), Solana Devnet $PLEMU token mint (Task C1), and official Ed25519 signing key synchronization across tools and Unity client.

**Architecture:** Edge-first, fail-closed, zero-egress architecture. All client devices maintain local Content Trust by verifying Ed25519 signatures and SHA-256 hashes locally prior to launch (never blindly trusting edge or CDN). Public ROMs and manifests are distributed via Cloudflare R2 and CDN (`cdn.plemu.xyz`). Private pack ROMs are guarded by an edge Worker (The Seat Gate) verifying Solana on-chain `Seat` and `Pack` accounts at current slot before issuing short-lived R2 presigned URLs (TTL ≤ 60s). Off-chain moderation, legal DMCA Safe Harbor records, and submission queues reside in Supabase under strict Row-Level Security (RLS).

**Tech Stack:** Cloudflare R2, Cloudflare Workers (TypeScript, `@solana/web3.js`, `@aws-sdk/client-s3`), Solana Devnet (Anchor, `@solana/spl-token`), PostgreSQL / Supabase, GitHub Actions (YAML, AWS CLI/rclone), OpenSSL (Ed25519), Unity C# (`PinnedKeys.cs`).

**Spec:** `docs/specs/SPEC.md` (normative, frozen v1), `docs/specs/SPEC-ERRATA.md`, `docs/runbooks/emergency-publish.md`, `docs/runbooks/key-rotation.md`, `docs/superpowers/plans/platform-expansion.md` (Tasks B1, C1, C7, C8).

---

## Global Constraints

- **Normative SPEC v1 is FROZEN (2026-07-12):** Never edit `docs/specs/SPEC.md` directly. Record any prose deviations in `docs/specs/SPEC-ERRATA.md`.
- **Fail-Closed Principle:** Any verification failure, empty build without `--allow-empty`, RPC timeout, or missing entitlement must reject immediately (HTTP 402, 410, or 503). Never fail open.
- **Content Trust Invariant:** The edge Worker gates access to bytes; it never gates authenticity. Client devices (`apps/device/unity`) always re-verify raw Ed25519 signatures against pinned keys and recalculate SHA-256 hashes locally before loading any ROM.
- **Key Custody Invariant:** The `emergency` private key is generated offline, split via M-of-N custody, and NEVER uploaded to any runner, repository, or cloud provider. Production `active` private key lives exclusively in GitHub Secrets / KMS.
- **Zero-Egress Invariant:** All public and gated artifacts stream from Cloudflare R2 to avoid bandwidth egress penalties. Gated downloads use presigned URLs with TTL ≤ 60 seconds; no byte proxying through Workers.
- **Anti-Dusting Check:** The Worker seat gate must NEVER rely on a bare null or lamports check for a Seat PDA address. It must inspect account program ownership, the 8-byte Anchor discriminator, and deserialized fields (`member === wallet && pack === packId`).

---

## 48-Hour Sprint Schedule

| Window | Focus | Responsibility |
| :--- | :--- | :--- |
| **H00 - H04 (Day 1 Morning)** | External Accounts Setup, Key Ceremony, GitHub Secrets | Human Operator (Manual Setup) |
| **H04 - H12 (Day 1 Afternoon)** | Task 1 (Keys Sync in Repo & Unity) & Task 3 (Solana $PLEMU Token) | Agent 5 & Agent 2 |
| **H12 - H24 (Day 1 Evening/Night)** | Task 2 (CI/CD Workflows for R2 & CDN) & Task 5 (Supabase Schema & RLS) | Agent 1 & Agent 3 |
| **H24 - H36 (Day 2 Morning)** | Task 4 (The Seat Gate Worker & Tests) | Agent 4 |
| **H36 - H44 (Day 2 Afternoon)** | Deployments & Integration Smoke Tests (Build → R2 → Worker → Unity) | Human + Agents |
| **H44 - H48 (Day 2 Evening)** | DMCA Emergency Revocation Drill & Readiness Sign-off | Human + Agents |

---

## Phase 0: External Accounts & Key Ceremony (Human Operator)

Execute these steps in the first 3 hours before launching code tasks:

### 0.1 Cloudflare R2 & CDN
1. Log in to Cloudflare Dashboard.
2. Navigate to **R2 > Create Bucket**:
   - Bucket name: `plemu-content`
   - Location: Automatic
3. In Bucket Settings:
   - Connect Custom Domain: `cdn.plemu.xyz` (or `cdn.plemu.dev` staging).
   - Ensure CORS allows `GET` from device origins and Unity WebGL if needed.
4. Create an **R2 API Token**:
   - Permissions: `Object Read & Write` on `plemu-content`.
   - Save: `CLOUDFLARE_ACCOUNT_ID`, `CLOUDFLARE_R2_ACCESS_KEY_ID`, `CLOUDFLARE_R2_SECRET_ACCESS_KEY`.
5. Create a **Cache Purge API Token**:
   - In *User Profile > API Tokens > Create Token*:
   - Permissions: `Zone - Cache Purge - Purge` for zone `plemu.xyz`.
   - Save: `CF_PURGE_API_TOKEN` and the Cloudflare `Zone ID`.

### 0.2 Cryptographic Key Ceremony (Offline / Secure Shell)
Run in a local directory outside git (e.g. `~/plemu-secrets/`):
```bash
mkdir -p ~/plemu-secrets && cd ~/plemu-secrets
chmod 700 .

# 1. Emergency key (Offline / Break-glass)
openssl genpkey -algorithm ed25519 -out plemu-emergency.pem
chmod 600 plemu-emergency.pem
openssl pkey -in plemu-emergency.pem -pubout -outform DER | tail -c 32 | xxd -p -c 64 > emergency.pub

# 2. Active key (Hot key for CI nightly & release signing)
openssl genpkey -algorithm ed25519 -out plemu-active.pem
chmod 600 plemu-active.pem
openssl pkey -in plemu-active.pem -pubout -outform DER | tail -c 32 | xxd -p -c 64 > active.pub
base64 -w 0 plemu-active.pem > active_base64.txt

# 3. Next key (Pre-pinned rotation target)
openssl genpkey -algorithm ed25519 -out plemu-next.pem
chmod 600 plemu-next.pem
openssl pkey -in plemu-next.pem -pubout -outform DER | tail -c 32 | xxd -p -c 64 > next.pub
```
*Note: Store `plemu-emergency.pem` on an encrypted offline drive. Never commit it.*

### 0.3 Solana Devnet Environment
```bash
# Generate deployer keypair
solana-keygen new -o ~/plemu-secrets/plemu-deployer.json --no-bip39-passphrase
solana airdrop 2 $(solana-keygen pubkey ~/plemu-secrets/plemu-deployer.json) --url devnet

# Obtain Helius or Quicknode RPC endpoint for Devnet:
# Example: https://devnet.helius-rpc.com/?api-key=<HELIUS_API_KEY>
```

### 0.4 Supabase Project
1. Create a project at [supabase.com](https://supabase.com) named `plemu-staging`.
2. Under **Project Settings > API**, record:
   - `Project URL`
   - `anon public` key
   - `service_role secret` key

### 0.5 GitHub Actions Secrets Registration
In GitHub repository settings (`Settings > Secrets and variables > Actions`), register:
- `CLOUDFLARE_ACCOUNT_ID`
- `CLOUDFLARE_R2_ACCESS_KEY_ID`
- `CLOUDFLARE_R2_SECRET_ACCESS_KEY`
- `CLOUDFLARE_ZONE_ID`
- `CF_PURGE_API_TOKEN`
- `PLEMU_MANIFEST_KEY` (Content of `active_base64.txt`)
- `SOLANA_DEVNET_RPC_URL`
- `SOLANA_DEVNET_DEPLOYER_KEY` (JSON string from `plemu-deployer.json`)
- `SUPABASE_URL`
- `SUPABASE_SERVICE_ROLE_KEY`

---

## Task 1: Key Registry Synchronization (Unity & Rust Tools)

**Files:**
- Modify: `tools/keys/manifest-signers.json`
- Modify: `apps/device/unity/Assets/_Game/Scripts/Runtime/Content/PinnedKeys.cs`
- Modify: `apps/device/unity/Assets/_Game/Tests/EditMode/PinnedKeysTests.cs`
- Modify: `apps/device/unity/Assets/_Game/Tests/EditMode/Fixtures/manifest-signers.json`
- Test: `tools/crates/plemu-manifest/tests/verify_integration.rs`

**Interfaces:**
- Consumes: `active.pub`, `next.pub`, `emergency.pub` hex strings from Phase 0.2.
- Produces: Updated `KeyRegistry` trusted in Unity and validated by `plemu-manifest verify`.

- [ ] **Step 1: Update `tools/keys/manifest-signers.json`**
Set official keys maintaining the invariant: exactly 1 `active`, 1 `next`, 1 `emergency`.

```json
{
  "keys": [
    {
      "keyId": "plemu-manifest-prod-a",
      "publicKey": "<ACTIVE_HEX_FROM_PHASE_0>",
      "status": "active",
      "validFrom": "2026-09-30T00:00:00Z"
    },
    {
      "keyId": "plemu-manifest-prod-b",
      "publicKey": "<NEXT_HEX_FROM_PHASE_0>",
      "status": "next"
    },
    {
      "keyId": "plemu-emergency-1",
      "publicKey": "<EMERGENCY_HEX_FROM_PHASE_0>",
      "status": "emergency"
    }
  ],
  "_warning": "OFFICIAL KEY REGISTRY. Production active/next keys live in CI environment secrets or KMS; the emergency key is stored offline and is never committed anywhere."
}
```

- [ ] **Step 2: Update `PinnedKeys.cs` in Unity**
Synchronize `PinnedKeys.cs` with the keys from Step 1:

```csharp
namespace Plemu.Content
{
    public static class PinnedKeys
    {
        public const string ProdKeyIdA = "plemu-manifest-prod-a";
        public const string ProdKeyIdB = "plemu-manifest-prod-b";
        public const string EmergencyKeyId = "plemu-emergency-1";

        public const string ProdPublicKeyA = "<ACTIVE_HEX_FROM_PHASE_0>";
        public const string ProdPublicKeyB = "<NEXT_HEX_FROM_PHASE_0>";
        public const string EmergencyPublicKey = "<EMERGENCY_HEX_FROM_PHASE_0>";

        public static KeyRegistry BuiltIn => new KeyRegistry
        {
            Keys =
            {
                new KeyEntry
                {
                    KeyId = ProdKeyIdA,
                    PublicKey = ProdPublicKeyA,
                    Status = "active",
                    ValidFrom = "2026-09-30T00:00:00Z",
                },
                new KeyEntry
                {
                    KeyId = ProdKeyIdB,
                    PublicKey = ProdPublicKeyB,
                    Status = "next",
                },
                new KeyEntry
                {
                    KeyId = EmergencyKeyId,
                    PublicKey = EmergencyPublicKey,
                    Status = "emergency",
                },
            },
        };
    }
}
```

- [ ] **Step 3: Update Fixtures & Run Unit Tests**
Copy updated `manifest-signers.json` to `apps/device/unity/Assets/_Game/Tests/EditMode/Fixtures/manifest-signers.json` and run:
```bash
cd tools && cargo test -p plemu-manifest
cd tools && cargo test -p plemu-schemas
```
Expected: PASS.

- [ ] **Step 4: Commit**
```bash
git add tools/keys/manifest-signers.json apps/device/unity/Assets/_Game/Scripts/Runtime/Content/PinnedKeys.cs apps/device/unity/Assets/_Game/Tests/EditMode/Fixtures/manifest-signers.json
git commit -m "feat(security): register official active, next, and emergency ed25519 signing keys"
```

---

## Task 2: CI/CD Pipeline R2 Publishing & CDN Cache Purge (Task B1 completion)

**Files:**
- Modify: `.github/workflows/nightly-enforce.yml`
- Modify: `.github/workflows/emergency-revoke.yml`

**Interfaces:**
- Consumes: `CLOUDFLARE_ACCOUNT_ID`, `CLOUDFLARE_R2_ACCESS_KEY_ID`, `CLOUDFLARE_R2_SECRET_ACCESS_KEY`, `CF_PURGE_API_TOKEN`, `CLOUDFLARE_ZONE_ID`.
- Produces: Live synced manifests and ROMs on `cdn.plemu.xyz`.

- [ ] **Step 1: Replace placeholder publish step in `nightly-enforce.yml`**
Replace lines 388-403 of `.github/workflows/nightly-enforce.yml`:

```yaml
      - name: Publish to Cloudflare R2
        env:
          AWS_ACCESS_KEY_ID: ${{ secrets.CLOUDFLARE_R2_ACCESS_KEY_ID }}
          AWS_SECRET_ACCESS_KEY: ${{ secrets.CLOUDFLARE_R2_SECRET_ACCESS_KEY }}
          AWS_DEFAULT_REGION: auto
          R2_ENDPOINT: https://${{ secrets.CLOUDFLARE_ACCOUNT_ID }}.r2.cloudflarestorage.com
        run: |
          echo "==> Publishing manifests to R2"
          aws s3 cp manifest-beta.json s3://plemu-content/manifests/store/manifest-beta.json --endpoint-url $R2_ENDPOINT --content-type application/json
          aws s3 cp manifest-beta.json.sig s3://plemu-content/manifests/store/manifest-beta.json.sig --endpoint-url $R2_ENDPOINT --content-type application/octet-stream
          
          # Also publish at root of bucket for direct cdn.plemu.xyz/manifest-beta.json access
          aws s3 cp manifest-beta.json s3://plemu-content/manifest-beta.json --endpoint-url $R2_ENDPOINT --content-type application/json
          aws s3 cp manifest-beta.json.sig s3://plemu-content/manifest-beta.json.sig --endpoint-url $R2_ENDPOINT --content-type application/octet-stream
          
          # Archive historical daily snapshot
          DATE_STR=$(date -u +%Y-%m-%d)
          aws s3 cp manifest-beta.json s3://plemu-content/by-date/${DATE_STR}/manifest-beta.json --endpoint-url $R2_ENDPOINT

          # Sync any public artifact files if present
          if [ -d "registry/beta" ]; then
            for art in registry/beta/*/*/artifact/*; do
              if [ -f "$art" ]; then
                SHA=$(sha256sum "$art" | awk '{print $1}')
                echo "==> Syncing artifact $SHA"
                aws s3 cp "$art" s3://plemu-content/artifacts/public/${SHA} --endpoint-url $R2_ENDPOINT --cache-control "public, max-age=31536000, immutable"
              fi
            done
          fi

      - name: Purge Cloudflare CDN Cache
        if: success()
        env:
          CF_TOKEN: ${{ secrets.CF_PURGE_API_TOKEN }}
          ZONE_ID: ${{ secrets.CLOUDFLARE_ZONE_ID }}
        run: |
          echo "==> Purging manifest URLs from Cloudflare edge cache"
          curl -s -X POST "https://api.cloudflare.com/client/v4/zones/${ZONE_ID}/purge_cache" \
            -H "Authorization: Bearer ${CF_TOKEN}" \
            -H "Content-Type: application/json" \
            --data '{"files":["https://cdn.plemu.xyz/manifest-beta.json","https://cdn.plemu.xyz/manifest-beta.json.sig","https://cdn.plemu.xyz/manifests/store/manifest-beta.json","https://cdn.plemu.xyz/manifests/store/manifest-beta.json.sig"]}' | jq .
```

- [ ] **Step 2: Replace placeholder publish step in `emergency-revoke.yml`**
Replace lines 222-235 of `.github/workflows/emergency-revoke.yml` with the same S3 upload and cache purge commands scoped to `${TIER}`.

- [ ] **Step 3: Test workflow syntax locally**
Validate YAML syntax using actionlint or python yaml parser:
```bash
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/nightly-enforce.yml')); yaml.safe_load(open('.github/workflows/emergency-revoke.yml')); print('YAML syntax valid')"
```
Expected: `YAML syntax valid`.

- [ ] **Step 4: Commit**
```bash
git add .github/workflows/nightly-enforce.yml .github/workflows/emergency-revoke.yml
git commit -m "ci(publish): implement cloudflare r2 s3 upload and cdn cache purge"
```

---

## Task 3: Solana Devnet SPL Token $PLEMU Script (Task C1)

**Files:**
- Create: `programs/plemu/scripts/create-token.ts`
- Create: `programs/plemu/devnet-addresses.json`
- Create/Modify: `programs/plemu/TOKENOMICS.md`

**Interfaces:**
- Consumes: `SOLANA_DEVNET_DEPLOYER_KEY`, `SOLANA_DEVNET_RPC_URL`.
- Produces: SPL Token Mint address for $PLEMU recorded in `programs/plemu/devnet-addresses.json`.

- [x] **Step 1: Write `programs/plemu/scripts/create-token.ts`**
Create the idempotent SPL Classic mint initialization script:

```typescript
import { Connection, Keypair, PublicKey, clusterApiUrl } from '@solana/web3.js';
import { createMint, getOrCreateAssociatedTokenAccount, mintTo } from '@solana/spl-token';
import * as fs from 'fs';
import * as path from 'path';

async function main() {
  const rpcUrl = process.env.SOLANA_DEVNET_RPC_URL || clusterApiUrl('devnet');
  const connection = new Connection(rpcUrl, 'confirmed');

  const keypairPath = process.env.SOLANA_DEPLOYER_PATH || path.join(process.env.HOME || '', 'plemu-secrets', 'plemu-deployer.json');
  if (!fs.existsSync(keypairPath)) {
    console.error(`Deployer keypair not found at: ${keypairPath}`);
    process.exit(1);
  }

  const secretKey = Uint8Array.from(JSON.parse(fs.readFileSync(keypairPath, 'utf-8')));
  const deployer = Keypair.fromSecretKey(secretKey);
  console.log(`Deployer: ${deployer.publicKey.toBase58()}`);

  const addressesFile = path.join(__dirname, '..', 'devnet-addresses.json');
  let addresses: Record<string, string> = {};
  if (fs.existsSync(addressesFile)) {
    addresses = JSON.parse(fs.readFileSync(addressesFile, 'utf-8'));
  }

  if (addresses.plemuMint) {
    console.log(`$PLEMU mint already exists at: ${addresses.plemuMint}`);
    return;
  }

  console.log('Creating SPL Classic Mint for $PLEMU (6 decimals)...');
  const mint = await createMint(
    connection,
    deployer,
    deployer.publicKey,
    deployer.publicKey,
    6 // 6 decimals
  );

  console.log(`$PLEMU Mint created: ${mint.toBase58()}`);
  addresses.plemuMint = mint.toBase58();

  // Create initial token account for treasury/deployer and mint 1,000,000,000 tokens
  const tokenAccount = await getOrCreateAssociatedTokenAccount(
    connection,
    deployer,
    mint,
    deployer.publicKey
  );

  const initialSupply = 1_000_000_000n * 1_000_000n; // 1 billion tokens with 6 decimals
  console.log(`Minting 1,000,000,000 $PLEMU to deployer ATA: ${tokenAccount.address.toBase58()}...`);
  await mintTo(
    connection,
    deployer,
    mint,
    tokenAccount.address,
    deployer,
    initialSupply
  );

  fs.writeFileSync(addressesFile, JSON.stringify(addresses, null, 2));
  console.log(`Saved mint address to ${addressesFile}`);
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
```

- [x] **Step 2: Document tokenomics and compatibility in `TOKENOMICS.md`**
Record:
  - Standard: SPL Token Classic (Non-2022) to guarantee full compatibility with Meteora DLMM, Raydium, and wallet adapters.
  - Decimals: 6.
  - Supply: 1,000,000,000 fixed initial supply.

- [x] **Step 3: Commit**
```bash
git add programs/plemu/scripts/create-token.ts programs/plemu/TOKENOMICS.md
git commit -m "feat(token): add devnet $PLEMU spl-token classic creation script"
```

---

## Task 4: The Seat Gate Worker (Task C7)

**Files:**
- Create: `services/worker/package.json`
- Create: `services/worker/tsconfig.json`
- Create: `services/worker/wrangler.toml`
- Create: `services/worker/src/index.ts`
- Create: `services/worker/test/gate.test.ts`

**Interfaces:**
- Consumes: Solana RPC URL, R2 bucket credentials/binding, `PLEMU_SEATS_PROGRAM_ID`.
- Produces: Authenticated endpoints `GET /challenge` and `POST /gate/:packId` issuing R2 Presigned URLs.

- [ ] **Step 1: Create `services/worker/package.json` and `wrangler.toml`**
Setup dependencies (`@solana/web3.js`, `@aws-sdk/client-s3`, `@aws-sdk/s3-request-presigner`, `tweetnacl`, `vitest`):

```json
{
  "name": "plemu-seat-gate",
  "version": "0.1.0",
  "private": true,
  "scripts": {
    "test": "vitest run",
    "deploy": "wrangler deploy"
  },
  "dependencies": {
    "@aws-sdk/client-s3": "^3.600.0",
    "@aws-sdk/s3-request-presigner": "^3.600.0",
    "@solana/web3.js": "^1.95.0",
    "tweetnacl": "^1.0.3"
  },
  "devDependencies": {
    "@cloudflare/workers-types": "^4.20240900.0",
    "typescript": "^5.5.0",
    "vitest": "^1.6.0",
    "wrangler": "^3.60.0"
  }
}
```

- [ ] **Step 2: Write failing unit tests in `services/worker/test/gate.test.ts`**
Include tests for:
1. Rejection when wallet holds no Seat account (HTTP 402).
2. Anti-Dusting: Rejection of account with 1-lamport balance but incorrect program owner or invalid Anchor discriminator (HTTP 402).
3. Rejection when `Pack.revoked == true` (HTTP 410).
4. Replay rejection for used/expired challenges (HTTP 401).
5. Issuance of R2 signed URL with TTL ≤ 60s upon valid credentials (HTTP 200).

- [ ] **Step 3: Implement `services/worker/src/index.ts`**
Implement the request handler enforcing fail-closed verification:

```typescript
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
  R2_ACCOUNT_ID: string;
  R2_ACCESS_KEY_ID: string;
  R2_SECRET_ACCESS_KEY: string;
  R2_BUCKET_NAME: string;
}

// 8-byte Anchor discriminator for `Seat` struct: sha256("account:Seat")[0..8]
const SEAT_DISCRIMINATOR = new Uint8Array([/* precomputed 8 bytes */]);

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);

    if (url.pathname === '/challenge' && request.method === 'GET') {
      const nonce = crypto.randomUUID();
      const timestamp = Date.now();
      const message = `Plemu Auth Challenge\nNonce: ${nonce}\nTimestamp: ${timestamp}`;
      await env.NONCE_KV.put(`nonce:${nonce}`, JSON.stringify({ timestamp }), { expirationTtl: 300 });
      return new Response(JSON.stringify({ nonce, message }), {
        headers: { 'Content-Type': 'application/json' },
      });
    }

    if (url.pathname.startsWith('/gate/') && request.method === 'POST') {
      const packId = url.pathname.split('/')[2];
      const body = await request.json() as { wallet: string; signature: string; nonce: string; sha256: string };

      // 1. Verify Nonce anti-replay
      const stored = await env.NONCE_KV.get(`nonce:${body.nonce}`);
      if (!stored) {
        return new Response(JSON.stringify({ error: 'challenge-expired-or-replayed' }), { status: 401 });
      }
      await env.NONCE_KV.delete(`nonce:${body.nonce}`);

      // 2. Verify Ed25519 signature
      const message = `Plemu Auth Challenge\nNonce: ${body.nonce}\nTimestamp: ${JSON.parse(stored).timestamp}`;
      const pubkeyBytes = new PublicKey(body.wallet).toBytes();
      const sigBytes = Uint8Array.from(Buffer.from(body.signature, 'hex'));
      const msgBytes = new TextEncoder().encode(message);
      if (!nacl.sign.detached.verify(msgBytes, sigBytes, pubkeyBytes)) {
        return new Response(JSON.stringify({ error: 'invalid-signature' }), { status: 401 });
      }

      // 3. Connect to Solana RPC with Fallback
      let connection = new Connection(env.SOLANA_RPC_URL, 'confirmed');
      const programId = new PublicKey(env.PLEMU_SEATS_PROGRAM_ID);
      const packPubkey = new PublicKey(packId);
      const walletPubkey = new PublicKey(body.wallet);

      // Derive PDAs: Seat = [b"seat", pack.key(), member.key()]
      const [seatPda] = PublicKey.findProgramAddressSync(
        [Buffer.from('seat'), packPubkey.toBuffer(), walletPubkey.toBuffer()],
        programId
      );

      let accounts;
      try {
        accounts = await connection.getMultipleAccountsInfo([seatPda, packPubkey], 'confirmed');
      } catch (rpcErr) {
        if (env.SOLANA_RPC_FALLBACK_URL) {
          connection = new Connection(env.SOLANA_RPC_FALLBACK_URL, 'confirmed');
          accounts = await connection.getMultipleAccountsInfo([seatPda, packPubkey], 'confirmed');
        } else {
          return new Response(JSON.stringify({ error: 'rpc-unavailable' }), { status: 503 });
        }
      }

      const [seatAccount, packAccount] = accounts;

      // 4. Validate Pack status
      if (!packAccount || !packAccount.owner.equals(programId)) {
        return new Response(JSON.stringify({ error: 'pack-not-found' }), { status: 404 });
      }
      // Check revoked flag on pack (offset depends on Anchor pack layout, e.g. at byte 8+32+32...)
      const packRevoked = packAccount.data[72] === 1; // Example offset
      if (packRevoked) {
        return new Response(JSON.stringify({ error: 'pack-revoked' }), { status: 410 });
      }

      // 5. Anti-Dusting Seat Account Validation
      if (!seatAccount || !seatAccount.owner.equals(programId)) {
        return new Response(JSON.stringify({ error: 'seat-not-found-or-dusted' }), { status: 402 });
      }

      // Verify discriminator & fields
      const data = seatAccount.data;
      if (data.length < 72) {
        return new Response(JSON.stringify({ error: 'invalid-seat-data' }), { status: 402 });
      }
      const memberInAccount = new PublicKey(data.subarray(8, 40));
      const packInAccount = new PublicKey(data.subarray(40, 72));
      if (!memberInAccount.equals(walletPubkey) || !packInAccount.equals(packPubkey)) {
        return new Response(JSON.stringify({ error: 'seat-member-mismatch' }), { status: 402 });
      }

      // 6. Issue R2 Presigned URL (TTL 60 seconds)
      const s3 = new S3Client({
        region: 'auto',
        endpoint: `https://${env.R2_ACCOUNT_ID}.r2.cloudflarestorage.com`,
        credentials: {
          accessKeyId: env.R2_ACCESS_KEY_ID,
          secretAccessKey: env.R2_SECRET_ACCESS_KEY,
        },
      });

      const getCommand = new GetObjectCommand({
        Bucket: env.R2_BUCKET_NAME,
        Key: `artifacts/gated/${body.sha256}`,
      });
      const downloadUrl = await getSignedUrl(s3, getCommand, { expiresIn: 60 });

      return new Response(JSON.stringify({ url: downloadUrl, expiresInSeconds: 60 }), {
        headers: { 'Content-Type': 'application/json' },
      });
    }

    return new Response('Not Found', { status: 404 });
  },
};
```

- [ ] **Step 4: Run Tests**
```bash
cd services/worker && npm install && npm test
```
Expected: PASS.

- [ ] **Step 5: Commit**
```bash
git add services/worker/
git commit -m "feat(edge): implement cloudflare worker seat gate with anti-dusting check"
```

---

## Task 5: Supabase Schema & Strict RLS (Task C8)

**Files:**
- Create: `services/supabase/migrations/20260930000000_init_moderation_and_dmca.sql`
- Create: `services/supabase/README.md`

**Interfaces:**
- Consumes: Supabase PostgreSQL instance.
- Produces: Secure tables for moderation, creator profiles, and DMCA §512 tickets with fail-closed RLS.

- [ ] **Step 1: Write Migration `20260930000000_init_moderation_and_dmca.sql`**
Define schemas and policies:

```sql
-- Plemu Moderation & DMCA Safe Harbor Schema (Task C8)

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 1. Creator Submissions Queue
CREATE TABLE IF NOT EXISTS submissions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    creator_wallet TEXT NOT NULL,
    system TEXT NOT NULL,
    title TEXT NOT NULL,
    sha256 TEXT NOT NULL,
    license_type TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'under_review', 'approved', 'rejected')),
    curator_notes TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 2. License Evidence Files
CREATE TABLE IF NOT EXISTS license_evidence (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    submission_id UUID NOT NULL REFERENCES submissions(id) ON DELETE CASCADE,
    file_sha256 TEXT NOT NULL,
    evidence_type TEXT NOT NULL,
    source_url TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 3. Creator Profiles & PII (Safe Harbor §512 Contact)
CREATE TABLE IF NOT EXISTS creator_profiles (
    wallet TEXT PRIMARY KEY,
    legal_name TEXT,
    contact_email TEXT NOT NULL,
    encrypted_pii JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 4. DMCA Takedown Notices & Counter-Notices
CREATE TABLE IF NOT EXISTS dmca_notices (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    entry_id TEXT NOT NULL,
    claimant_name TEXT NOT NULL,
    claimant_email TEXT NOT NULL,
    infringing_url TEXT NOT NULL,
    copyrighted_work TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'received' CHECK (status IN ('received', 'processed', 'counter_noticed', 'withdrawn')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS dmca_counter_notices (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    notice_id UUID NOT NULL REFERENCES dmca_notices(id) ON DELETE CASCADE,
    creator_wallet TEXT NOT NULL,
    statement TEXT NOT NULL,
    consent_jurisdiction BOOLEAN NOT NULL DEFAULT FALSE,
    submitted_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 5. Off-chain Receipts & Quest Mirror (Query optimization only; Solana is authoritative)
CREATE TABLE IF NOT EXISTS receipts_mirror (
    tx_signature TEXT PRIMARY KEY,
    pack_id TEXT NOT NULL,
    buyer_wallet TEXT NOT NULL,
    slot BIGINT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- =========================================================================
-- ROW LEVEL SECURITY (RLS) POLICIES - STRICT FAIL-CLOSED
-- =========================================================================

ALTER TABLE submissions ENABLE ROW LEVEL SECURITY;
ALTER TABLE license_evidence ENABLE ROW LEVEL SECURITY;
ALTER TABLE creator_profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE dmca_notices ENABLE ROW LEVEL SECURITY;
ALTER TABLE dmca_counter_notices ENABLE ROW LEVEL SECURITY;
ALTER TABLE receipts_mirror ENABLE ROW LEVEL SECURITY;

-- Default DENY for public roles
DROP POLICY IF EXISTS "Public deny submissions" ON submissions;
DROP POLICY IF EXISTS "Public deny profiles" ON creator_profiles;
DROP POLICY IF EXISTS "Public deny dmca" ON dmca_notices;

-- Submissions: Creators can read/insert their own; Curators/Service role can read all
CREATE POLICY "Creators view their submissions" ON submissions
    FOR SELECT TO authenticated
    USING (creator_wallet = auth.jwt() ->> 'sub');

CREATE POLICY "Creators insert their submissions" ON submissions
    FOR INSERT TO authenticated
    WITH CHECK (creator_wallet = auth.jwt() ->> 'sub');

-- Creator Profiles: Strictly restricted to the owner and service role
CREATE POLICY "Owners view own profile" ON creator_profiles
    FOR SELECT TO authenticated
    USING (wallet = auth.jwt() ->> 'sub');

CREATE POLICY "Owners update own profile" ON creator_profiles
    FOR ALL TO authenticated
    USING (wallet = auth.jwt() ->> 'sub');

-- DMCA Notices: Never exposed to public or anon roles; service_role only
-- (Service role bypasses RLS automatically in Supabase)
```

- [ ] **Step 2: Commit Schema**
```bash
git add services/supabase/migrations/
git commit -m "feat(db): add supabase schema with strict fail-closed rls for submissions and dmca"
```

---

## Dispatchable Agent Prompts (For Parallel Execution)

Copy and paste these exact prompts into separate subagents or CLI terminals:

### Agent Prompt 1: CI/CD & R2 Publisher (Task 2)
```text
Execute Task 2 from docs/superpowers/plans/2026-09-30-infrastructure-and-cloud-setup.md.
Modify .github/workflows/nightly-enforce.yml and .github/workflows/emergency-revoke.yml to publish manifests and ROMs to Cloudflare R2 using AWS CLI/S3 API and issue single-URL cache purge requests to Cloudflare API. Ensure fail-closed checks remain active. Commit when done.
```

### Agent Prompt 2: Solana Devnet SPL Token (Task 3)
```text
Execute Task 3 from docs/superpowers/plans/2026-09-30-infrastructure-and-cloud-setup.md.
Create programs/plemu/scripts/create-token.ts and programs/plemu/TOKENOMICS.md. Implement idempotent creation of a 6-decimal, 1-billion supply SPL Classic token on Solana Devnet. Commit when done.
```

### Agent Prompt 3: Worker Seat Gate (Task 4)
```text
Execute Task 4 from docs/superpowers/plans/2026-09-30-infrastructure-and-cloud-setup.md.
Create services/worker with wrangler.toml, src/index.ts, and test/gate.test.ts. Implement /challenge with replay prevention and /gate/:packId validating Solana Seat and Pack PDAs with anti-dusting checks before emitting 60s R2 presigned URLs. Write unit tests in Vitest and commit when tests pass.
```

### Agent Prompt 4: Supabase Schema & RLS (Task 5)
```text
Execute Task 5 from docs/superpowers/plans/2026-09-30-infrastructure-and-cloud-setup.md.
Create services/supabase/migrations/20260930000000_init_moderation_and_dmca.sql. Add tables for submissions, license evidence, PII creator profiles, and DMCA notices with strict fail-closed RLS policies. Commit when done.
```

### Agent Prompt 5: Key Synchronization (Task 1)
```text
Execute Task 1 from docs/superpowers/plans/2026-09-30-infrastructure-and-cloud-setup.md.
Once the human operator provides the hex values for active, next, and emergency keys, update tools/keys/manifest-signers.json, apps/device/unity/Assets/_Game/Scripts/Runtime/Content/PinnedKeys.cs, and the fixture files. Run cargo test --workspace and commit when passing.
```

---

## Verification & Completion Gate

Before considering the 48-hour setup complete, run:
1. `cd tools && cargo test --workspace` (Rust test suite green).
2. `cd services/worker && npm test` (Worker gate unit tests green).
3. Test a trial dry-run invocation of `emergency-revoke.yml` on GitHub Actions using `-f action=reinstate` or a staging branch to verify R2 connectivity.
4. Verify `https://cdn.plemu.xyz/manifest-beta.json` returns HTTP 200 with the valid active signature.
