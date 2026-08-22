-- Seed the built-in department user attribute used by the admin user editor.
INSERT INTO user_attribute_definitions (
    key,
    name,
    description,
    type,
    options,
    required,
    validation,
    placeholder,
    display_order,
    enabled,
    created_at,
    updated_at
)
SELECT
    'department',
    '部门',
    '用户所属部门',
    'text',
    '[]'::jsonb,
    false,
    '{}'::jsonb,
    '请输入部门',
    COALESCE((
        SELECT MAX(display_order) + 1
        FROM user_attribute_definitions
        WHERE deleted_at IS NULL
    ), 0),
    true,
    NOW(),
    NOW()
WHERE NOT EXISTS (
    SELECT 1
    FROM user_attribute_definitions
    WHERE key = 'department'
      AND deleted_at IS NULL
);
