-- Normalize ParaGateway's legacy Copilot groups and the remaining account
-- shapes without changing group IDs, account_groups bindings/priorities, or
-- api_keys.group_id references. The migration runner executes this file in a
-- transaction, so every preflight must pass before any backup or update occurs.

-- Reject legacy accounts that cannot be converted without guessing a token or
-- silently choosing between two different long-lived GitHub credentials.
DO $$
DECLARE
    invalid_account_ids BIGINT[];
BEGIN
    SELECT ARRAY_AGG(id ORDER BY id)
    INTO invalid_account_ids
    FROM accounts
    WHERE platform = 'copilot'
      AND (
          type IS DISTINCT FROM 'apikey'
          OR NOT (
              (
                  jsonb_typeof(credentials -> 'github_token') = 'string'
                  AND NULLIF(BTRIM(credentials ->> 'github_token'), '') IS NOT NULL
              )
              OR (
                  jsonb_typeof(credentials -> 'github_access_token') = 'string'
                  AND NULLIF(BTRIM(credentials ->> 'github_access_token'), '') IS NOT NULL
              )
          )
          OR (
              jsonb_typeof(credentials -> 'github_token') = 'string'
              AND NULLIF(BTRIM(credentials ->> 'github_token'), '') IS NOT NULL
              AND jsonb_typeof(credentials -> 'github_access_token') = 'string'
              AND NULLIF(BTRIM(credentials ->> 'github_access_token'), '') IS NOT NULL
              AND BTRIM(credentials ->> 'github_token')
                  IS DISTINCT FROM BTRIM(credentials ->> 'github_access_token')
          )
      );

    IF invalid_account_ids IS NOT NULL THEN
        RAISE EXCEPTION USING
            ERRCODE = 'check_violation',
            MESSAGE = FORMAT(
                'migration 235 blocked: invalid legacy Copilot account IDs=%s (require type=apikey and one non-conflicting GitHub token)',
                invalid_account_ids
            );
    END IF;
END
$$;

-- A legacy Copilot group must not contain an ordinary OpenAI/Anthropic account.
-- Converting such a group to openai would otherwise make that unrelated account
-- eligible for traffic that previously targeted Copilot only.
DO $$
DECLARE
    invalid_bindings TEXT[];
BEGIN
    SELECT ARRAY_AGG(
        FORMAT('group=%s/account=%s', invalid.group_id, invalid.account_id)
        ORDER BY invalid.group_id, invalid.account_id
    )
    INTO invalid_bindings
    FROM (
        SELECT DISTINCT g.id AS group_id, a.id AS account_id
        FROM groups AS g
        JOIN account_groups AS ag ON ag.group_id = g.id
        JOIN accounts AS a ON a.id = ag.account_id
        WHERE g.platform = 'copilot'
          AND NOT (
              (a.platform = 'copilot' AND a.type = 'apikey')
              OR (
                  a.platform = 'openai'
                  AND a.type = 'oauth'
                  AND LOWER(BTRIM(COALESCE(a.credentials ->> 'oauth_profile', ''))) = 'github_copilot'
                  AND jsonb_typeof(a.credentials -> 'github_access_token') = 'string'
                  AND NULLIF(BTRIM(a.credentials ->> 'github_access_token'), '') IS NOT NULL
              )
          )
    ) AS invalid;

    IF invalid_bindings IS NOT NULL THEN
        RAISE EXCEPTION USING
            ERRCODE = 'check_violation',
            MESSAGE = FORMAT(
                'migration 235 blocked: legacy Copilot groups contain non-Copilot members %s',
                invalid_bindings
            );
    END IF;
END
$$;

-- Copilot accounts may only belong to groups supported by the canonical
-- OpenAI/OAuth scheduler. This also catches a legacy account linked to Gemini,
-- Antigravity, Grok, or composite groups before its platform is rewritten.
DO $$
DECLARE
    invalid_bindings TEXT[];
BEGIN
    SELECT ARRAY_AGG(
        FORMAT('group=%s/account=%s', invalid.group_id, invalid.account_id)
        ORDER BY invalid.group_id, invalid.account_id
    )
    INTO invalid_bindings
    FROM (
        SELECT DISTINCT g.id AS group_id, a.id AS account_id
        FROM accounts AS a
        JOIN account_groups AS ag ON ag.account_id = a.id
        JOIN groups AS g ON g.id = ag.group_id
        WHERE (
            a.platform = 'copilot'
            OR EXISTS (
                SELECT 1
                FROM account_groups AS legacy_ag
                JOIN groups AS legacy_group ON legacy_group.id = legacy_ag.group_id
                WHERE legacy_ag.account_id = a.id
                  AND legacy_group.platform = 'copilot'
            )
        )
          AND g.platform NOT IN ('copilot', 'openai', 'anthropic')
    ) AS invalid;

    IF invalid_bindings IS NOT NULL THEN
        RAISE EXCEPTION USING
            ERRCODE = 'check_violation',
            MESSAGE = FORMAT(
                'migration 235 blocked: Copilot accounts have incompatible group bindings %s',
                invalid_bindings
            );
    END IF;
END
$$;

-- Explicit backup tables make retries additive instead of turning CREATE TABLE
-- AS SELECT into a permanent no-op after the first run. Rows are retained for
-- audit and for migrations/rollback/235_restore_legacy_copilot_groups.sql.
CREATE TABLE IF NOT EXISTS legacy_copilot_groups_backup_235 (
    id BIGINT PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    platform VARCHAR(50) NOT NULL,
    allow_messages_dispatch BOOLEAN NOT NULL,
    allow_live BOOLEAN NOT NULL,
    require_oauth_only BOOLEAN NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    backed_up_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS legacy_copilot_accounts_backup_235 (
    id BIGINT PRIMARY KEY,
    platform VARCHAR(50) NOT NULL,
    type VARCHAR(20) NOT NULL,
    credentials JSONB NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    backed_up_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Keep the account platform canonical (openai/oauth) while preserving the
-- legacy group's stronger scheduling identity. A dedicated column is required:
-- allow_messages_dispatch/require_oauth_only are valid for ordinary OpenAI
-- groups as well and therefore cannot safely identify a Copilot-only group.
ALTER TABLE groups
    ADD COLUMN IF NOT EXISTS github_copilot_only BOOLEAN NOT NULL DEFAULT FALSE;

INSERT INTO legacy_copilot_groups_backup_235 (
    id,
    name,
    platform,
    allow_messages_dispatch,
    allow_live,
    require_oauth_only,
    updated_at
)
SELECT
    id,
    name,
    platform,
    allow_messages_dispatch,
    allow_live,
    require_oauth_only,
    updated_at
FROM groups
WHERE platform = 'copilot'
ON CONFLICT (id) DO UPDATE
SET
    name = EXCLUDED.name,
    platform = EXCLUDED.platform,
    allow_messages_dispatch = EXCLUDED.allow_messages_dispatch,
    allow_live = EXCLUDED.allow_live,
    require_oauth_only = EXCLUDED.require_oauth_only,
    updated_at = EXCLUDED.updated_at,
    backed_up_at = NOW();

INSERT INTO legacy_copilot_accounts_backup_235 (
    id,
    platform,
    type,
    credentials,
    updated_at
)
SELECT
    id,
    platform,
    type,
    credentials,
    updated_at
FROM accounts
WHERE platform = 'copilot'
ON CONFLICT (id) DO UPDATE
SET
    platform = EXCLUDED.platform,
    type = EXCLUDED.type,
    credentials = EXCLUDED.credentials,
    updated_at = EXCLUDED.updated_at,
    backed_up_at = NOW();

-- Migration 233 handled github_token-only rows. This migration also handles
-- github_access_token-only rows and equal dual-token rows while preserving all
-- unrelated credentials.
UPDATE accounts
SET
    platform = 'openai',
    type = 'oauth',
    credentials = (credentials - 'github_token') || jsonb_build_object(
        'oauth_profile', 'github_copilot',
        'github_access_token',
        COALESCE(
            NULLIF(BTRIM(credentials ->> 'github_access_token'), ''),
            NULLIF(BTRIM(credentials ->> 'github_token'), '')
        )
    ),
    updated_at = NOW()
WHERE platform = 'copilot';

-- Keep each group ID stable so account_groups and api_keys continue to point at
-- the same logical group. Messages dispatch is required for Anthropic-protocol
-- clients using Copilot through the normalized OpenAI-compatible gateway.
UPDATE groups
SET
    platform = 'openai',
    allow_messages_dispatch = TRUE,
    github_copilot_only = TRUE,
    allow_live = FALSE,
    require_oauth_only = TRUE,
    updated_at = NOW()
WHERE platform = 'copilot';
