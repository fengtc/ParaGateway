package migrations

import (
	"strings"
	"testing"

	"github.com/stretchr/testify/require"
)

func TestDepartmentUserAttributeMigration(t *testing.T) {
	content, err := FS.ReadFile("227_seed_department_user_attribute.sql")
	require.NoError(t, err)

	sql := strings.Join(strings.Fields(string(content)), " ")
	require.Contains(t, sql, "INSERT INTO user_attribute_definitions")
	require.Contains(t, sql, "'department', '部门', '用户所属部门', 'text'")
	require.Contains(t, sql, "'请输入部门'")
	require.Contains(t, sql, "SELECT MAX(display_order) + 1")
	require.Contains(t, sql, "WHERE NOT EXISTS")
	require.Contains(t, sql, "WHERE key = 'department' AND deleted_at IS NULL")
}
