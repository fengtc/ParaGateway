package workclassifier

import (
	"encoding/json"
	"fmt"
	"net/http"
	"strings"
	"testing"

	"github.com/stretchr/testify/require"
)

func TestInputFromHeadersUsesOnlyCanonicalAllowlist(t *testing.T) {
	headers := http.Header{
		HeaderProject:          []string{"  customer-platform  "},
		"X-Project":            []string{"ignored-alias"},
		HeaderRepository:       []string{"fengtc/ParaGateway"},
		HeaderSubmissionType:   []string{"code"},
		"X-Para-Work-Related":  []string{"yes"},
		"X-Para-Work-Category": []string{"CODING"},
		"Authorization":        []string{"Bearer " + "must-never-be-read"},
		"X-Prompt":             []string{"complete private source code"},
	}

	got := InputFromHeaders(headers)
	require.Equal(t, "customer-platform", got.Project)
	require.Equal(t, "fengtc/ParaGateway", got.Repository)
	require.Equal(t, "coding", got.SubmissionType)
	require.Nil(t, got.ExplicitWorkRelated)
	require.Empty(t, got.ExplicitCategory)
	require.Empty(t, got.TransientText)

	formatted := fmt.Sprintf("%+v", got)
	require.NotContains(t, formatted, "must-never-be-read")
	require.NotContains(t, formatted, "complete private source code")
}

func TestInputFromHeadersIgnoresAliases(t *testing.T) {
	headers := http.Header{
		"X-Project":         []string{"docs"},
		"X-Repository":      []string{"manual"},
		"X-Submission-Type": []string{"documentation"},
		"X-Work-Related":    []string{"work"},
		"X-Work-Category":   []string{"coding"},
	}
	got := InputFromHeaders(headers)
	require.Empty(t, got.Project)
	require.Empty(t, got.Repository)
	require.Empty(t, got.SubmissionType)
	require.Nil(t, got.ExplicitWorkRelated)
	require.Empty(t, got.ExplicitCategory)
}

func TestCleanMetadataFieldRejectsSecretsCodeJSONAndFreeText(t *testing.T) {
	tests := []struct {
		name  string
		input string
	}{
		{
			name:  "key value",
			input: "repo api_key=the-secret-value backend",
		},
		{
			name:  "standalone OpenAI style",
			input: "repo " + "sk-" + strings.Repeat("a", 24) + " backend",
		},
		{
			name:  "GitHub PAT",
			input: "github_" + "pat_" + strings.Repeat("1", 30),
		},
		{
			name:  "opaque token without a known prefix",
			input: strings.Repeat("a", 32),
		},
		{
			name:  "embedded GitHub token prefix",
			input: "team_" + "gh" + "p_" + strings.Repeat("g", 20),
		},
		{
			name:  "embedded provider key prefix",
			input: "team_" + "s" + "k-" + strings.Repeat("h", 16),
		},
		{
			name:  "URL user info",
			input: "https://admin:password@example.test/repo",
		},
		{
			name:  "controls",
			input: "project\r\n injected\tvalue",
		},
		{
			name:  "JSON",
			input: `{"project":"billing"}`,
		},
		{
			name:  "source code",
			input: "func main() { return secret; }",
		},
		{
			name:  "English free prompt",
			input: "please analyze all of this private customer document for me",
		},
		{
			name:  "Chinese free prompt",
			input: "请帮我修改以下完整源代码",
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			require.Empty(t, CleanMetadataField(tc.input))
		})
	}
}

func TestCleanMetadataFieldAcceptsCommonIdentifiers(t *testing.T) {
	tests := map[string]string{
		"English project": "customer-platform-2",
		"Chinese project": "客户智能分析平台二期",
		"versioned slug":  "analysis-service.v2",
	}
	for name, input := range tests {
		t.Run(name, func(t *testing.T) {
			require.Equal(t, input, CleanMetadataField(input))
		})
	}
	require.Equal(t, "customer-platform", CleanMetadataField("  customer-platform  "))
	require.Equal(t, "fengtc/ParaGateway", CleanRepositoryRef("fengtc/ParaGateway"))
	require.Equal(t, "研发组/智能分析-service.v2", CleanRepositoryRef("研发组/智能分析-service.v2"))
	require.Empty(t, CleanMetadataField("Customer Platform"))
	require.Empty(t, CleanRepositoryRef("org/team/repository"))
	require.Empty(t, CleanMetadataField("---"))
}

func TestProjectAndRepositoryRefsRejectOpaqueLongTokensWithoutRejectingShortNames(t *testing.T) {
	shortIdentifier := strings.Repeat("a", 31)
	opaqueToken := strings.Repeat("b", 32)

	require.Equal(t, shortIdentifier, CleanProjectRef(shortIdentifier))
	require.Equal(t, "team/"+shortIdentifier, CleanRepositoryRef("team/"+shortIdentifier))
	require.Equal(t, "customer.analytics.platform.v2", CleanProjectRef("customer.analytics.platform.v2"))
	require.Equal(t, "customer-analytics-platform-service", CleanProjectRef("customer-analytics-platform-service"))
	require.Empty(t, CleanProjectRef(opaqueToken))
	require.Empty(t, CleanRepositoryRef("team/"+opaqueToken))
	require.Empty(t, CleanRepositoryRef(opaqueToken+"/service"))
}

func TestCleanClassifierVersionUsesFailClosedLengthBoundary(t *testing.T) {
	require.Equal(t, "work-classifier-v1", CleanClassifierVersion("work-classifier-v1"))
	require.Empty(t, CleanClassifierVersion(strings.Repeat("v-", 32)+"v"))
	require.Empty(t, CleanClassifierVersion(strings.Repeat("-", 64)+"v"))
}

func TestCleanMetadataFieldRejectsOversizedValue(t *testing.T) {
	input := strings.Repeat("智", MaxMetadataRunes+25)
	require.Empty(t, CleanMetadataField(input))
}

func TestCleanMetadataFieldRejectsInvalidUTF8(t *testing.T) {
	require.Empty(t, CleanMetadataField(string([]byte{'a', 0xff, 'b'})))
}

func TestHeaderInputJSONContainsNoMetadata(t *testing.T) {
	input := InputFromHeaders(http.Header{
		HeaderProject:    []string{"confidential-project"},
		HeaderRepository: []string{"private-repository"},
	})
	encoded, err := json.Marshal(input)
	require.NoError(t, err)
	require.JSONEq(t, `{}`, string(encoded))
}

func TestInputFromHeadersClassifiesWithoutReadingPromptHeader(t *testing.T) {
	input := InputFromHeaders(http.Header{
		HeaderRepository: []string{"payments-backend"},
		"X-Prompt":       []string{"personal travel itinerary and movie recommendation"},
	})
	result := Classify(input)
	require.Equal(t, WorkRelationWork, result.WorkRelated)
	require.Equal(t, CategoryCoding, result.Category)
}

func TestInputFromHeadersRejectsUnsafeMetadataWithoutReturningRawValue(t *testing.T) {
	secret := "github_" + "pat_" + strings.Repeat("1", 30)
	prompt := "please review the complete private source code before deployment"
	input := InputFromHeaders(http.Header{
		HeaderProject:    []string{prompt},
		HeaderRepository: []string{secret},
	})
	require.Empty(t, input.Project)
	require.Empty(t, input.Repository)

	rendered := fmt.Sprintf("%+v", input)
	require.NotContains(t, rendered, secret)
	require.NotContains(t, rendered, prompt)
}

func TestInputFromHeadersNormalizesKnownSubmissionTypes(t *testing.T) {
	tests := []struct {
		raw          string
		want         string
		wantCategory Category
	}{
		{raw: "document", want: "documentation", wantCategory: CategoryDocumentation},
		{raw: "analysis", want: "data_analysis", wantCategory: CategoryAnalysis},
		{raw: "personal", want: "non_work", wantCategory: CategoryNonWork},
		{raw: "pull request", want: "pull_request", wantCategory: CategoryCoding},
		{raw: "data-analysis", want: "data_analysis", wantCategory: CategoryAnalysis},
	}
	for _, tc := range tests {
		t.Run(tc.raw, func(t *testing.T) {
			input := InputFromHeaders(http.Header{HeaderSubmissionType: []string{tc.raw}})
			require.Equal(t, tc.want, input.SubmissionType)
			require.Equal(t, tc.wantCategory, Classify(input).Category)
		})
	}

	input := InputFromHeaders(http.Header{HeaderSubmissionType: []string{"arbitrary prompt text"}})
	require.Empty(t, input.SubmissionType)
}
