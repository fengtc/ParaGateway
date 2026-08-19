ALTER TABLE accounts
    ADD COLUMN IF NOT EXISTS weight integer NOT NULL DEFAULT 100,
    ADD COLUMN IF NOT EXISTS rpm_limit integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS circuit_breaker_threshold integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS circuit_breaker_cooldown_seconds integer NOT NULL DEFAULT 0;

ALTER TABLE accounts
    ADD CONSTRAINT accounts_weight_positive CHECK (weight > 0),
    ADD CONSTRAINT accounts_rpm_limit_nonnegative CHECK (rpm_limit >= 0),
    ADD CONSTRAINT accounts_circuit_breaker_threshold_nonnegative CHECK (circuit_breaker_threshold >= 0),
    ADD CONSTRAINT accounts_circuit_breaker_cooldown_nonnegative CHECK (circuit_breaker_cooldown_seconds >= 0);
