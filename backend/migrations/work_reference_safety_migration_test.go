package migrations

import (
	"regexp"
	"strings"
	"testing"
	"unicode/utf8"

	"github.com/stretchr/testify/require"
)

func TestWorkReferenceSafetyMigrationAddsDefenseInDepthChecks(t *testing.T) {
	content, err := FS.ReadFile("237_work_reference_safety.sql")
	require.NoError(t, err)
	sql := string(content)

	for _, required := range []string{
		"CREATE OR REPLACE FUNCTION public.paragateway_is_safe_work_reference",
		"CREATE OR REPLACE FUNCTION public.paragateway_is_safe_work_attribution",
		"SET search_path = pg_catalog",
		"UPDATE usage_work_metadata",
		"SET project_ref = NULL",
		"SET repository_ref = NULL",
		"UPDATE batch_image_jobs",
		"work_attribution = work_attribution - 'project_ref'",
		"work_attribution = work_attribution - 'repository_ref'",
		"usage_work_metadata_project_ref_check",
		"usage_work_metadata_repository_ref_check",
		"batch_image_jobs_work_reference_safety_check",
		"public.paragateway_is_safe_work_reference(project_ref, 100, false)",
		"public.paragateway_is_safe_work_reference(repository_ref, 160, true)",
		"public.paragateway_is_safe_work_attribution(work_attribution)",
		"SET work_attribution = NULL",
		"attribution ?& ARRAY[",
		"jsonb_typeof(attribution -> 'project_ref') = 'string'",
		"jsonb_typeof(attribution -> 'repository_ref') = 'string'",
		"jsonb_typeof(attribution -> 'submission_type') = 'string'",
		"jsonb_typeof(attribution -> 'work_related') = 'string'",
		"jsonb_typeof(attribution -> 'category') = 'string'",
		"jsonb_typeof(attribution -> 'confidence') = 'number'",
		"attribution -> 'confidence' >= '0'::jsonb",
		"attribution -> 'confidence' <= '1'::jsonb",
		"jsonb_typeof(attribution -> 'classification_source') = 'string'",
		"jsonb_typeof(attribution -> 'classifier_version') = 'string'",
		"reference_value !~ '[A-Za-z0-9]{32,}'",
		"reference_value !~ 'AIza[0-9A-Za-z_-]{30,}'",
		"reference_value !~* 'xox[baprs]-[a-z0-9-]{16,}'",
	} {
		require.Contains(t, sql, required)
	}

	require.NotContains(t, sql, "DROP CONSTRAINT")
	require.Equal(t, 3, strings.Count(sql, "ADD CONSTRAINT"))
}

func TestWorkReferenceSafetyContractExamples(t *testing.T) {
	shortIdentifier := strings.Repeat("a", 31)
	maxProject := segmentedWorkReference(100)
	maxRepository := "team/" + segmentedWorkReference(155)
	for _, tc := range []struct {
		name       string
		value      string
		maxRunes   int
		allowSlash bool
	}{
		{name: "short project", value: "ParaGateway-v2", maxRunes: 100},
		{name: "Chinese project", value: "客户智能分析平台", maxRunes: 100},
		{name: "segmented project", value: "customer.analytics-platform_v2", maxRunes: 100},
		{name: "31 character project", value: shortIdentifier, maxRunes: 100},
		{name: "owner repository", value: "fengtc/ParaGateway", maxRunes: 160, allowSlash: true},
		{name: "Chinese repository", value: "研发组/智能分析-service.v2", maxRunes: 160, allowSlash: true},
		{name: "31 character repository segment", value: "team/" + shortIdentifier, maxRunes: 160, allowSlash: true},
		{name: "100 character project", value: maxProject, maxRunes: 100},
		{name: "160 character repository", value: maxRepository, maxRunes: 160, allowSlash: true},
		{name: "segmented long project", value: segmentedWorkReference(64), maxRunes: 100},
	} {
		t.Run(tc.name, func(t *testing.T) {
			require.True(t, workReferenceSafetyContract(tc.value, tc.maxRunes, tc.allowSlash))
		})
	}

	opaqueToken := strings.Repeat("b", 32)
	unsafe := []struct {
		name       string
		value      string
		maxRunes   int
		allowSlash bool
	}{
		{name: "32 character project token", value: opaqueToken, maxRunes: 100},
		{name: "32 character repository token suffix", value: "team/" + opaqueToken, maxRunes: 160, allowSlash: true},
		{name: "32 character repository token prefix", value: opaqueToken + "/service", maxRunes: 160, allowSlash: true},
		{name: "nested repository", value: "org/team/repository", maxRunes: 160, allowSlash: true},
		{name: "empty repository segment", value: "team/", maxRunes: 160, allowSlash: true},
		{name: "relative repository segment", value: "team/..", maxRunes: 160, allowSlash: true},
		{name: "leading repository slash", value: "/repository", maxRunes: 160, allowSlash: true},
		{name: "leading current segment", value: "./repository", maxRunes: 160, allowSlash: true},
		{name: "leading parent segment", value: "../repository", maxRunes: 160, allowSlash: true},
		{name: "slash in project", value: "team/project", maxRunes: 100},
		{name: "punctuation only", value: "---", maxRunes: 100},
		{name: "surrounding whitespace", value: " project ", maxRunes: 100},
		{name: "control character", value: "team\nproject", maxRunes: 100},
		{name: "unsupported character", value: "team🚀project", maxRunes: 100},
		{name: "free text prompt", value: "please-review", maxRunes: 100},
		{name: "Chinese prompt", value: "请修复项目", maxRunes: 100},
		{name: "provider key", value: workReferenceTestCanary("s", "k-", strings.Repeat("c", 16)), maxRunes: 100},
		{name: "GitHub token", value: workReferenceTestCanary("github", "_pat_", strings.Repeat("d", 20)), maxRunes: 160, allowSlash: true},
		{name: "AWS key", value: workReferenceTestCanary("team_", "AK", "IA", "1234567890ABCDEF"), maxRunes: 100},
		{name: "JWT", value: workReferenceTestCanary("team_", "ey", "J", strings.Repeat("j", 10), ".", strings.Repeat("k", 11), ".", strings.Repeat("l", 11)), maxRunes: 100},
		{name: "Google key", value: workReferenceTestCanary("AI", "za", strings.Repeat("e-", 15)), maxRunes: 100},
		{name: "Slack token", value: workReferenceTestCanary("xo", "xb-", strings.Repeat("f", 16)), maxRunes: 100},
		{name: "project over limit", value: strings.Repeat("项", 101), maxRunes: 100},
		{name: "repository over limit", value: strings.Repeat("仓", 161), maxRunes: 160, allowSlash: true},
	}
	for _, tc := range unsafe {
		t.Run(tc.name, func(t *testing.T) {
			require.False(t, workReferenceSafetyContract(tc.value, tc.maxRunes, tc.allowSlash))
		})
	}
}

var (
	workReferenceEnglishPrefix = regexp.MustCompile(`(?i)^(please|help|write|create|explain|review|fix|translate|summarize|generate|tell|show|how|why|what|can|could|would|package|import|func|function|class|select|insert|update|delete)\b`)
	workReferenceBearer        = regexp.MustCompile(`(?i)(bearer|basic)\s+[A-Za-z0-9._~+/=-]{12,}`)
	workReferenceProviderKey   = regexp.MustCompile(`(?i)(sk|rk|pk)-[A-Za-z0-9_-]{16,}`)
	workReferenceGitHubKey     = regexp.MustCompile(`(?i)(gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,})`)
	workReferenceAWSKey        = regexp.MustCompile(`AKIA[0-9A-Z]{16}`)
	workReferenceJWT           = regexp.MustCompile(`eyJ[A-Za-z0-9_-]{10,}[.][A-Za-z0-9_-]{10,}[.][A-Za-z0-9_-]{10,}`)
	workReferenceGoogleKey     = regexp.MustCompile(`AIza[0-9A-Za-z_-]{30,}`)
	workReferenceSlackKey      = regexp.MustCompile(`(?i)xox[baprs]-[a-z0-9-]{16,}`)
	workReferenceOpaqueToken   = regexp.MustCompile(`[A-Za-z0-9]{32,}`)
)

func workReferenceSafetyContract(value string, maxRunes int, allowSlash bool) bool {
	if value == "" || value != strings.TrimSpace(value) || !utf8.ValidString(value) || utf8.RuneCountInString(value) > maxRunes {
		return false
	}
	containsIdentifierCharacter := false
	for _, r := range value {
		switch {
		case r >= 'A' && r <= 'Z', r >= 'a' && r <= 'z', r >= '0' && r <= '9', r >= '一' && r <= '龥':
			containsIdentifierCharacter = true
		case r == '-' || r == '_' || r == '.':
		case allowSlash && r == '/':
		default:
			return false
		}
	}
	if !containsIdentifierCharacter || workReferenceEnglishPrefix.MatchString(value) {
		return false
	}
	if allowSlash {
		if strings.Count(value, "/") > 1 {
			return false
		}
		for _, segment := range strings.Split(value, "/") {
			if segment == "" || segment == "." || segment == ".." {
				return false
			}
		}
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
		workReferenceBearer,
		workReferenceProviderKey,
		workReferenceGitHubKey,
		workReferenceAWSKey,
		workReferenceJWT,
		workReferenceGoogleKey,
		workReferenceSlackKey,
		workReferenceOpaqueToken,
	} {
		if pattern.MatchString(value) {
			return false
		}
	}
	return true
}

func workReferenceTestCanary(parts ...string) string {
	return strings.Join(parts, "")
}

func segmentedWorkReference(length int) string {
	var builder strings.Builder
	for builder.Len() < length {
		if builder.Len() > 0 {
			builder.WriteByte('-')
		}
		remaining := length - builder.Len()
		chunkLength := 10
		if remaining < chunkLength {
			chunkLength = remaining
		}
		builder.WriteString(strings.Repeat("s", chunkLength))
	}
	return builder.String()
}
