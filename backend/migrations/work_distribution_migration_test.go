package migrations

import (
	"regexp"
	"strings"
	"testing"
	"unicode/utf8"

	"github.com/stretchr/testify/require"
)

func TestWorkDistributionMigrationUsesStructuredMetadataOnly(t *testing.T) {
	content, err := FS.ReadFile("236_work_distribution.sql")
	require.NoError(t, err)
	lines := strings.Split(string(content), "\n")
	sqlLines := make([]string, 0, len(lines))
	for _, line := range lines {
		if !strings.HasPrefix(strings.TrimSpace(line), "--") {
			sqlLines = append(sqlLines, line)
		}
	}
	normalized := strings.ToLower(strings.Join(strings.Fields(strings.Join(sqlLines, "\n")), " "))
	for _, required := range []string{
		"create table if not exists usage_work_metadata",
		"create table if not exists usage_work_classifications",
		"create table if not exists usage_work_reviews",
		"usage_log_id bigint primary key references usage_logs(id)",
		"user_id bigint not null references users(id)",
		"weight bigint not null",
		"classification_source",
		"repository_ref",
		"submission_type",
		"idx_usage_work_reviews_one_pending",
		"work_attribution jsonb",
		"batch_image_jobs_work_attribution_check",
		"usage_work_classifications_combination_check",
		"usage_work_reviews_previous_combination_check",
		"usage_work_reviews_decision_note_check",
		"usage_work_metadata_department_label_check",
		"usage_work_metadata_role_label_check",
		"idx_usage_work_reviews_requested_by",
		"idx_usage_work_reviews_resolved_by",
		"'job_role', '岗位角色'",
		"where key = 'job_role' and deleted_at is null",
	} {
		require.Contains(t, normalized, required)
	}
	for _, forbidden := range []string{"prompt", "request_body", "response_body", "source_code", "api_key", "credential", "secret"} {
		require.NotContains(t, normalized, forbidden)
	}
	require.NotContains(t, normalized, "'code', 'coding'")
	require.NotContains(t, normalized, "'document', 'documentation'")
	require.NotContains(t, normalized, "'analysis', 'data_analysis'")
	require.NotContains(t, normalized, "'personal', 'non_work'")
}

func TestWorkDistributionMigrationConstrainsDimensionSnapshots(t *testing.T) {
	content, err := FS.ReadFile("236_work_distribution.sql")
	require.NoError(t, err)
	sql := string(content)

	for _, required := range []string{
		"department IS NULL",
		"department = 'unknown'",
		"department = BTRIM(department)",
		"CHAR_LENGTH(department) BETWEEN 1 AND 100",
		"department !~ '[[:cntrl:]]'",
		"department ~ '^[A-Za-z0-9一-龥 _.#·•（）()、/&+-]+$'",
		"department ~ '[A-Za-z0-9一-龥]'",
		"role IS NULL",
		"role = 'unknown'",
		"role = BTRIM(role)",
		"CHAR_LENGTH(role) BETWEEN 1 AND 50",
		"role !~ '[[:cntrl:]]'",
		"role ~ '^[A-Za-z0-9一-龥 _.#·•（）()、/&+-]+$'",
		"role ~ '[A-Za-z0-9一-龥]'",
		"!~* '(bearer|basic)[[:space:]]+[A-Za-z0-9._~+/=-]{12,}'",
		"!~* '(sk|rk|pk)-[A-Za-z0-9_-]{16,}'",
		"!~* 'gh[pousr]_[A-Za-z0-9]{20,}'",
		"!~* 'github_pat_[A-Za-z0-9_]{20,}'",
		"!~ 'AKIA[0-9A-Z]{16}'",
		"!~ 'eyJ[A-Za-z0-9_-]{10,}[.][A-Za-z0-9_-]{10,}[.][A-Za-z0-9_-]{10,}'",
		"!~ '[A-Za-z0-9_-]{24,}'",
		"POSITION('完整源代码' IN department) = 0",
		"POSITION('完整源代码' IN role) = 0",
	} {
		require.Contains(t, sql, required)
	}
	require.Equal(t, 2, strings.Count(sql, "^(please|help|write|create|explain|review|fix|translate|summarize|generate|tell|show|how|why|what|can|could|would|package|import|func|function|class|select|insert|update|delete)"))
	require.Equal(t, 2, strings.Count(sql, "^(请|请问|帮我|帮忙|如何|怎么|为什么|能否|可否|给我|以下|这段)"))
}

func TestWorkDistributionDimensionLabelContractExamples(t *testing.T) {
	for _, value := range []string{
		"unknown",
		"研发中心",
		"高级软件工程师",
		"Platform Engineering",
		".NET 开发",
		"C++工程师",
		"R&D",
	} {
		require.Truef(t, workDimensionLabelContract(value, 100), "expected a safe business label: %q", value)
		require.Truef(t, workDimensionLabelContract(value, 50), "expected a safe role label: %q", value)
	}

	unsafe := []string{
		"",
		" 研发中心 ",
		"研发\n中心",
		"研发🚀中心",
		"---",
		`{"department":"研发"}`,
		"func main()",
		"please review private code",
		"请帮我修改代码",
		"研发完整源代码中心",
		"Bearer " + "AbCdEfGhIjKlMn",
		"Basic " + "AbCdEfGhIjKlMn",
		"sk-" + "1234567890abcdefghijklmnop",
		"ghp_" + strings.Repeat("1", 20),
		"github_" + "pat_" + strings.Repeat("2", 20),
		"AKIA" + "1234567890ABCDEF",
		"eyJabcdefghij" + "." + "abcdefghijk" + "." + "abcdefghijk",
		"abcdefghijklmnopqrstuvwx",
		strings.Repeat("智", 101),
	}
	for _, value := range unsafe {
		require.Falsef(t, workDimensionLabelContract(value, 100), "expected an unsafe label to be rejected: %q", value)
	}
	require.False(t, workDimensionLabelContract(strings.Repeat("岗", 51), 50))
}

var (
	workDimensionEnglishPrefix = regexp.MustCompile(`(?i)^(please|help|write|create|explain|review|fix|translate|summarize|generate|tell|show|how|why|what|can|could|would|package|import|func|function|class|select|insert|update|delete)\b`)
	workDimensionBearer        = regexp.MustCompile(`(?i)(bearer|basic)\s+[A-Za-z0-9._~+/=-]{12,}`)
	workDimensionAPIKey        = regexp.MustCompile(`(?i)(sk|rk|pk)-[A-Za-z0-9_-]{16,}`)
	workDimensionGitHubKey     = regexp.MustCompile(`(?i)(gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,})`)
	workDimensionAWSKey        = regexp.MustCompile(`AKIA[0-9A-Z]{16}`)
	workDimensionJWT           = regexp.MustCompile(`eyJ[A-Za-z0-9_-]{10,}[.][A-Za-z0-9_-]{10,}[.][A-Za-z0-9_-]{10,}`)
	workDimensionLongToken     = regexp.MustCompile(`[A-Za-z0-9_-]{24,}`)
)

func workDimensionLabelContract(value string, maxRunes int) bool {
	if value == "unknown" {
		return true
	}
	if value == "" || value != strings.TrimSpace(value) || !utf8.ValidString(value) || utf8.RuneCountInString(value) > maxRunes {
		return false
	}
	containsBusinessCharacter := false
	for _, r := range value {
		switch {
		case r >= 'A' && r <= 'Z', r >= 'a' && r <= 'z', r >= '0' && r <= '9', r >= '一' && r <= '龥':
			containsBusinessCharacter = true
		case strings.ContainsRune(" _.#·•（）()、/&+-", r):
		default:
			return false
		}
	}
	if !containsBusinessCharacter || workDimensionEnglishPrefix.MatchString(value) {
		return false
	}
	for _, prefix := range []string{"请", "请问", "帮我", "帮忙", "如何", "怎么", "为什么", "能否", "可否", "给我", "以下", "这段"} {
		if strings.HasPrefix(value, prefix) {
			return false
		}
	}
	if strings.Contains(value, "完整源代码") {
		return false
	}
	for _, pattern := range []*regexp.Regexp{
		workDimensionBearer,
		workDimensionAPIKey,
		workDimensionGitHubKey,
		workDimensionAWSKey,
		workDimensionJWT,
		workDimensionLongToken,
	} {
		if pattern.MatchString(value) {
			return false
		}
	}
	return true
}
