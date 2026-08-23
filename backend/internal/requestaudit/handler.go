package requestaudit

import (
	"strconv"
	"strings"
	"time"

	infraerrors "github.com/Wei-Shaw/sub2api/internal/pkg/errors"
	"github.com/Wei-Shaw/sub2api/internal/pkg/response"
	"github.com/Wei-Shaw/sub2api/internal/server/middleware"
	"github.com/gin-gonic/gin"
)

type AdminHandler struct{ service *Service }

func NewAdminHandler(service *Service) *AdminHandler { return &AdminHandler{service: service} }

func (h *AdminHandler) GetPolicy(c *gin.Context) {
	response.Success(c, h.service.GetPolicy())
}

func (h *AdminHandler) UpdatePolicy(c *gin.Context) {
	var request UpdatePolicyRequest
	if err := c.ShouldBindJSON(&request); err != nil {
		setPolicyAudit(c, "failed", "request_audit_invalid_policy", request, nil)
		response.ErrorFrom(c, infraerrors.BadRequest("request_audit_invalid_policy", "请求审计策略请求无效"))
		return
	}
	updated, err := h.service.UpdatePolicy(c.Request.Context(), request, adminID(c))
	if err != nil {
		setPolicyAudit(c, "failed", infraerrors.Reason(err), request, nil)
		response.ErrorFrom(c, err)
		return
	}
	setPolicyAudit(c, "success", "", request, updated)
	response.Success(c, updated)
}

func (h *AdminHandler) Runtime(c *gin.Context) {
	response.Success(c, h.service.Runtime())
}

func (h *AdminHandler) List(c *gin.Context) {
	page, err := positiveInt(c.Query("page"), 1, 0)
	if err != nil {
		response.ErrorFrom(c, err)
		return
	}
	pageSize, err := positiveInt(c.Query("page_size"), 20, 100)
	if err != nil {
		response.ErrorFrom(c, err)
		return
	}
	filter, err := filterFromQuery(c)
	if err != nil {
		response.ErrorFrom(c, err)
		return
	}
	result, err := h.service.List(c.Request.Context(), filter, page, pageSize)
	if err != nil {
		response.ErrorFrom(c, err)
		return
	}
	response.Success(c, result)
}

func (h *AdminHandler) Get(c *gin.Context) {
	id, err := recordID(c)
	if err != nil {
		response.ErrorFrom(c, err)
		return
	}
	item, err := h.service.Get(c.Request.Context(), id)
	if err != nil {
		response.ErrorFrom(c, err)
		return
	}
	response.Success(c, item)
}

func (h *AdminHandler) GetContent(c *gin.Context) {
	id, err := recordID(c)
	if err != nil {
		response.ErrorFrom(c, err)
		return
	}
	content, err := h.service.GetContent(c.Request.Context(), id)
	if err != nil {
		response.ErrorFrom(c, err)
		return
	}
	middleware.SetAuditExtra(c, map[string]any{"result": "success", "event_id": id})
	response.Success(c, content)
}

func filterFromQuery(c *gin.Context) (Filter, error) {
	filter := Filter{
		RequestID: strings.TrimSpace(c.Query("request_id")),
		Model:     strings.TrimSpace(c.Query("model")),
		Query:     strings.TrimSpace(c.Query("q")),
	}
	var err error
	if filter.UserID, err = optionalPositiveInt64(c.Query("user_id"), "用户 ID"); err != nil {
		return Filter{}, err
	}
	if filter.APIKeyID, err = optionalPositiveInt64(c.Query("api_key_id"), "API Key ID"); err != nil {
		return Filter{}, err
	}
	if filter.GroupID, err = optionalPositiveInt64(c.Query("group_id"), "分组 ID"); err != nil {
		return Filter{}, err
	}
	if value := strings.TrimSpace(c.Query("status_code")); value != "" {
		status, parseErr := strconv.Atoi(value)
		if parseErr != nil || status < 100 || status > 599 {
			return Filter{}, infraerrors.BadRequest("request_audit_invalid_status", "HTTP 状态码无效")
		}
		filter.StatusCode = &status
	}
	if filter.StartAt, err = optionalTime(c.Query("start_at"), "开始时间"); err != nil {
		return Filter{}, err
	}
	if filter.EndAt, err = optionalTime(c.Query("end_at"), "结束时间"); err != nil {
		return Filter{}, err
	}
	if filter.StartAt != nil && filter.EndAt != nil && !filter.StartAt.Before(*filter.EndAt) {
		return Filter{}, infraerrors.BadRequest("request_audit_invalid_time_range", "开始时间必须早于结束时间")
	}
	return filter, nil
}

func positiveInt(value string, fallback, max int) (int, error) {
	value = strings.TrimSpace(value)
	if value == "" {
		return fallback, nil
	}
	parsed, err := strconv.Atoi(value)
	if err != nil || parsed < 1 || max > 0 && parsed > max {
		return 0, infraerrors.BadRequest("request_audit_invalid_pagination", "分页参数无效")
	}
	return parsed, nil
}

func optionalPositiveInt64(value, label string) (*int64, error) {
	value = strings.TrimSpace(value)
	if value == "" {
		return nil, nil
	}
	parsed, err := strconv.ParseInt(value, 10, 64)
	if err != nil || parsed <= 0 {
		return nil, infraerrors.BadRequest("request_audit_invalid_filter", label+" 无效")
	}
	return &parsed, nil
}

func optionalTime(value, label string) (*time.Time, error) {
	value = strings.TrimSpace(value)
	if value == "" {
		return nil, nil
	}
	parsed, err := time.Parse(time.RFC3339, value)
	if err != nil {
		return nil, infraerrors.BadRequest("request_audit_invalid_time", label+"格式无效")
	}
	parsed = parsed.UTC()
	return &parsed, nil
}

func recordID(c *gin.Context) (int64, error) {
	id, err := strconv.ParseInt(strings.TrimSpace(c.Param("id")), 10, 64)
	if err != nil || id <= 0 {
		return 0, infraerrors.BadRequest("request_audit_invalid_record_id", "请求审计记录 ID 无效")
	}
	return id, nil
}

func adminID(c *gin.Context) int64 {
	if subject, ok := middleware.GetAuthSubjectFromContext(c); ok {
		return subject.UserID
	}
	return 0
}

func setPolicyAudit(c *gin.Context, result, errorCode string, request UpdatePolicyRequest, policy *Policy) {
	fields := map[string]any{
		"result": result, "error_code": errorCode, "enabled": request.Enabled,
		"capture_mode": request.CaptureMode, "retention_days": request.RetentionDays,
		"encrypted_content": request.StoreEncryptedContent,
	}
	if policy != nil {
		fields["config_version"] = policy.Version
	}
	middleware.SetAuditExtra(c, fields)
}
