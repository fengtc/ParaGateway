-- Worker 风格的上游账号是独立领域，不复用官方平台账号 accounts 表。
CREATE TABLE IF NOT EXISTS upstream_accounts (
    id text PRIMARY KEY,
    name varchar(80) NOT NULL,
    provider_type varchar(16) NOT NULL,
    base_url text NOT NULL,
    auth_type varchar(16) NOT NULL DEFAULT 'api_key',
    credential_ciphertext text NOT NULL,
    credential_hint varchar(16) NOT NULL,

    oauth_profile varchar(32),
    oauth_refresh_token_ciphertext text,
    oauth_expires_at timestamptz,
    oauth_client_id text,
    oauth_account_id text,
    oauth_email text,
    oauth_scope text,

    copilot_enabled boolean NOT NULL DEFAULT false,
    copilot_github_login text,
    copilot_access_token_ciphertext text,
    copilot_token_expires_at timestamptz,
    copilot_token_refresh_at timestamptz,

    wif_subject_token_url text,
    wif_client_id text,
    wif_client_auth_method varchar(32),
    wif_audience text,
    wif_scope text,
    wif_identity_provider_id text,
    wif_service_account_id text,
    wif_federation_rule_id text,
    wif_organization_id text,
    wif_workspace_id text,

    is_active boolean NOT NULL DEFAULT true,
    priority integer NOT NULL DEFAULT 100,
    weight integer NOT NULL DEFAULT 100,
    max_concurrency integer NOT NULL DEFAULT 8,
    rpm_limit integer NOT NULL DEFAULT 120,
    circuit_breaker_threshold integer NOT NULL DEFAULT 3,
    circuit_breaker_cooldown_seconds integer NOT NULL DEFAULT 60,

    quota_status varchar(16) NOT NULL DEFAULT 'unknown',
    quota_utilization double precision,
    quota_resets_at timestamptz,
    quota_checked_at timestamptz,
    quota_five_hour_utilization double precision,
    quota_five_hour_resets_at timestamptz,
    quota_seven_day_utilization double precision,
    quota_seven_day_resets_at timestamptz,
    quota_seven_day_sonnet_utilization double precision,
    quota_seven_day_sonnet_resets_at timestamptz,
    cooldown_until timestamptz,
    cooldown_reason text,
    last_upstream_status integer,
    last_success_at timestamptz,
    last_failure_at timestamptz,

    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    deleted_at timestamptz,

    CONSTRAINT upstream_accounts_provider_type_check CHECK (provider_type IN ('openai', 'claude')),
    CONSTRAINT upstream_accounts_auth_type_check CHECK (auth_type IN ('api_key', 'wif')),
    CONSTRAINT upstream_accounts_priority_check CHECK (priority BETWEEN 1 AND 9999),
    CONSTRAINT upstream_accounts_weight_check CHECK (weight BETWEEN 1 AND 10000),
    CONSTRAINT upstream_accounts_max_concurrency_check CHECK (max_concurrency BETWEEN 1 AND 10000),
    CONSTRAINT upstream_accounts_rpm_limit_check CHECK (rpm_limit BETWEEN 1 AND 1000000),
    CONSTRAINT upstream_accounts_breaker_threshold_check CHECK (circuit_breaker_threshold BETWEEN 1 AND 1000),
    CONSTRAINT upstream_accounts_breaker_cooldown_check CHECK (circuit_breaker_cooldown_seconds BETWEEN 1 AND 86400),
    CONSTRAINT upstream_accounts_quota_status_check CHECK (quota_status IN ('unknown', 'available', 'exhausted', 'error'))
);

CREATE INDEX IF NOT EXISTS idx_upstream_accounts_routing
    ON upstream_accounts (provider_type, is_active, priority, created_at)
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS idx_upstream_accounts_pool_health
    ON upstream_accounts (is_active, quota_status, cooldown_until, priority, created_at)
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS idx_upstream_accounts_deleted_at
    ON upstream_accounts (deleted_at);

