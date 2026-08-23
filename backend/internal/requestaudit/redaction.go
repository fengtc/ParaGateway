package requestaudit

import (
	"encoding/json"
	"strings"

	"github.com/Wei-Shaw/sub2api/internal/service"
)

const maxPreviewBytes = 16 * 1024

func textualContent(contentType string, body []byte) bool {
	contentType = strings.ToLower(strings.TrimSpace(strings.Split(contentType, ";")[0]))
	if contentType == "" {
		return json.Valid(body)
	}
	return strings.HasPrefix(contentType, "text/") || strings.Contains(contentType, "json") ||
		strings.Contains(contentType, "xml") || strings.Contains(contentType, "event-stream") ||
		strings.Contains(contentType, "x-ndjson") || strings.Contains(contentType, "graphql")
}

func buildPreview(body []byte, contentType, level string) string {
	if len(body) == 0 {
		return ""
	}
	if !textualContent(contentType, body) {
		return "<binary or multipart content omitted>"
	}
	if level == RedactionStrict {
		return "<content hidden by strict redaction>"
	}
	return truncateUTF8(service.RedactAuditBody(body, contentType), maxPreviewBytes)
}

func extractModelAndStream(body []byte) (string, bool) {
	if !json.Valid(body) {
		return "", false
	}
	var root map[string]any
	if err := json.Unmarshal(body, &root); err != nil {
		return "", false
	}
	model, _ := root["model"].(string)
	stream, _ := root["stream"].(bool)
	return strings.TrimSpace(model), stream
}
