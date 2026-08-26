-- Explicit operator rollback for migration 235. This directory is intentionally
-- outside migrations/*.sql and is therefore not embedded or run automatically.
-- Run during a maintenance window after stopping writers that can edit the
-- affected groups/accounts.

BEGIN;

-- A group created or duplicated after migration 235 has no legacy row to
-- restore. Dropping the marker would silently turn it into an ordinary OpenAI
-- group, so fail closed and require the operator to remove or migrate it first.
DO $$
DECLARE
    unbacked_group_ids BIGINT[];
BEGIN
    SELECT ARRAY_AGG(g.id ORDER BY g.id)
    INTO unbacked_group_ids
    FROM groups AS g
    WHERE g.github_copilot_only = TRUE
      AND NOT EXISTS (
          SELECT 1
          FROM legacy_copilot_groups_backup_235 AS backup
          WHERE backup.id = g.id
      );

    IF unbacked_group_ids IS NOT NULL THEN
        RAISE EXCEPTION USING
            ERRCODE = 'check_violation',
            MESSAGE = FORMAT(
                'rollback 235 blocked: Copilot-only group IDs without legacy backup=%s',
                unbacked_group_ids
            );
    END IF;
END
$$;

DO $$
BEGIN
    UPDATE accounts AS account
    SET
        platform = backup.platform,
        type = backup.type,
        credentials = backup.credentials,
        updated_at = backup.updated_at
    FROM legacy_copilot_accounts_backup_235 AS backup
    WHERE account.id = backup.id;

    UPDATE groups AS target_group
    SET
        platform = backup.platform,
        allow_messages_dispatch = backup.allow_messages_dispatch,
        allow_live = backup.allow_live,
        require_oauth_only = backup.require_oauth_only,
        github_copilot_only = FALSE,
        updated_at = backup.updated_at
    FROM legacy_copilot_groups_backup_235 AS backup
    WHERE target_group.id = backup.id;
END
$$;

ALTER TABLE groups DROP COLUMN IF EXISTS github_copilot_only;

-- Make a later deployment re-run migration 235. Leaving this row behind after
-- dropping the column would make the runner skip 235 and the application would
-- then query a column that no longer exists.
DELETE FROM schema_migrations
WHERE filename = '235_normalize_legacy_copilot_groups.sql';

COMMIT;
