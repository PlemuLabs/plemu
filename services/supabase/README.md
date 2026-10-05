# Plemu Supabase Backend & Moderation Service

This directory manages the Supabase schema, database migrations, and Row-Level Security (RLS) policies for Plemu's off-chain curation, Safe Harbor §512 DMCA compliance, and creator registry.

## Architecture & Security Invariants

1. **Content Authenticity is Independent:**
   The database mirrors state and queues submissions for human curation. Devices NEVER trust database state blindly; all game packages must have Ed25519-signed attestations and SHA-256 matching official manifests.
2. **Fail-Closed RLS:**
   - Public/Anon keys have ZERO read/write access to creator PII or DMCA tickets.
   - Creators can only inspect and insert their own game submissions (`creator_wallet = auth.jwt() ->> 'sub'`).
   - DMCA notices and counter-notices are strictly restricted to the `service_role` (curators / legal officers).
3. **Safe Harbor §512 Records:**
   - `creator_profiles` stores contact information required for copyright counter-notices.
   - `dmca_notices` and `dmca_counter_notices` maintain an immutable audit trail of takedown demands.

## Environment Variables

For local development or CI/CD services interacting with Supabase:

| Variable | Description | Location |
| :--- | :--- | :--- |
| `SUPABASE_URL` | API endpoint for the Supabase project | Project Settings > API |
| `SUPABASE_ANON_KEY` | Public client key (used by frontend apps) | Project Settings > API |
| `SUPABASE_SERVICE_ROLE_KEY` | Secret backend service role key (bypasses RLS) | Project Settings > API (Keep Secret!) |

## Applying Migrations

Migrations can be applied using the Supabase MCP or Supabase CLI:

```bash
# Using Supabase CLI
supabase db push
```
