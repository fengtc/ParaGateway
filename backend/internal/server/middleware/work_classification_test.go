package middleware

import (
	"bytes"
	"io"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
	"unicode/utf8"

	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/gin-gonic/gin"
	"github.com/stretchr/testify/require"
)

type workClassificationTrackingBody struct {
	io.Reader
	closed bool
}

func (b *workClassificationTrackingBody) Close() error {
	b.closed = true
	return nil
}

func TestWorkClassificationAttachesOnlyStructuredResultAndPreservesBody(t *testing.T) {
	gin.SetMode(gin.TestMode)
	body := `{"model":"example","messages":[{"role":"user","content":"请修复编译错误并补充单元测试"}],"api_key":"must-not-persist"}`
	router := gin.New()
	router.Use(WorkClassification())
	router.POST("/v1/chat/completions", func(c *gin.Context) {
		attribution, ok := service.UsageWorkAttributionFromContext(c.Request.Context())
		require.True(t, ok)
		require.Equal(t, service.WorkRelatedWork, attribution.WorkRelated)
		require.Equal(t, service.WorkCategoryCoding, attribution.Category)
		require.Equal(t, "local_rule", attribution.ClassificationSource)
		preserved, err := io.ReadAll(c.Request.Body)
		require.NoError(t, err)
		require.Equal(t, body, string(preserved))
		c.Status(http.StatusNoContent)
	})

	request := httptest.NewRequest(http.MethodPost, "/v1/chat/completions", strings.NewReader(body))
	request.Header.Set("Content-Type", "application/json")
	response := httptest.NewRecorder()
	router.ServeHTTP(response, request)
	require.Equal(t, http.StatusNoContent, response.Code)
}

func TestExtractTransientWorkTextUsesAllowlistedFields(t *testing.T) {
	text := extractTransientWorkText([]byte(`{
		"model":"debug-model-must-not-count",
		"authorization":"Bearer secret",
		"input":[{"content":"write technical documentation"}],
		"image_url":{"data":"private-image"}
	}`))
	require.Contains(t, text, "write technical documentation")
	require.NotContains(t, text, "debug-model")
	require.NotContains(t, text, "Bearer secret")
	require.NotContains(t, text, "private-image")
}

func TestExtractTransientWorkTextIgnoresNonHumanConversationHistory(t *testing.T) {
	text := extractTransientWorkText([]byte(`{
		"instructions":"debug source code and unit test",
		"messages":[
			{"role":"system","content":"personal entertainment movie recommendation"},
			{"role":"developer","content":"production deployment with kubernetes"},
			{"role":"assistant","content":"write documentation"},
			{"role":"tool","content":"sql query dashboard"},
			{"content":"production deployment without a role"},
			{"role":"user","content":"write technical documentation"}
		]
	}`))
	require.Equal(t, "write technical documentation", text)
}

func TestExtractTransientWorkTextSupportsGeminiContentsParts(t *testing.T) {
	text := extractTransientWorkText([]byte(`{
		"systemInstruction":{"parts":[{"text":"personal entertainment movie recommendation"}]},
		"contents":[
			{"role":"model","parts":[{"text":"write technical documentation"}]},
			{"role":"user","parts":[
				{"text":"请修复编译错误并补充单元测试"},
				{"inline_data":{"mime_type":"image/png","data":"private-image-data"}}
			]},
			{"parts":[{"text":"重构后端接口"}]}
		]
	}`))

	require.Equal(t, "请修复编译错误并补充单元测试 重构后端接口", text)
	require.NotContains(t, text, "personal entertainment")
	require.NotContains(t, text, "write technical documentation")
	require.NotContains(t, text, "private-image-data")
}

func TestWorkClassificationSupportsGeminiContentsAndPreservesBody(t *testing.T) {
	gin.SetMode(gin.TestMode)
	body := `{"contents":[{"role":"model","parts":[{"text":"write documentation"}]},{"role":"user","parts":[{"text":"请修复编译错误并补充单元测试"}]}]}`
	router := gin.New()
	router.Use(WorkClassification())
	router.POST("/v1beta/models/gemini-2.5-pro:generateContent", func(c *gin.Context) {
		attribution, ok := service.UsageWorkAttributionFromContext(c.Request.Context())
		require.True(t, ok)
		require.Equal(t, service.WorkRelatedWork, attribution.WorkRelated)
		require.Equal(t, service.WorkCategoryCoding, attribution.Category)

		preserved, err := io.ReadAll(c.Request.Body)
		require.NoError(t, err)
		require.Equal(t, body, string(preserved))
		c.Status(http.StatusNoContent)
	})

	request := httptest.NewRequest(http.MethodPost, "/v1beta/models/gemini-2.5-pro:generateContent", strings.NewReader(body))
	request.Header.Set("Content-Type", "application/json; charset=utf-8")
	response := httptest.NewRecorder()
	router.ServeHTTP(response, request)
	require.Equal(t, http.StatusNoContent, response.Code)
}

func TestExtractTransientWorkTextUsesStableMapOrder(t *testing.T) {
	body := []byte(`{"prompt":{"z":{"text":"second"},"a":{"text":"first"}}}`)
	for range 20 {
		require.Equal(t, "first second", extractTransientWorkText(body))
	}
}

func TestAppendUTF8LimitedNeverSplitsRune(t *testing.T) {
	var builder strings.Builder
	appendUTF8Limited(&builder, strings.Repeat("智", maxWorkClassificationTextBytes), maxWorkClassificationTextBytes)
	require.LessOrEqual(t, builder.Len(), maxWorkClassificationTextBytes)
	require.True(t, utf8.ValidString(builder.String()))
	require.NotContains(t, builder.String(), "�")
}

func TestReadTransientWorkTextForwardsBodyClose(t *testing.T) {
	body := &workClassificationTrackingBody{Reader: bytes.NewBufferString(`{"prompt":"write documentation"}`)}
	request := httptest.NewRequest(http.MethodPost, "/v1/responses", nil)
	request.Body = body
	require.Equal(t, "write documentation", readTransientWorkText(request))
	require.NoError(t, request.Body.Close())
	require.True(t, body.closed)
}

func TestWorkClassificationSkipsPanelRoutes(t *testing.T) {
	router := gin.New()
	router.Use(WorkClassification())
	router.POST("/api/v1/payment/orders", func(c *gin.Context) {
		_, ok := service.UsageWorkAttributionFromContext(c.Request.Context())
		require.False(t, ok)
		c.Status(http.StatusNoContent)
	})

	request := httptest.NewRequest(http.MethodPost, "/api/v1/payment/orders", strings.NewReader(`{"prompt":"code"}`))
	response := httptest.NewRecorder()
	router.ServeHTTP(response, request)
	require.Equal(t, http.StatusNoContent, response.Code)
}
