package workclassifier

import (
	"encoding/json"
	"fmt"
	"log/slog"
	"strings"
	"testing"

	"github.com/stretchr/testify/require"
)

func relationPointer(value WorkRelation) *WorkRelation { return &value }

func TestClassifyPriorityAndCategories(t *testing.T) {
	tests := []struct {
		name            string
		input           Input
		wantWorkRelated WorkRelation
		wantCategory    Category
		wantSource      string
	}{
		{
			name: "explicit category wins over contradictory text",
			input: Input{
				ExplicitCategory: "documentation",
				TransientText:    "debug source code and run unit test",
			},
			wantWorkRelated: WorkRelationWork,
			wantCategory:    CategoryDocumentation,
			wantSource:      SourceExplicitMetadata,
		},
		{
			name: "explicit non work wins",
			input: Input{
				ExplicitWorkRelated: relationPointer(WorkRelationNonWork),
				Repository:          "payments-backend",
			},
			wantWorkRelated: WorkRelationNonWork,
			wantCategory:    CategoryNonWork,
			wantSource:      SourceExplicitMetadata,
		},
		{
			name: "submission type wins over text",
			input: Input{
				SubmissionType: "documentation",
				TransientText:  "compile error and unit test failure",
			},
			wantWorkRelated: WorkRelationWork,
			wantCategory:    CategoryDocumentation,
			wantSource:      SourceLocalRule,
		},
		{
			name: "explicit work combines with metadata category",
			input: Input{
				ExplicitWorkRelated: relationPointer(WorkRelationWork),
				Repository:          "customer-backend",
			},
			wantWorkRelated: WorkRelationWork,
			wantCategory:    CategoryCoding,
			wantSource:      SourceLocalRule,
		},
		{
			name: "metadata analysis",
			input: Input{
				Project:    "sales-analytics",
				Repository: "business-intelligence-dashboard",
			},
			wantWorkRelated: WorkRelationWork,
			wantCategory:    CategoryAnalysis,
			wantSource:      SourceLocalRule,
		},
		{
			name: "underscored data analysis submission type",
			input: Input{
				SubmissionType: "data_analysis",
			},
			wantWorkRelated: WorkRelationWork,
			wantCategory:    CategoryAnalysis,
			wantSource:      SourceLocalRule,
		},
		{
			name: "transient text operations",
			input: Input{
				TransientText: "Deploy the service with Kubernetes and verify production deployment monitoring.",
			},
			wantWorkRelated: WorkRelationWork,
			wantCategory:    CategoryOperations,
			wantSource:      SourceLocalRule,
		},
		{
			name: "transient text Chinese documentation",
			input: Input{
				TransientText: "请撰写技术方案和需求说明文档。",
			},
			wantWorkRelated: WorkRelationWork,
			wantCategory:    CategoryDocumentation,
			wantSource:      SourceLocalRule,
		},
		{
			name: "clear non work text",
			input: Input{
				TransientText: "Create a personal travel itinerary and movie recommendation list.",
			},
			wantWorkRelated: WorkRelationNonWork,
			wantCategory:    CategoryNonWork,
			wantSource:      SourceLocalRule,
		},
		{
			name: "weak signal stays unclassified",
			input: Input{
				TransientText: "Please help me with this.",
			},
			wantWorkRelated: WorkRelationUncertain,
			wantCategory:    CategoryUnclassified,
			wantSource:      SourceUnclassified,
		},
		{
			name: "single potentially personal term stays uncertain",
			input: Input{
				Project:       "personal-assistant",
				TransientText: "Please create a travel itinerary.",
			},
			wantWorkRelated: WorkRelationUncertain,
			wantCategory:    CategoryUnclassified,
			wantSource:      SourceUnclassified,
		},
		{
			name: "explicit work without category remains uncertain and unclassified",
			input: Input{
				ExplicitWorkRelated: relationPointer(WorkRelationWork),
				TransientText:       "Please help me with this.",
			},
			wantWorkRelated: WorkRelationUncertain,
			wantCategory:    CategoryUnclassified,
			wantSource:      SourceExplicitMetadata,
		},
		{
			name: "ambiguous local rules stay unclassified",
			input: Input{
				Project: "documentation-backend",
			},
			wantWorkRelated: WorkRelationUncertain,
			wantCategory:    CategoryUnclassified,
			wantSource:      SourceUnclassified,
		},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			got := Classify(tc.input)
			require.Equal(t, tc.wantWorkRelated, got.WorkRelated)
			require.Equal(t, tc.wantCategory, got.Category)
			require.Equal(t, tc.wantSource, got.ClassificationSource)
			require.Equal(t, ClassifierVersion, got.ClassifierVersion)
			require.GreaterOrEqual(t, got.Confidence, 0.0)
			require.LessOrEqual(t, got.Confidence, 1.0)
		})
	}
}

func TestClassifyExplicitWorkConflictWithInferredNonWorkIsUncertain(t *testing.T) {
	result := Classify(Input{
		ExplicitWorkRelated: relationPointer(WorkRelationWork),
		TransientText:       "Create a personal travel itinerary and movie recommendation list.",
	})
	require.Equal(t, WorkRelationUncertain, result.WorkRelated)
	require.Equal(t, CategoryUnclassified, result.Category)
	require.Equal(t, SourceExplicitMetadata, result.ClassificationSource)
}

func TestNormalizeCategory(t *testing.T) {
	tests := []struct {
		input string
		want  Category
		ok    bool
	}{
		{"CODING", CategoryCoding, true},
		{" non-work ", CategoryNonWork, true},
		{"analysis", CategoryAnalysis, true},
		{"data_analysis", CategoryAnalysis, true},
		{"unclassified", CategoryUnclassified, true},
		{"performance", Category("performance"), false},
		{"", Category(""), false},
	}
	for _, tc := range tests {
		got, ok := NormalizeCategory(tc.input)
		require.Equal(t, tc.ok, ok)
		require.Equal(t, tc.want, got)
	}
}

func TestNormalizeWorkRelation(t *testing.T) {
	tests := []struct {
		input string
		want  WorkRelation
		ok    bool
	}{
		{"work", WorkRelationWork, true},
		{"TRUE", WorkRelationWork, true},
		{"non-work", WorkRelationNonWork, true},
		{"false", WorkRelationNonWork, true},
		{"uncertain", WorkRelationUncertain, true},
		{"unknown", WorkRelationUncertain, true},
		{"maybe", WorkRelation(""), false},
	}
	for _, tc := range tests {
		got, ok := NormalizeWorkRelation(tc.input)
		require.Equal(t, tc.ok, ok)
		require.Equal(t, tc.want, got)
	}
}

func TestInputCannotExposeTransientContentThroughJSONOrFormatting(t *testing.T) {
	secret := "sk-" + strings.Repeat("s", 24)
	input := Input{
		Project:        "secret-project",
		Repository:     "private-repository",
		SubmissionType: "code",
		TransientText:  "complete source code " + secret,
	}

	encoded, err := json.Marshal(input)
	require.NoError(t, err)
	require.JSONEq(t, `{}`, string(encoded))

	for _, rendered := range []string{
		fmt.Sprintf("%v", input),
		fmt.Sprintf("%+v", input),
		fmt.Sprintf("%#v", input),
		slog.Any("input", input).Value.Resolve().String(),
	} {
		require.NotContains(t, rendered, secret)
		require.NotContains(t, rendered, "complete source code")
		require.Contains(t, strings.ToLower(rendered), "redacted")
	}
}

func TestResultNeverContainsInputContent(t *testing.T) {
	secret := "github_" + "pat_" + strings.Repeat("1", 30)
	result := Classify(Input{
		Repository:    "backend",
		TransientText: "func main contains " + secret,
	})
	encoded, err := json.Marshal(result)
	require.NoError(t, err)
	require.NotContains(t, string(encoded), secret)
	require.NotContains(t, string(encoded), "func main")
	require.Equal(t, CategoryCoding, result.Category)
}

func TestClassifyIsDeterministic(t *testing.T) {
	input := Input{
		Project:       "sales-analysis",
		TransientText: "Run a SQL query and build a data analysis dashboard.",
	}
	want := Classify(input)
	for range 20 {
		require.Equal(t, want, Classify(input))
	}
}
