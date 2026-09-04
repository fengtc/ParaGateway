//go:build unit

package repository

import (
	"context"
	"regexp"
	"testing"

	"github.com/DATA-DOG/go-sqlmock"
	"github.com/Wei-Shaw/sub2api/internal/pkg/pagination"
	"github.com/Wei-Shaw/sub2api/internal/pkg/usagestats"
	"github.com/stretchr/testify/require"
)

func TestListWithFiltersMatchesCurrentDepartment(t *testing.T) {
	db, mock := newSQLMock(t)
	repo := &usageLogRepository{sql: db}

	departmentCondition := `EXISTS ( SELECT 1 FROM user_attribute_values department_value JOIN user_attribute_definitions department_def ON department_def.id = department_value.attribute_id AND department_def.key = 'department' AND department_def.deleted_at IS NULL WHERE department_value.user_id = usage_logs.user_id AND LOWER(BTRIM(department_value.value)) = LOWER($1) )`
	mock.ExpectQuery(regexp.QuoteMeta("SELECT COUNT(*) FROM usage_logs WHERE " + departmentCondition)).
		WithArgs("研发部").
		WillReturnRows(sqlmock.NewRows([]string{"count"}).AddRow(int64(0)))
	mock.ExpectQuery(regexp.QuoteMeta("FROM usage_logs WHERE " + departmentCondition + " ORDER BY id DESC LIMIT $2 OFFSET $3")).
		WithArgs("研发部", 20, 0).
		WillReturnRows(sqlmock.NewRows([]string{"id"}))

	logs, page, err := repo.ListWithFilters(context.Background(), pagination.PaginationParams{Page: 1, PageSize: 20}, usagestats.UsageLogFilters{
		Department: " 研发部 ",
		ExactTotal: true,
	})

	require.NoError(t, err)
	require.Empty(t, logs)
	require.Equal(t, int64(0), page.Total)
	require.NoError(t, mock.ExpectationsWereMet())
}

func TestDepartmentFilterUsesExactPaginationTotal(t *testing.T) {
	require.False(t, shouldUseFastUsageLogTotal(usagestats.UsageLogFilters{Department: "研发部"}))
}

func TestLoadUserDepartmentsReturnsCurrentValues(t *testing.T) {
	db, mock := newSQLMock(t)
	repo := &usageLogRepository{sql: db}

	mock.ExpectQuery(regexp.QuoteMeta("WHERE uav.user_id = ANY($1)")).
		WithArgs(sqlmock.AnyArg()).
		WillReturnRows(sqlmock.NewRows([]string{"user_id", "department"}).
			AddRow(int64(11), "研发部").
			AddRow(int64(22), "销售部"))

	departments, err := repo.loadUserDepartments(context.Background(), []int64{11, 22})

	require.NoError(t, err)
	require.Equal(t, map[int64]string{11: "研发部", 22: "销售部"}, departments)
	require.NoError(t, mock.ExpectationsWereMet())
}

func TestLoadUserDepartmentsReturnsEmptyMapWithoutValues(t *testing.T) {
	db, mock := newSQLMock(t)
	repo := &usageLogRepository{sql: db}

	mock.ExpectQuery(regexp.QuoteMeta("WHERE uav.user_id = ANY($1)")).
		WithArgs(sqlmock.AnyArg()).
		WillReturnRows(sqlmock.NewRows([]string{"user_id", "department"}))

	departments, err := repo.loadUserDepartments(context.Background(), []int64{33})

	require.NoError(t, err)
	require.Empty(t, departments)
	require.NoError(t, mock.ExpectationsWereMet())
}

func TestLoadUserDepartmentsSkipsQueryWithoutUsers(t *testing.T) {
	db, mock := newSQLMock(t)
	repo := &usageLogRepository{sql: db}

	departments, err := repo.loadUserDepartments(context.Background(), nil)

	require.NoError(t, err)
	require.Empty(t, departments)
	require.NoError(t, mock.ExpectationsWereMet())
}
