package requestaudit

import (
	"bytes"
	"hash/fnv"
	"io"
	"net/http"
	"strconv"
	"strings"
	"time"
	"unicode/utf8"

	"github.com/Wei-Shaw/sub2api/internal/pkg/ctxkey"
	"github.com/gin-gonic/gin"
)

type IdentityResolver func(*gin.Context) Identity

// Middleware captures only authenticated gateway traffic. It is deliberately
// fail-open: archive failures must not affect the proxied model response.
func (s *Service) Middleware(resolve IdentityResolver) gin.HandlerFunc {
	return func(c *gin.Context) {
		policy := s.GetPolicy()
		if !policy.Enabled {
			c.Next()
			return
		}
		if !gatewayCandidatePath(c.Request.URL.Path) {
			c.Next()
			return
		}
		requestID, _ := c.Request.Context().Value(ctxkey.RequestID).(string)
		if policy.CaptureMode == CaptureModeSample && !sampleSelected(policy.SampleRate, requestID, Identity{}, c.Request.URL.Path) {
			c.Next()
			return
		}

		requestBody, requestBytes, requestTruncated, captureErr := captureRequestBody(c.Request, policy.MaxBodyBytes)
		writer := &captureResponseWriter{ResponseWriter: c.Writer, limit: policy.MaxBodyBytes}
		c.Writer = writer
		startedAt := time.Now()
		c.Next()
		requestID, _ = c.Request.Context().Value(ctxkey.RequestID).(string)

		identity := Identity{}
		if resolve != nil {
			identity = resolve(c)
		}
		if identity.APIKeyID <= 0 {
			return
		}
		if policy.CaptureMode == CaptureModeErrors && c.Writer.Status() < http.StatusBadRequest {
			return
		}
		model, requestStream := extractModelAndStream(requestBody)
		responseContentType := truncateUTF8(c.Writer.Header().Get("Content-Type"), 255)
		isStream := requestStream || strings.Contains(strings.ToLower(responseContentType), "event-stream")
		captureReason := policy.CaptureMode
		if captureReason == CaptureModeErrors {
			captureReason = "error"
		}

		item := &Record{
			RequestID:          truncateUTF8(requestID, 128),
			UsernameSnapshot:   truncateUTF8(identity.Username, 255),
			UserEmailSnapshot:  truncateUTF8(identity.UserEmail, 320),
			APIKeyNameSnapshot: truncateUTF8(identity.APIKeyName, 255),
			GroupID:            cloneID(identity.GroupID), GroupNameSnapshot: truncateUTF8(identity.GroupName, 255),
			Method: truncateUTF8(c.Request.Method, 16), Endpoint: truncateUTF8(c.Request.URL.Path, 512),
			Model: truncateUTF8(model, 255), ClientIP: truncateUTF8(identity.ClientIP, 64),
			StatusCode: c.Writer.Status(), LatencyMS: time.Since(startedAt).Milliseconds(),
			IsStream: isStream, CaptureReason: captureReason, PolicyVersion: policy.Version,
			RequestContentType:  truncateUTF8(c.GetHeader("Content-Type"), 255),
			ResponseContentType: responseContentType,
			RequestBytes:        requestBytes, ResponseBytes: writer.total,
			RequestTruncated: requestTruncated, ResponseTruncated: writer.truncated,
			CreatedAt: time.Now().UTC(),
		}
		if identity.UserID > 0 {
			id := identity.UserID
			item.UserID = &id
		}
		if identity.APIKeyID > 0 {
			id := identity.APIKeyID
			item.APIKeyID = &id
		}
		if captureErr != "" {
			item.ContentError = captureErr
		}
		s.Enqueue(item, requestBody, writer.body.Bytes(), policy)
	}
}

func captureRequestBody(request *http.Request, limit int) ([]byte, int64, bool, string) {
	if request == nil || request.Body == nil || limit <= 0 {
		return nil, 0, false, ""
	}
	original := request.Body
	read, err := io.ReadAll(io.LimitReader(original, int64(limit)+1))
	request.Body = &restoredRequestBody{Reader: io.MultiReader(bytes.NewReader(read), original), closer: original}
	total := int64(len(read))
	if request.ContentLength > total {
		total = request.ContentLength
	}
	truncated := len(read) > limit || total > int64(limit)
	if len(read) > limit {
		read = read[:limit]
	}
	if err != nil {
		return read, total, truncated, "request body capture failed"
	}
	return read, total, truncated, ""
}

type restoredRequestBody struct {
	io.Reader
	closer io.Closer
}

func (b *restoredRequestBody) Close() error { return b.closer.Close() }

type captureResponseWriter struct {
	gin.ResponseWriter
	body      bytes.Buffer
	limit     int
	total     int64
	truncated bool
}

func (w *captureResponseWriter) Write(value []byte) (int, error) {
	n, err := w.ResponseWriter.Write(value)
	w.capture(value[:n])
	return n, err
}

func (w *captureResponseWriter) WriteString(value string) (int, error) {
	n, err := w.ResponseWriter.WriteString(value)
	w.capture([]byte(value[:n]))
	return n, err
}

func (w *captureResponseWriter) capture(value []byte) {
	w.total += int64(len(value))
	remaining := w.limit - w.body.Len()
	if remaining > 0 {
		if len(value) > remaining {
			_, _ = w.body.Write(value[:remaining])
		} else {
			_, _ = w.body.Write(value)
		}
	}
	if w.total > int64(w.limit) {
		w.truncated = true
	}
}
func gatewayCandidatePath(path string) bool {
	for _, prefix := range []string{
		"/v1", "/v1beta", "/responses", "/alpha/search", "/models",
		"/messages/count_tokens", "/backend-api/codex", "/chat/completions",
		"/embeddings", "/images", "/videos", "/tts", "/stt", "/custom-voices",
		"/realtime", "/web_search", "/x_search", "/antigravity",
	} {
		if path == prefix || strings.HasPrefix(path, prefix+"/") {
			return true
		}
	}
	return false
}

func sampleSelected(rate float64, requestID string, identity Identity, path string) bool {
	if rate >= 100 {
		return true
	}
	if rate <= 0 {
		return false
	}
	hash := fnv.New32a()
	_, _ = hash.Write([]byte(requestID))
	_, _ = hash.Write([]byte("|" + strconv.FormatInt(identity.UserID, 10) + "|" + path))
	return float64(hash.Sum32()%10000) < rate*100
}

func cloneID(value *int64) *int64 {
	if value == nil {
		return nil
	}
	copy := *value
	return &copy
}

func truncateUTF8(value string, max int) string {
	value = strings.TrimSpace(value)
	if len(value) <= max {
		return value
	}
	cut := max
	for cut > 0 && !utf8.ValidString(value[:cut]) {
		cut--
	}
	return value[:cut]
}
