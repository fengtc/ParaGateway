package admin

import (
	"context"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"testing"
	"time"

	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/gin-gonic/gin"
	"github.com/stretchr/testify/require"
)

// 捕获 ListUsers 入参、返回一个已删用户的 admin service 桩。
type searchUsersAdminStub struct {
	service.AdminService
	gotFilters []service.UserListFilters
}

func (s *searchUsersAdminStub) ListUsers(ctx context.Context, page, pageSize int, filters service.UserListFilters, sortBy, sortOrder string) ([]service.User, int64, error) {
	s.gotFilters = append(s.gotFilters, filters)
	if filters.EmailExact != "" {
		return nil, 0, nil
	}
	ts := time.Date(2026, 5, 28, 0, 0, 0, 0, time.UTC)
	return []service.User{
		{ID: 1, Email: "active@test.com"},
		{ID: 2, Email: "deleted@test.com", DeletedAt: &ts},
	}, 2, nil
}

func TestAdminUsageSearchUsers_IncludesDeletedAndFlags(t *testing.T) {
	gin.SetMode(gin.TestMode)
	stub := &searchUsersAdminStub{}
	handler := NewUsageHandler(nil, nil, stub, nil)
	router := gin.New()
	router.GET("/admin/usage/search-users", handler.SearchUsers)

	req := httptest.NewRequest(http.MethodGet, "/admin/usage/search-users?q=test", nil)
	rec := httptest.NewRecorder()
	router.ServeHTTP(rec, req)

	require.Equal(t, http.StatusOK, rec.Code)
	require.Len(t, stub.gotFilters, 2)
	require.Equal(t, "test", stub.gotFilters[0].EmailExact)
	require.Empty(t, stub.gotFilters[0].Search)
	require.True(t, stub.gotFilters[0].IncludeDeleted, "精确邮箱查询必须请求 IncludeDeleted")
	require.Empty(t, stub.gotFilters[1].EmailExact)
	require.Equal(t, "test", stub.gotFilters[1].Search)
	require.True(t, stub.gotFilters[1].IncludeDeleted, "模糊回退查询必须请求 IncludeDeleted")

	var resp struct {
		Data []struct {
			ID      int64  `json:"id"`
			Email   string `json:"email"`
			Deleted bool   `json:"deleted"`
		} `json:"data"`
	}
	require.NoError(t, json.Unmarshal(rec.Body.Bytes(), &resp))
	require.Len(t, resp.Data, 2)
	require.False(t, resp.Data[0].Deleted)
	require.True(t, resp.Data[1].Deleted, "已删用户必须标记 deleted=true")
}

type duplicateEmailSearchUsersAdminStub struct {
	service.AdminService
	gotFilters []service.UserListFilters
}

func (s *duplicateEmailSearchUsersAdminStub) ListUsers(ctx context.Context, page, pageSize int, filters service.UserListFilters, sortBy, sortOrder string) ([]service.User, int64, error) {
	s.gotFilters = append(s.gotFilters, filters)
	deletedAt := time.Date(2026, 6, 8, 2, 44, 49, 0, time.UTC)
	return []service.User{
		{ID: 4, Email: "xiegy@paratera.com", DeletedAt: &deletedAt},
		{ID: 7, Email: "xiegy@paratera.com"},
	}, 2, nil
}

func TestAdminUsageSearchUsers_PrefersActiveExactDuplicate(t *testing.T) {
	gin.SetMode(gin.TestMode)
	stub := &duplicateEmailSearchUsersAdminStub{}
	handler := NewUsageHandler(nil, nil, stub, nil)
	router := gin.New()
	router.GET("/admin/usage/search-users", handler.SearchUsers)

	req := httptest.NewRequest(http.MethodGet, "/admin/usage/search-users?q=%20XIEGY%40paratera.com%20", nil)
	rec := httptest.NewRecorder()
	router.ServeHTTP(rec, req)

	require.Equal(t, http.StatusOK, rec.Code)
	require.Len(t, stub.gotFilters, 1, "精确邮箱命中后不应继续执行模糊查询")
	require.Equal(t, "XIEGY@paratera.com", stub.gotFilters[0].EmailExact)
	require.Empty(t, stub.gotFilters[0].Search)
	require.True(t, stub.gotFilters[0].IncludeDeleted, "SearchUsers 必须保留已删除用户以支持历史记录")

	var resp struct {
		Data []struct {
			ID      int64  `json:"id"`
			Email   string `json:"email"`
			Deleted bool   `json:"deleted"`
		} `json:"data"`
	}
	require.NoError(t, json.Unmarshal(rec.Body.Bytes(), &resp))
	require.Len(t, resp.Data, 2)
	require.EqualValues(t, 7, resp.Data[0].ID, "同邮箱时活动用户必须排在已删除用户前")
	require.False(t, resp.Data[0].Deleted)
	require.EqualValues(t, 4, resp.Data[1].ID)
	require.True(t, resp.Data[1].Deleted)
}
