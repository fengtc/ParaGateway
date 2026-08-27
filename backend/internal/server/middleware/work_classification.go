package middleware

import (
	"bytes"
	"encoding/json"
	"io"
	"mime"
	"net/http"
	"sort"
	"strings"
	"unicode/utf8"

	"github.com/Wei-Shaw/sub2api/internal/pkg/workclassifier"
	"github.com/Wei-Shaw/sub2api/internal/service"
	"github.com/gin-gonic/gin"
)

const (
	maxWorkClassificationBodyBytes = 64 * 1024
	maxWorkClassificationTextBytes = 16 * 1024
)

var workTopLevelTextKeys = map[string]struct{}{
	"contents": {}, "input": {}, "message": {}, "messages": {}, "prompt": {}, "query": {},
}

var workNestedTextKeys = map[string]struct{}{
	"content": {}, "input": {}, "message": {}, "parts": {}, "text": {},
}

var workTextDeniedKeys = map[string]struct{}{
	"audio": {}, "authorization": {}, "data": {}, "file": {}, "image": {},
	"image_url": {}, "password": {}, "secret": {}, "token": {},
}

// WorkClassification performs a bounded, local-only classification of gateway
// requests. Only its structured result is attached to context. Request text is
// discarded before the next middleware runs and is never logged or persisted.
func WorkClassification() gin.HandlerFunc {
	return func(c *gin.Context) {
		if c == nil {
			return
		}
		if c.Request == nil || c.Request.URL == nil || !isWorkClassificationPath(c.Request.URL.Path) {
			c.Next()
			return
		}

		input := workclassifier.InputFromHeaders(c.Request.Header)
		if shouldInspectWorkClassificationBody(c.Request) {
			input.TransientText = readTransientWorkText(c.Request)
		}
		result := workclassifier.Classify(input)
		attribution := service.UsageWorkAttribution{
			ProjectRef:           input.Project,
			RepositoryRef:        input.Repository,
			SubmissionType:       input.SubmissionType,
			WorkRelated:          string(result.WorkRelated),
			Category:             string(result.Category),
			Confidence:           result.Confidence,
			ClassificationSource: result.ClassificationSource,
			ClassifierVersion:    result.ClassifierVersion,
		}
		c.Request = c.Request.WithContext(service.WithUsageWorkAttribution(c.Request.Context(), attribution))
		c.Next()
	}
}

func isWorkClassificationPath(path string) bool {
	for _, prefix := range []string{
		"/v1/", "/v1beta/", "/backend-api/codex/", "/antigravity/", "/copilot/",
		"/responses", "/alpha/search", "/messages", "/chat/completions", "/embeddings",
		"/images/", "/videos", "/tts", "/stt", "/custom-voices", "/realtime",
		"/web_search", "/x_search",
	} {
		if strings.HasPrefix(path, prefix) {
			return true
		}
	}
	return false
}

func shouldInspectWorkClassificationBody(request *http.Request) bool {
	if request == nil || request.Body == nil {
		return false
	}
	switch request.Method {
	case http.MethodPost, http.MethodPut, http.MethodPatch:
	default:
		return false
	}
	mediaType, _, err := mime.ParseMediaType(request.Header.Get("Content-Type"))
	if err != nil || mediaType == "" {
		return true
	}
	return mediaType == "application/json" || strings.HasSuffix(mediaType, "+json")
}

func readTransientWorkText(request *http.Request) string {
	if request == nil || request.Body == nil {
		return ""
	}
	originalBody := request.Body
	prefix, err := io.ReadAll(io.LimitReader(originalBody, maxWorkClassificationBodyBytes+1))
	request.Body = &workReplayReadCloser{
		Reader: io.MultiReader(bytes.NewReader(prefix), originalBody),
		Closer: originalBody,
	}
	if err != nil || len(prefix) == 0 || len(prefix) > maxWorkClassificationBodyBytes {
		return ""
	}
	return extractTransientWorkText(prefix)
}

func extractTransientWorkText(body []byte) string {
	var value any
	if len(body) == 0 || json.Unmarshal(body, &value) != nil {
		return ""
	}
	var builder strings.Builder
	appendTransientWorkText(&builder, value, false, true)
	return builder.String()
}

type workReplayReadCloser struct {
	io.Reader
	io.Closer
}

func appendTransientWorkText(builder *strings.Builder, value any, enabled, topLevel bool) {
	if builder == nil || builder.Len() >= maxWorkClassificationTextBytes {
		return
	}
	switch typed := value.(type) {
	case map[string]any:
		if rawRole, hasRole := typed["role"]; hasRole {
			role, _ := rawRole.(string)
			switch strings.ToLower(strings.TrimSpace(role)) {
			case "user", "human":
				enabled = true
				topLevel = false
			default:
				return
			}
		}
		keys := make([]string, 0, len(typed))
		for key := range typed {
			keys = append(keys, key)
		}
		sort.Strings(keys)
		for _, key := range keys {
			child := typed[key]
			normalizedKey := strings.ToLower(strings.TrimSpace(key))
			if normalizedKey == "role" || normalizedKey == "instructions" || normalizedKey == "system" || normalizedKey == "developer" {
				continue
			}
			if _, denied := workTextDeniedKeys[normalizedKey]; denied {
				continue
			}
			_, topAllowed := workTopLevelTextKeys[normalizedKey]
			_, nestedAllowed := workNestedTextKeys[normalizedKey]
			// Providers may add structural wrappers inside an allowlisted prompt
			// field. Traverse those containers, while still collecting strings
			// only from allowlisted text keys and rejecting denied keys above.
			_, childIsMap := child.(map[string]any)
			_, childIsSlice := child.([]any)
			childEnabled := enabled && (nestedAllowed || childIsMap || childIsSlice)
			if topLevel && topAllowed {
				childEnabled = true
			}
			if normalizedKey == "messages" {
				appendTransientUserMessages(builder, child)
				continue
			}
			if normalizedKey == "input" {
				appendTransientWorkText(builder, child, childEnabled, false)
				continue
			}
			if childEnabled {
				appendTransientWorkText(builder, child, true, false)
			}
		}
	case []any:
		for _, child := range typed {
			appendTransientWorkText(builder, child, enabled, false)
		}
	case string:
		if !enabled || strings.TrimSpace(typed) == "" {
			return
		}
		appendUTF8Limited(builder, typed, maxWorkClassificationTextBytes)
	}
}

func appendTransientUserMessages(builder *strings.Builder, value any) {
	messages, ok := value.([]any)
	if !ok {
		return
	}
	for _, item := range messages {
		message, ok := item.(map[string]any)
		if !ok {
			continue
		}
		role, _ := message["role"].(string)
		switch strings.ToLower(strings.TrimSpace(role)) {
		case "user", "human":
			appendTransientWorkText(builder, message, false, false)
		}
	}
}

func appendUTF8Limited(builder *strings.Builder, value string, limit int) {
	if builder == nil || limit <= 0 || builder.Len() >= limit || !utf8.ValidString(value) {
		return
	}
	if builder.Len() > 0 {
		if builder.Len()+1 >= limit {
			return
		}
		builder.WriteByte(' ')
	}
	for _, r := range value {
		runeLen := utf8.RuneLen(r)
		if runeLen <= 0 || builder.Len()+runeLen > limit {
			return
		}
		builder.WriteRune(r)
	}
}
