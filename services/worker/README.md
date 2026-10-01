# Plemu Seat Gate Worker

Cloudflare Edge Worker that controls access to gated ROM artifacts on Cloudflare R2 based on Solana on-chain `Seat` and `Pack` accounts.

## Architecture

```
Client Device ---> GET /challenge ---> Worker (Stores Nonce in KV, 300s TTL)
Client Device ---> POST /gate/:packId -> Worker:
                                         1. Validate Nonce (Anti-replay via KV)
                                         2. Verify Ed25519 signature
                                         3. Verify Solana Seat & Pack PDA (Anti-dusting)
                                         4. Generate R2 Presigned URL (TTL <= 60s)
Client Device <--- Presigned URL <------- Worker
Client Device ---> Direct R2 Stream ----> Cloudflare R2 (Zero Egress Cost)
```

## Security Invariants
- **Content Trust:** Worker gates access, never authenticity. Devices re-verify manifest Ed25519 signatures and SHA-256 hashes locally.
- **Fail-Closed:** Missing seat (402), revoked pack (410), invalid signature/nonce (401), RPC errors (503).
- **Anti-Dusting:** Validates 8-byte Anchor discriminator and deserialized member/pack pubkeys.
