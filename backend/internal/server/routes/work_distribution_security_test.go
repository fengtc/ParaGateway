package routes

import (
	"context"
	"net/http"
	"net/http/httptest"
	"strings"
	"sync"
	"testing"
	"time"

	"github.com/Wei-Shaw/sub2api/internal/handler"
	adminhandler "github.com/Wei-Shaw/sub2api/internal/handler/admin"
	servermiddleware "github.com/Wei-Shaw/sub2api/internal/server/middleware"
	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/gin-gonic/gin"
	"github.com/stretchr/testify/require"
)

type workDistributionRouteRepository struct {
	owners           map[int64]int64
	lastListedUser   int64
	lastCreateInput  service.CreateWorkReviewInput
	lastResolveInput service.ResolveWorkReviewInput
}

func newWorkDistributionRouteRepository() *workDistributionRouteRepository {
	return &workDistributionRouteRepository{
		owners: map[int64]int64{
			101: 11,
			202: 12,
		},
	}
}

func (r *workDistributionRouteRepository) GetAggregates(context.Context, service.WorkDistributionFilter) ([]service.WorkDistributionAggregate, error) {
	rows := make([]service.WorkDistributionAggregate, 0, 5)
	for userID := int64(11); userID <= 15; userID++ {
		rows = append(rows, service.WorkDistributionAggregate{
			UserID: userID, Email: "member@example.test", Department: "engineering", Role: "developer",
			WorkRelated: service.WorkRelatedWork, Category: service.WorkCategoryCoding, Classified: true,
			Requests: 5, TotalTokens: 500, ConfidenceSum: 4.5, ConfidenceSampleCount: 5, DepartmentCohortSize: 5,
		})
	}
	return rows, nil
}

func (r *workDistributionRouteRepository) ListRecords(context.Context, service.WorkDistributionRecordFilter) ([]service.WorkDistributionRecord, int64, error) {
	return []service.WorkDistributionRecord{{
		UsageLogID: 101, UserID: 11, Email: "owner-11@example.test", Department: "engineering", Role: "developer",
		TotalTokens: 500, CreatedAt: time.Date(2026, 8, 1, 8, 0, 0, 0, time.UTC),
	}}, 1, nil
}

func (r *workDistributionRouteRepository) ListUserClassifications(_ context.Context, userID int64, _, _ int) ([]service.WorkDistributionRecord, int64, error) {
	r.lastListedUser = userID
	return []service.WorkDistributionRecord{{
		UsageLogID: 101, UserID: userID, Email: "owner-11@example.test", Department: "engineering", Role: "developer",
		TotalTokens: 500, CreatedAt: time.Date(2026, 8, 1, 8, 0, 0, 0, time.UTC),
	}}, 1, nil
}

func (r *workDistributionRouteRepository) CreateReview(_ context.Context, input service.CreateWorkReviewInput) (*service.WorkDistributionReview, error) {
	ownerUserID, exists := r.owners[input.UsageLogID]
	if !exists || (input.OwnerUserID > 0 && input.OwnerUserID != ownerUserID) {
		return nil, service.ErrWorkUsageNotFound
	}
	r.lastCreateInput = input
	requestedBy := input.RequestedBy
	return &service.WorkDistributionReview{
		ID: 301, UsageLogID: input.UsageLogID, UserID: ownerUserID,
		ProposedWorkRelated: input.WorkRelated, ProposedCategory: input.Category,
		ReasonCode: input.ReasonCode, Status: service.WorkReviewPending,
		RequestedBy: &requestedBy, CreatedAt: time.Date(2026, 8, 1, 9, 0, 0, 0, time.UTC),
	}, nil
}

func (r *workDistributionRouteRepository) ListReviews(context.Context, service.WorkDistributionReviewFilter) ([]service.WorkDistributionReview, int64, error) {
	requestedBy := int64(11)
	return []service.WorkDistributionReview{{
		ID: 301, UsageLogID: 101, UserID: 11, Email: "owner-11@example.test",
		ProposedWorkRelated: service.WorkRelatedWork, ProposedCategory: service.WorkCategoryDocumentation,
		ReasonCode: "incorrect_category", Status: service.WorkReviewPending,
		RequestedBy: &requestedBy, CreatedAt: time.Date(2026, 8, 1, 9, 0, 0, 0, time.UTC),
	}}, 1, nil
}

func (r *workDistributionRouteRepository) ResolveReview(_ context.Context, input service.ResolveWorkReviewInput) (*service.WorkDistributionReview, error) {
	if input.ReviewID != 301 {
		return nil, service.ErrWorkReviewNotFound
	}
	r.lastResolveInput = input
	resolvedBy := input.ResolvedBy
	resolvedAt := time.Date(2026, 8, 1, 10, 0, 0, 0, time.UTC)
	return &service.WorkDistributionReview{
		ID: 301, UsageLogID: 101, UserID: 11, Email: "owner-11@example.test",
		ProposedWorkRelated: service.WorkRelatedWork, ProposedCategory: service.WorkCategoryDocumentation,
		ReasonCode: "incorrect_category", Status: input.Decision, ResolutionNote: input.ResolutionNote,
		ResolvedBy: &resolvedBy, ResolvedAt: &resolvedAt, CreatedAt: time.Date(2026, 8, 1, 9, 0, 0, 0, time.UTC),
	}, nil
}

func workDistributionRouteHandlers(repo service.WorkDistributionRepository) *handler.Handlers {
	workService := service.NewWorkDistributionService(repo)
	usageHandler := handler.NewUsageHandler(nil, nil, nil, nil)
	usageHandler.SetWorkDistributionService(workService)
	return &handler.Handlers{
		Usage: usageHandler,
		Admin: &handler.AdminHandlers{
			WorkDistribution: adminhandler.NewWorkDistributionHandler(workService),
		},
	}
}

func passWorkDistributionMiddleware(c *gin.Context) { c.Next() }

func workDistributionPanelRateLimiter() *servermiddleware.PanelRateLimiter {
	return servermiddleware.NewPanelRateLimiter(nil, nil)
}

func TestWorkDistributionAdminRoutesRejectUnauthenticatedAndNonAdminRequests(t *testing.T) {
	gin.SetMode(gin.TestMode)
	router := gin.New()
	adminAuth := servermiddleware.AdminAuthMiddleware(func(c *gin.Context) {
		if strings.TrimSpace(c.GetHeader("Authorization")) == "" {
			servermiddleware.AbortWithError(c, http.StatusUnauthorized, "UNAUTHORIZED", "Authorization required")
			return
		}
		servermiddleware.AbortWithError(c, http.StatusForbidden, "FORBIDDEN", "Admin access required")
	})
	RegisterAdminRoutes(
		router.Group("/api/v1"), workDistributionRouteHandlers(newWorkDistributionRouteRepository()), adminAuth,
		servermiddleware.AuditLogMiddleware(passWorkDistributionMiddleware),
		servermiddleware.StepUpAuthMiddleware(passWorkDistributionMiddleware),
		servermiddleware.StrictStepUpAuthMiddleware(passWorkDistributionMiddleware),
		nil, workDistributionPanelRateLimiter(),
	)

	tests := []struct {
		name   string
		method string
		path   string
		body   string
	}{
		{name: "summary", method: http.MethodGet, path: "/api/v1/admin/work-distribution/summary"},
		{name: "records", method: http.MethodGet, path: "/api/v1/admin/work-distribution/records"},
		{name: "create correction", method: http.MethodPost, path: "/api/v1/admin/work-distribution/records/101/correction", body: `{}`},
		{name: "reviews", method: http.MethodGet, path: "/api/v1/admin/work-distribution/reviews"},
		{name: "resolve review", method: http.MethodPost, path: "/api/v1/admin/work-distribution/reviews/301/resolve", body: `{}`},
	}
	for _, tc := range tests {
		for _, identity := range []struct {
			name       string
			authority  string
			wantStatus int
		}{
			{name: "unauthenticated", wantStatus: http.StatusUnauthorized},
			{name: "ordinary user", authority: "Bearer " + "ordinary-user", wantStatus: http.StatusForbidden},
		} {
			t.Run(tc.name+"/"+identity.name, func(t *testing.T) {
				recorder := httptest.NewRecorder()
				request := httptest.NewRequest(tc.method, tc.path, strings.NewReader(tc.body))
				if identity.authority != "" {
					request.Header.Set("Authorization", identity.authority)
				}
				if tc.body != "" {
					request.Header.Set("Content-Type", "application/json")
				}
				router.ServeHTTP(recorder, request)
				require.Equal(t, identity.wantStatus, recorder.Code)
			})
		}
	}
}

func TestWorkClassificationUserRoutesPinOwnershipToAuthenticatedUser(t *testing.T) {
	gin.SetMode(gin.TestMode)
	repo := newWorkDistributionRouteRepository()
	router := gin.New()
	jwtAuth := servermiddleware.JWTAuthMiddleware(func(c *gin.Context) {
		c.Set(string(servermiddleware.ContextKeyUser), servermiddleware.AuthSubject{UserID: 11})
		c.Set(string(servermiddleware.ContextKeyUserRole), service.RoleUser)
		c.Set(servermiddleware.ContextKeyAuthEmail, "owner-11@example.test")
		c.Next()
	})
	RegisterUserRoutes(
		router.Group("/api/v1"), workDistributionRouteHandlers(repo), jwtAuth,
		servermiddleware.AuditLogMiddleware(passWorkDistributionMiddleware), nil,
		workDistributionPanelRateLimiter(),
	)

	recorder := httptest.NewRecorder()
	request := httptest.NewRequest(http.MethodGet, "/api/v1/usage/work-classifications?user_id=12", nil)
	router.ServeHTTP(recorder, request)
	require.Equal(t, http.StatusOK, recorder.Code)
	require.Equal(t, int64(11), repo.lastListedUser)
	require.Contains(t, recorder.Body.String(), "owner-11@example.test")
	require.NotContains(t, recorder.Body.String(), "owner-12@example.test")

	recorder = httptest.NewRecorder()
	request = httptest.NewRequest(http.MethodPost, "/api/v1/usage/work-classifications/101/appeals",
		strings.NewReader(`{"work_related":"work","category":"documentation","reason_code":"incorrect_category"}`))
	request.Header.Set("Content-Type", "application/json")
	router.ServeHTTP(recorder, request)
	require.Equal(t, http.StatusOK, recorder.Code)
	require.Equal(t, int64(11), repo.lastCreateInput.OwnerUserID)
	require.Equal(t, int64(11), repo.lastCreateInput.RequestedBy)

	recorder = httptest.NewRecorder()
	request = httptest.NewRequest(http.MethodPost, "/api/v1/usage/work-classifications/202/appeals",
		strings.NewReader(`{"work_related":"work","category":"documentation","reason_code":"incorrect_category"}`))
	request.Header.Set("Content-Type", "application/json")
	router.ServeHTTP(recorder, request)
	require.Equal(t, http.StatusNotFound, recorder.Code)
	require.Equal(t, int64(101), repo.lastCreateInput.UsageLogID, "a rejected cross-user appeal must not reach persistence")
}

type workDistributionAuditRepository struct {
	mu   sync.Mutex
	logs []*service.AuditLog
}

func (r *workDistributionAuditRepository) BatchInsert(_ context.Context, logs []*service.AuditLog) (int64, error) {
	r.mu.Lock()
	defer r.mu.Unlock()
	r.logs = append(r.logs, logs...)
	return int64(len(logs)), nil
}

func (r *workDistributionAuditRepository) Insert(_ context.Context, log *service.AuditLog) error {
	r.mu.Lock()
	defer r.mu.Unlock()
	r.logs = append(r.logs, log)
	return nil
}

func (r *workDistributionAuditRepository) List(context.Context, *service.AuditLogFilter) (*service.AuditLogList, error) {
	return &service.AuditLogList{}, nil
}

func (r *workDistributionAuditRepository) GetByID(context.Context, int64) (*service.AuditLog, error) {
	return nil, service.ErrAuditLogNotFound
}

func (r *workDistributionAuditRepository) Count(context.Context) (int64, error) { return 0, nil }
func (r *workDistributionAuditRepository) TruncateAll(context.Context) error    { return nil }
func (r *workDistributionAuditRepository) DeleteBefore(context.Context, time.Time, int) (int64, error) {
	return 0, nil
}

func (r *workDistributionAuditRepository) snapshot() []*service.AuditLog {
	r.mu.Lock()
	defer r.mu.Unlock()
	return append([]*service.AuditLog(nil), r.logs...)
}

func TestWorkDistributionAdminReviewAndSensitiveReadsAreAudited(t *testing.T) {
	gin.SetMode(gin.TestMode)
	workRepo := newWorkDistributionRouteRepository()
	auditRepo := &workDistributionAuditRepository{}
	auditService := service.NewAuditLogService(auditRepo, nil)
	auditService.Start()
	stopped := false
	t.Cleanup(func() {
		if !stopped {
			auditService.Stop()
		}
	})

	adminAuth := servermiddleware.AdminAuthMiddleware(func(c *gin.Context) {
		c.Set(string(servermiddleware.ContextKeyUser), servermiddleware.AuthSubject{UserID: 77})
		c.Set(string(servermiddleware.ContextKeyUserRole), service.RoleAdmin)
		c.Set(servermiddleware.ContextKeyAuthEmail, "admin@example.test")
		c.Set("auth_method", "jwt")
		c.Next()
	})
	router := gin.New()
	RegisterAdminRoutes(
		router.Group("/api/v1"), workDistributionRouteHandlers(workRepo), adminAuth,
		servermiddleware.NewAuditLogMiddleware(auditService),
		servermiddleware.StepUpAuthMiddleware(passWorkDistributionMiddleware),
		servermiddleware.StrictStepUpAuthMiddleware(passWorkDistributionMiddleware),
		nil, workDistributionPanelRateLimiter(),
	)

	requests := []struct {
		method string
		path   string
		body   string
	}{
		{method: http.MethodGet, path: "/api/v1/admin/work-distribution/summary?start_date=2026-08-01&end_date=2026-08-02"},
		{method: http.MethodGet, path: "/api/v1/admin/work-distribution/records?start_date=2026-08-01&end_date=2026-08-02"},
		{method: http.MethodGet, path: "/api/v1/admin/work-distribution/reviews"},
		{method: http.MethodPost, path: "/api/v1/admin/work-distribution/reviews/301/resolve",
			body: `{"decision":"approved","resolution_note":"confirmed_correction"}`},
	}
	for _, tc := range requests {
		recorder := httptest.NewRecorder()
		request := httptest.NewRequest(tc.method, tc.path, strings.NewReader(tc.body))
		if tc.body != "" {
			request.Header.Set("Content-Type", "application/json")
		}
		router.ServeHTTP(recorder, request)
		require.Equal(t, http.StatusOK, recorder.Code, "%s %s: %s", tc.method, tc.path, recorder.Body.String())
		if tc.method == http.MethodGet && strings.Contains(tc.path, "/work-distribution/summary") {
			require.Contains(t, recorder.Body.String(), `"roles":[{"role":"developer","user_count":5}]`)
		}
	}
	require.Equal(t, int64(301), workRepo.lastResolveInput.ReviewID)
	require.Equal(t, int64(77), workRepo.lastResolveInput.ResolvedBy)

	auditService.Stop()
	stopped = true
	logs := auditRepo.snapshot()
	wantAudit := map[string]bool{
		"GET /api/v1/admin/work-distribution/summary":                     false,
		"GET /api/v1/admin/work-distribution/records":                     false,
		"GET /api/v1/admin/work-distribution/reviews":                     false,
		"POST /api/v1/admin/work-distribution/reviews/:review_id/resolve": false,
	}
	for _, entry := range logs {
		key := entry.Method + " " + entry.Path
		if _, exists := wantAudit[key]; exists {
			wantAudit[key] = true
			require.NotNil(t, entry.ActorUserID)
			require.Equal(t, int64(77), *entry.ActorUserID)
			require.Equal(t, "admin", entry.ActorRole)
		}
	}
	for key, audited := range wantAudit {
		require.Truef(t, audited, "%s must be recorded as an audited work-distribution operation", key)
	}
}
