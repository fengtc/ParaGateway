//go:build unit

package repository

import (
	"context"
	"regexp"
	"testing"

	"github.com/DATA-DOG/go-sqlmock"
	"github.com/stretchr/testify/require"
)

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
