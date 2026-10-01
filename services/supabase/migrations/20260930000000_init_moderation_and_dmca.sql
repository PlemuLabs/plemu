-- Plemu Moderation & DMCA Safe Harbor Schema (Task C8)
-- Strict fail-closed Row Level Security (RLS) for creator submissions, PII, and DMCA notices.

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 1. Creator Submissions Queue
CREATE TABLE IF NOT EXISTS submissions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
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
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
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
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    entry_id TEXT NOT NULL,
    claimant_name TEXT NOT NULL,
    claimant_email TEXT NOT NULL,
    infringing_url TEXT NOT NULL,
    copyrighted_work TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'received' CHECK (status IN ('received', 'processed', 'counter_noticed', 'withdrawn')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS dmca_counter_notices (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
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

-- Submissions: Creators can read/insert their own; Service role bypasses RLS
DROP POLICY IF EXISTS "Creators view their submissions" ON submissions;
CREATE POLICY "Creators view their submissions" ON submissions
    FOR SELECT TO authenticated
    USING (creator_wallet = (auth.jwt() ->> 'sub'));

DROP POLICY IF EXISTS "Creators insert their submissions" ON submissions;
CREATE POLICY "Creators insert their submissions" ON submissions
    FOR INSERT TO authenticated
    WITH CHECK (creator_wallet = (auth.jwt() ->> 'sub'));

-- Creator Profiles: Strictly restricted to the owner and service role
DROP POLICY IF EXISTS "Owners view own profile" ON creator_profiles;
CREATE POLICY "Owners view own profile" ON creator_profiles
    FOR SELECT TO authenticated
    USING (wallet = (auth.jwt() ->> 'sub'));

DROP POLICY IF EXISTS "Owners update own profile" ON creator_profiles;
CREATE POLICY "Owners update own profile" ON creator_profiles
    FOR ALL TO authenticated
    USING (wallet = (auth.jwt() ->> 'sub'));

-- DMCA Notices & Counter-Notices:
-- No public / anon access. Only authenticated service role can access notices and counter-notices.
