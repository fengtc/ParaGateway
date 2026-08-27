package workclassifier

import (
	"net/http"
	"regexp"
	"strings"
	"unicode"
	"unicode/utf8"

	"github.com/Wei-Shaw/sub2api/internal/util/logredact"
)

const (
	HeaderProject        = "X-Para-Project"
	HeaderRepository     = "X-Para-Repository"
	HeaderSubmissionType = "X-Para-Submission-Type"

	MaxMetadataRunes          = 160
	maxProjectRunes           = 100
	maxClassifierVersionRunes = 64
	maxTransientTextRunes     = 16 * 1024
)

var (
	standaloneCredentialPatterns = []*regexp.Regexp{
		regexp.MustCompile(`(?i)\b(?:bearer|basic)\s+[a-z0-9._~+/=-]{12,}`),
		regexp.MustCompile(`(?i)\b(?:sk|rk|pk)-[a-z0-9_-]{16,}\b`),
		regexp.MustCompile(`(?i)\bgh[pousr]_[a-z0-9]{20,}\b`),
		regexp.MustCompile(`(?i)\bgithub_pat_[a-z0-9_]{20,}\b`),
		regexp.MustCompile(`\bAKIA[0-9A-Z]{16}\b`),
		regexp.MustCompile(`\beyJ[a-zA-Z0-9_-]{10,}\.[a-zA-Z0-9_-]{10,}\.[a-zA-Z0-9_-]{10,}\b`),
	}
	metadataCredentialPatterns = []*regexp.Regexp{
		regexp.MustCompile(`(?i)(?:bearer|basic)\s+[a-z0-9._~+/=-]{12,}`),
		regexp.MustCompile(`(?i)(?:sk|rk|pk)-[a-z0-9_-]{16,}`),
		regexp.MustCompile(`(?i)gh[pousr]_[a-z0-9]{20,}`),
		regexp.MustCompile(`(?i)github_pat_[a-z0-9_]{20,}`),
		regexp.MustCompile(`AKIA[0-9A-Z]{16}`),
		regexp.MustCompile(`eyJ[a-zA-Z0-9_-]{10,}\.[a-zA-Z0-9_-]{10,}\.[a-zA-Z0-9_-]{10,}`),
		regexp.MustCompile(`AIza[0-9A-Za-z_-]{30,}`),
		regexp.MustCompile(`(?i)xox[baprs]-[a-z0-9-]{16,}`),
	}
	urlUserInfoPattern     = regexp.MustCompile(`(?i)(?:https?|ssh|git)://[^/@\s]+:[^/@\s]+@`)
	credentialLabelPattern = regexp.MustCompile(
		`(?i)(?:api[_-]?key|apikey|secret|token|authorization|private[_-]?key|password|passwd|client[_-]?secret)\s*[:=]`,
	)
	opaqueTokenPattern  = regexp.MustCompile(`[A-Za-z0-9]{32,}`)
	promptPrefixPattern = regexp.MustCompile(
		`(?i)^(?:please|help|write|create|explain|review|fix|translate|summarize|generate|tell|show|how|why|what|can|could|would|package|import|func|function|class|select|insert|update|delete)\b`,
	)
)

// InputFromHeaders parses only the canonical project, repository, and
// submission-type headers. Client-declared classifications, prompt text,
// arbitrary headers, authorization values, and request bodies are never read.
func InputFromHeaders(headers http.Header) Input {
	if headers == nil {
		return Input{}
	}
	input := Input{
		Project:        CleanProjectRef(headers.Get(HeaderProject)),
		Repository:     CleanRepositoryRef(headers.Get(HeaderRepository)),
		SubmissionType: NormalizeSubmissionType(headers.Get(HeaderSubmissionType)),
	}
	return input
}

// CleanMetadataField accepts only a short, structured identifier. Unsafe input
// is discarded in full rather than redacted and partially retained.
func CleanMetadataField(raw string) string {
	return cleanStructuredIdentifier(raw, MaxMetadataRunes, false)
}

// CleanProjectRef accepts a stable project slug/identifier. Free-form names
// with spaces are rejected so prompt fragments cannot be mistaken for metadata.
func CleanProjectRef(raw string) string {
	return cleanStructuredIdentifier(raw, maxProjectRunes, false)
}

// CleanRepositoryRef accepts repo or owner/repo identifiers only.
func CleanRepositoryRef(raw string) string {
	cleaned := cleanStructuredIdentifier(raw, MaxMetadataRunes, true)
	if cleaned == "" || strings.Count(cleaned, "/") > 1 {
		return ""
	}
	for _, segment := range strings.Split(cleaned, "/") {
		if segment == "" || segment == "." || segment == ".." {
			return ""
		}
	}
	return cleaned
}

// CleanClassifierVersion accepts only a short structured rule/model version.
// Oversized or unsafe values are discarded in full instead of truncated.
func CleanClassifierVersion(raw string) string {
	return cleanStructuredIdentifier(raw, maxClassifierVersionRunes, false)
}

func cleanStructuredIdentifier(raw string, maxRunes int, allowSlash bool) string {
	if raw == "" || !utf8.ValidString(raw) || containsUnsafeMetadata(raw) {
		return ""
	}
	for _, r := range raw {
		if unicode.IsControl(r) {
			return ""
		}
	}

	cleaned := strings.TrimSpace(raw)
	if cleaned == "" || utf8.RuneCountInString(cleaned) > maxRunes {
		return ""
	}
	if looksLikeFreeText(cleaned) {
		return ""
	}
	hasIdentifierCharacter := false
	for _, r := range cleaned {
		switch {
		case r >= 'A' && r <= 'Z', r >= 'a' && r <= 'z', r >= '0' && r <= '9', r >= '一' && r <= '龥':
			hasIdentifierCharacter = true
		case r == '-' || r == '_' || r == '.':
		case allowSlash && r == '/':
		default:
			return ""
		}
	}
	if !hasIdentifierCharacter {
		return ""
	}
	return cleaned
}

func containsUnsafeMetadata(raw string) bool {
	if urlUserInfoPattern.MatchString(raw) || credentialLabelPattern.MatchString(raw) ||
		opaqueTokenPattern.MatchString(raw) {
		return true
	}
	for _, pattern := range metadataCredentialPatterns {
		if pattern.MatchString(raw) {
			return true
		}
	}
	return false
}

func looksLikeFreeText(value string) bool {
	if promptPrefixPattern.MatchString(strings.TrimSpace(value)) {
		return true
	}
	for _, prefix := range []string{
		"请", "请问", "帮我", "帮忙", "如何", "怎么", "为什么", "能否", "可否", "给我", "以下", "这段",
	} {
		if strings.HasPrefix(value, prefix) {
			return true
		}
	}
	return strings.Contains(value, "完整源代码")
}

// NormalizeSubmissionType maps aliases to the canonical persisted values.
func NormalizeSubmissionType(raw string) string {
	value := strings.ToLower(strings.TrimSpace(raw))
	value = strings.NewReplacer("-", "_", " ", "_").Replace(value)
	for strings.Contains(value, "__") {
		value = strings.ReplaceAll(value, "__", "_")
	}
	switch value {
	case "code", "coding":
		return "coding"
	case "document", "documentation", "docs":
		return "documentation"
	case "analysis", "data_analysis":
		return "data_analysis"
	case "personal", "non_work":
		return "non_work"
	case "commit", "pull_request", "merge_request",
		"operations", "deployment", "incident", "communication", "meeting",
		"learning", "training", "other":
		return value
	default:
		return ""
	}
}

func redactStandaloneCredentials(value string) string {
	for _, pattern := range standaloneCredentialPatterns {
		value = pattern.ReplaceAllString(value, "[REDACTED]")
	}
	return value
}

func normalizeTransientText(value string) string {
	if value == "" || !utf8.ValidString(value) {
		return ""
	}
	value = truncateRunes(value, maxTransientTextRunes)
	value = logredact.RedactText(value,
		"api_key", "apikey", "secret", "token", "authorization", "private_key",
	)
	value = redactStandaloneCredentials(value)
	return normalizeForMatching(value)
}

func truncateRunes(value string, limit int) string {
	if limit <= 0 {
		return ""
	}
	runes := []rune(value)
	if len(runes) <= limit {
		return value
	}
	return string(runes[:limit])
}
