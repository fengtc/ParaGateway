-- Enterprise request/response audit. The feature is disabled by default.
-- Raw request and response bodies are never stored as plaintext: administrators
-- may choose redacted previews only, or encrypted raw content plus previews.

CREATE TABLE IF NOT EXISTS request_audit_policies (
    id                       SMALLINT PRIMARY KEY DEFAULT 1,
    enabled                  BOOLEAN NOT NULL DEFAULT FALSE,
    capture_mode             VARCHAR(24) NOT NULL DEFAULT 'all',
    sample_rate              NUMERIC(5, 2) NOT NULL DEFAULT 100,
    retention_days           INT NOT NULL DEFAULT 30,
    capture_request_body     BOOLEAN NOT NULL DEFAULT TRUE,
    capture_response_body    BOOLEAN NOT NULL DEFAULT TRUE,
    store_encrypted_content  BOOLEAN NOT NULL DEFAULT FALSE,
    redaction_level          VARCHAR(24) NOT NULL DEFAULT 'standard',
    max_body_bytes           INT NOT NULL DEFAULT 1048576,
    version                  BIGINT NOT NULL DEFAULT 1,
    updated_by               BIGINT REFERENCES users(id) ON DELETE SET NULL,
    updated_at               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_request_audit_policy_singleton CHECK (id = 1),
    CONSTRAINT chk_request_audit_capture_mode
        CHECK (capture_mode IN ('all', 'errors', 'sample')),
    CONSTRAINT chk_request_audit_sample_rate
        CHECK (sample_rate >= 0 AND sample_rate <= 100),
    CONSTRAINT chk_request_audit_retention_days
        CHECK (retention_days BETWEEN 1 AND 3650),
    CONSTRAINT chk_request_audit_redaction_level
        CHECK (redaction_level IN ('standard', 'strict')),
    CONSTRAINT chk_request_audit_max_body_bytes
        CHECK (max_body_bytes BETWEEN 4096 AND 4194304),
    CONSTRAINT chk_request_audit_policy_version CHECK (version >= 1)
);

INSERT INTO request_audit_policies (id)
VALUES (1)
ON CONFLICT (id) DO NOTHING;

CREATE TABLE IF NOT EXISTS request_audit_records (
    id                         BIGSERIAL PRIMARY KEY,
    request_id                 VARCHAR(128) NOT NULL DEFAULT '',
    user_id                    BIGINT REFERENCES users(id) ON DELETE SET NULL,
    username_snapshot          VARCHAR(255) NOT NULL DEFAULT '',
    user_email_snapshot        VARCHAR(320) NOT NULL DEFAULT '',
    api_key_id                 BIGINT REFERENCES api_keys(id) ON DELETE SET NULL,
    api_key_name_snapshot      VARCHAR(255) NOT NULL DEFAULT '',
    group_id                   BIGINT REFERENCES groups(id) ON DELETE SET NULL,
    group_name_snapshot        VARCHAR(255) NOT NULL DEFAULT '',
    method                     VARCHAR(16) NOT NULL DEFAULT '',
    endpoint                   VARCHAR(512) NOT NULL DEFAULT '',
    model                      VARCHAR(255) NOT NULL DEFAULT '',
    client_ip                  VARCHAR(64) NOT NULL DEFAULT '',
    status_code                INT NOT NULL DEFAULT 0,
    latency_ms                 BIGINT NOT NULL DEFAULT 0,
    is_stream                  BOOLEAN NOT NULL DEFAULT FALSE,
    capture_reason             VARCHAR(32) NOT NULL DEFAULT 'all',
    policy_version             BIGINT NOT NULL DEFAULT 1,
    request_content_type       VARCHAR(255) NOT NULL DEFAULT '',
    response_content_type      VARCHAR(255) NOT NULL DEFAULT '',
    request_preview            TEXT NOT NULL DEFAULT '',
    response_preview           TEXT NOT NULL DEFAULT '',
    request_body_ciphertext    TEXT NOT NULL DEFAULT '',
    response_body_ciphertext   TEXT NOT NULL DEFAULT '',
    encryption_version         VARCHAR(32) NOT NULL DEFAULT '',
    request_bytes              BIGINT NOT NULL DEFAULT 0,
    response_bytes             BIGINT NOT NULL DEFAULT 0,
    request_truncated          BOOLEAN NOT NULL DEFAULT FALSE,
    response_truncated         BOOLEAN NOT NULL DEFAULT FALSE,
    request_body_omitted       BOOLEAN NOT NULL DEFAULT FALSE,
    response_body_omitted      BOOLEAN NOT NULL DEFAULT FALSE,
    content_error              VARCHAR(255) NOT NULL DEFAULT '',
    expires_at                 TIMESTAMPTZ NOT NULL,
    created_at                 TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_request_audit_status_code CHECK (status_code >= 0 AND status_code <= 999),
    CONSTRAINT chk_request_audit_nonnegative
        CHECK (latency_ms >= 0 AND policy_version >= 1 AND request_bytes >= 0 AND response_bytes >= 0)
);

CREATE INDEX IF NOT EXISTS idx_request_audit_records_created
    ON request_audit_records (created_at DESC, id DESC);
CREATE INDEX IF NOT EXISTS idx_request_audit_records_expires
    ON request_audit_records (expires_at, id);
CREATE INDEX IF NOT EXISTS idx_request_audit_records_user_created
    ON request_audit_records (user_id, created_at DESC, id DESC);
CREATE INDEX IF NOT EXISTS idx_request_audit_records_api_key_created
    ON request_audit_records (api_key_id, created_at DESC, id DESC);
CREATE INDEX IF NOT EXISTS idx_request_audit_records_group_created
    ON request_audit_records (group_id, created_at DESC, id DESC);
CREATE INDEX IF NOT EXISTS idx_request_audit_records_request_id
    ON request_audit_records (request_id);
CREATE INDEX IF NOT EXISTS idx_request_audit_records_status_created
    ON request_audit_records (status_code, created_at DESC, id DESC);

COMMENT ON TABLE request_audit_records IS
    'Optional enterprise request/response archive; raw bodies are AES-256-GCM ciphertext only';
COMMENT ON COLUMN request_audit_records.request_preview IS
    'Redacted and length-bounded request preview';
COMMENT ON COLUMN request_audit_records.request_body_ciphertext IS
    'AES-256-GCM ciphertext; never plaintext';
