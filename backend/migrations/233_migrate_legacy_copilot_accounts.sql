-- Migrate ParaGateway's legacy Copilot API-key account shape to the official
-- OpenAI OAuth profile used by the current GitHub Copilot gateway.
--
-- Preserve billing_pat, billing_username, base_url, model_mapping, and any
-- other account-specific credentials. The short-lived Copilot access_token is
-- intentionally not created here; OpenAITokenProvider exchanges it from the
-- long-lived GitHub token on first use.
UPDATE accounts
SET
    platform = 'openai',
    type = 'oauth',
    credentials = (credentials - 'github_token') || jsonb_build_object(
        'oauth_profile', 'github_copilot',
        'github_access_token', credentials -> 'github_token'
    ),
    updated_at = NOW()
WHERE platform = 'copilot'
  AND type = 'apikey'
  AND jsonb_typeof(credentials -> 'github_token') = 'string'
  AND NULLIF(BTRIM(credentials ->> 'github_token'), '') IS NOT NULL
  AND NOT (credentials ? 'github_access_token');
