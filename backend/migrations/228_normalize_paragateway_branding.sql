-- Normalize legacy product names in settings that are rendered to users.
-- Technical compatibility identifiers (module paths, API routes, Redis keys,
-- database names and environment variables) are intentionally not changed.
UPDATE settings
SET value = regexp_replace(
        regexp_replace(
            value,
            'Para[[:space:]]+AI[[:space:]]+Coding[[:space:]]+Gateway',
            'ParaGateway',
            'gi'
        ),
        'Sub2API',
        'ParaGateway',
        'gi'
    ),
    updated_at = NOW()
WHERE key IN (
        'site_name',
        'site_subtitle',
        'contact_info',
        'home_content',
        'login_agreement_documents',
        'smtp_from_name'
    )
  AND (
        value ~* 'Sub2API'
        OR value ~* 'Para[[:space:]]+AI[[:space:]]+Coding[[:space:]]+Gateway'
    );
