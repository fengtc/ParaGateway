package service

import (
	"encoding/json"
	"testing"

	"github.com/stretchr/testify/require"
)

func TestNormalizeUsageWorkAttributionEnforcesStructuredBoundary(t *testing.T) {
	got := NormalizeUsageWorkAttribution(UsageWorkAttribution{
		ProjectRef:           "Customer Platform",
		RepositoryRef:        "org/team/repository",
		SubmissionType:       "document",
		WorkRelated:          WorkRelatedNonWork,
		Category:             WorkCategoryCoding,
		Confidence:           4,
		ClassificationSource: "CLIENT_OVERRIDE",
		ClassifierVersion:    "rules-v1",
	})
	require.Empty(t, got.ProjectRef)
	require.Empty(t, got.RepositoryRef)
	require.Equal(t, "documentation", got.SubmissionType)
	require.Equal(t, 1.0, got.Confidence)
	require.Equal(t, "unclassified", got.ClassificationSource)
	// An unclassified source cannot retain a concrete classification.
	require.Equal(t, WorkRelatedUncertain, got.WorkRelated)
	require.Equal(t, WorkCategoryUnclassified, got.Category)

	paired := NormalizeUsageWorkAttribution(UsageWorkAttribution{
		WorkRelated: WorkRelatedNonWork, Category: WorkCategoryCoding,
		ClassificationSource: "local_rule",
	})
	require.Equal(t, WorkRelatedWork, paired.WorkRelated)
	require.Equal(t, WorkCategoryCoding, paired.Category)
}

func TestUsageWorkAttributionJSONContainsOnlyAllowlistedStructuredFields(t *testing.T) {
	got := NormalizeUsageWorkAttribution(UsageWorkAttribution{
		ProjectRef: "paragateway", RepositoryRef: "fengtc/ParaGateway",
		SubmissionType: "code", WorkRelated: WorkRelatedWork,
		Category: WorkCategoryCoding, Confidence: 0.8,
		ClassificationSource: "local_rule", ClassifierVersion: "rules-v1",
	})
	encoded, err := json.Marshal(got)
	require.NoError(t, err)
	var fields map[string]any
	require.NoError(t, json.Unmarshal(encoded, &fields))
	allowed := map[string]struct{}{
		"project_ref": {}, "repository_ref": {}, "submission_type": {},
		"work_related": {}, "category": {}, "confidence": {},
		"classification_source": {}, "classifier_version": {},
	}
	for key := range fields {
		_, ok := allowed[key]
		require.Truef(t, ok, "unexpected attribution field %q", key)
	}
	require.NotContains(t, string(encoded), "prompt")
	require.NotContains(t, string(encoded), "source_code")
}
