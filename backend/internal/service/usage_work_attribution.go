package service

import (
	"context"
	"math"
	"strings"
)

// UsageWorkAttribution contains only the classification result. Request text is transient.
type UsageWorkAttribution struct {
	WorkRelated string `json:"work_related"`
	Category string `json:"category"`
	Confidence float64 `json:"confidence"`
	ClassificationSource string `json:"classification_source"`
	ClassifierVersion string `json:"classifier_version,omitempty"`
}

type usageWorkAttributionContextKey struct{}
var usageWorkAttributionKey usageWorkAttributionContextKey

func WithUsageWorkAttribution(ctx context.Context, value UsageWorkAttribution) context.Context {
	if ctx == nil { ctx = context.Background() }
	return context.WithValue(ctx, usageWorkAttributionKey, NormalizeUsageWorkAttribution(value))
}

func NormalizeUsageWorkAttribution(value UsageWorkAttribution) UsageWorkAttribution {
	value.WorkRelated = strings.ToLower(strings.TrimSpace(value.WorkRelated))
	value.Category = strings.ToLower(strings.TrimSpace(value.Category))
	value.ClassificationSource = strings.ToLower(strings.TrimSpace(value.ClassificationSource))
	value.ClassifierVersion = strings.TrimSpace(value.ClassifierVersion)
	if len(value.ClassifierVersion) > 64 { value.ClassifierVersion = value.ClassifierVersion[:64] }
	value.Confidence = math.Max(0, math.Min(1, value.Confidence))
	if value.ClassificationSource != "local_rule" && value.ClassificationSource != "import" { value.ClassificationSource = "unclassified" }
	if _, ok := validWorkCategories[value.Category]; !ok { value.Category = WorkCategoryUnclassified }
	switch value.Category {
	case WorkCategoryUnclassified: value.WorkRelated = WorkRelatedUncertain
	case WorkCategoryNonWork: value.WorkRelated = WorkRelatedNonWork
	default: value.WorkRelated = WorkRelatedWork
	}
	if value.ClassificationSource == "unclassified" { value.WorkRelated = WorkRelatedUncertain; value.Category = WorkCategoryUnclassified }
	return value
}

func UsageWorkAttributionFromContext(ctx context.Context) (UsageWorkAttribution, bool) {
	if ctx == nil { return UsageWorkAttribution{}, false }
	value, ok := ctx.Value(usageWorkAttributionKey).(UsageWorkAttribution)
	if !ok { return UsageWorkAttribution{}, false }
	return NormalizeUsageWorkAttribution(value), true
}

func ApplyUsageWorkAttribution(ctx context.Context, usageLog *UsageLog) {
	if usageLog == nil || usageLog.WorkAttribution != nil { return }
	if value, ok := UsageWorkAttributionFromContext(ctx); ok { value = NormalizeUsageWorkAttribution(value); usageLog.WorkAttribution = &value }
}
