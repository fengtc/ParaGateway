package service

import (
	"context"
	"math"
	"strings"

	"github.com/Wei-Shaw/sub2api/internal/pkg/workclassifier"
)

// UsageWorkAttribution contains only structured, sanitized classification data.
// Request text and source code must never be stored in this value.
type UsageWorkAttribution struct {
	ProjectRef           string  `json:"project_ref,omitempty"`
	RepositoryRef        string  `json:"repository_ref,omitempty"`
	SubmissionType       string  `json:"submission_type,omitempty"`
	WorkRelated          string  `json:"work_related"`
	Category             string  `json:"category"`
	Confidence           float64 `json:"confidence"`
	ClassificationSource string  `json:"classification_source"`
	ClassifierVersion    string  `json:"classifier_version,omitempty"`
}

type usageWorkAttributionContextKey struct{}

var usageWorkAttributionKey usageWorkAttributionContextKey

func WithUsageWorkAttribution(ctx context.Context, attribution UsageWorkAttribution) context.Context {
	if ctx == nil {
		ctx = context.Background()
	}
	attribution = NormalizeUsageWorkAttribution(attribution)
	return context.WithValue(ctx, usageWorkAttributionKey, attribution)
}

// NormalizeUsageWorkAttribution is the final trust boundary before structured
// attribution crosses an asynchronous or persisted lifecycle.
func NormalizeUsageWorkAttribution(attribution UsageWorkAttribution) UsageWorkAttribution {
	attribution.ProjectRef = workclassifier.CleanProjectRef(attribution.ProjectRef)
	attribution.RepositoryRef = workclassifier.CleanRepositoryRef(attribution.RepositoryRef)
	attribution.SubmissionType = workclassifier.NormalizeSubmissionType(attribution.SubmissionType)
	attribution.WorkRelated = strings.ToLower(strings.TrimSpace(attribution.WorkRelated))
	attribution.Category = strings.ToLower(strings.TrimSpace(attribution.Category))
	attribution.ClassificationSource = strings.ToLower(strings.TrimSpace(attribution.ClassificationSource))
	attribution.ClassifierVersion = workclassifier.CleanClassifierVersion(attribution.ClassifierVersion)
	attribution.Confidence = math.Max(0, math.Min(1, attribution.Confidence))

	switch attribution.ClassificationSource {
	case "explicit_metadata", "local_rule", "unclassified", "manual_review", "import":
	default:
		attribution.ClassificationSource = "unclassified"
	}
	if _, ok := validWorkCategories[attribution.Category]; !ok {
		attribution.Category = WorkCategoryUnclassified
	}
	switch attribution.Category {
	case WorkCategoryUnclassified:
		attribution.WorkRelated = WorkRelatedUncertain
	case WorkCategoryNonWork:
		attribution.WorkRelated = WorkRelatedNonWork
	default:
		attribution.WorkRelated = WorkRelatedWork
	}
	if attribution.ClassificationSource == "unclassified" {
		attribution.WorkRelated = WorkRelatedUncertain
		attribution.Category = WorkCategoryUnclassified
	}
	return attribution
}

func UsageWorkAttributionFromContext(ctx context.Context) (UsageWorkAttribution, bool) {
	if ctx == nil {
		return UsageWorkAttribution{}, false
	}
	attribution, ok := ctx.Value(usageWorkAttributionKey).(UsageWorkAttribution)
	if !ok {
		return UsageWorkAttribution{}, false
	}
	return NormalizeUsageWorkAttribution(attribution), true
}

func ApplyUsageWorkAttribution(ctx context.Context, usageLog *UsageLog) {
	if usageLog == nil || usageLog.WorkAttribution != nil {
		return
	}
	attribution, ok := UsageWorkAttributionFromContext(ctx)
	if !ok {
		return
	}
	attribution = NormalizeUsageWorkAttribution(attribution)
	usageLog.WorkAttribution = &attribution
}
