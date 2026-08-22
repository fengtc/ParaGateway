-- Add a user-global tokens-per-minute gateway limit.
-- 0 means unlimited; usage is aggregated across all API keys, models, and groups.
ALTER TABLE users
    ADD COLUMN IF NOT EXISTS tpm_limit INTEGER NOT NULL DEFAULT 0;
