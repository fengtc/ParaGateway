// Package workclassifier classifies transient request context without retaining
// prompt content. It is deterministic, local-only, and performs no I/O or logging.
package workclassifier

import (
	"log/slog"
	"math"
	"strings"
)

type Category string

type WorkRelation string

const (
	CategoryCoding        Category = "coding"
	CategoryDocumentation Category = "documentation"
	CategoryAnalysis      Category = "data_analysis"
	CategoryOperations    Category = "operations"
	CategoryCommunication Category = "communication"
	CategoryLearning      Category = "learning"
	CategoryOther         Category = "other"
	CategoryNonWork       Category = "non_work"
	CategoryUnclassified  Category = "unclassified"

	WorkRelationWork      WorkRelation = "work"
	WorkRelationNonWork   WorkRelation = "non_work"
	WorkRelationUncertain WorkRelation = "uncertain"

	SourceLocalRule        = "local_rule"
	SourceUnclassified     = "unclassified"

	ClassifierVersion = "work-content-rules-v1"
)

var validCategories = map[Category]struct{}{
	CategoryCoding:        {},
	CategoryDocumentation: {},
	CategoryAnalysis:      {},
	CategoryOperations:    {},
	CategoryCommunication: {},
	CategoryLearning:      {},
	CategoryOther:         {},
	CategoryNonWork:       {},
	CategoryUnclassified:  {},
}

// Input is intentionally excluded from JSON and safe logging. TransientText must
// only exist for the duration of Classify and must never be stored by callers.
type Input struct {
	TransientText string `json:"-"`
}

func (Input) String() string   { return "<workclassifier.Input redacted>" }
func (Input) GoString() string { return "<workclassifier.Input redacted>" }
func (Input) LogValue() slog.Value {
	return slog.StringValue("<workclassifier.Input redacted>")
}

type Result struct {
	WorkRelated          WorkRelation `json:"work_related"`
	Category             Category     `json:"category"`
	Confidence           float64      `json:"confidence"`
	ClassificationSource string       `json:"classification_source"`
	ClassifierVersion    string       `json:"classifier_version"`
}

// Classify applies local transient-text rules. Ambiguous matches remain unclassified.
func Classify(input Input) Result {
	category, confidence, ok := classifyText(input.TransientText)
	if ok {
		return newResult(relationForCategory(category), category, confidence, SourceLocalRule)
	}
	return newResult(WorkRelationUncertain, CategoryUnclassified, 0.30, SourceUnclassified)
}

func newResult(workRelated WorkRelation, category Category, confidence float64, source string) Result {
	if !IsValidCategory(category) {
		category = CategoryUnclassified
	}
	switch category {
	case CategoryUnclassified:
		workRelated = WorkRelationUncertain
	case CategoryNonWork:
		workRelated = WorkRelationNonWork
	default:
		workRelated = WorkRelationWork
	}
	return Result{
		WorkRelated:          workRelated,
		Category:             category,
		Confidence:           math.Round(clamp(confidence, 0, 1)*100) / 100,
		ClassificationSource: source,
		ClassifierVersion:    ClassifierVersion,
	}
}

func relationForCategory(category Category) WorkRelation {
	if category == CategoryNonWork {
		return WorkRelationNonWork
	}
	if category == CategoryUnclassified {
		return WorkRelationUncertain
	}
	return WorkRelationWork
}

func IsValidCategory(category Category) bool {
	_, ok := validCategories[category]
	return ok
}

func NormalizeCategory(value string) (Category, bool) {
	normalized := strings.ToLower(strings.TrimSpace(value))
	normalized = strings.ReplaceAll(normalized, "-", "_")
	normalized = strings.ReplaceAll(normalized, " ", "_")
	if normalized == "analysis" {
		normalized = string(CategoryAnalysis)
	}
	category := Category(normalized)
	return category, IsValidCategory(category)
}

func NormalizeWorkRelation(value string) (WorkRelation, bool) {
	switch strings.ToLower(strings.TrimSpace(value)) {
	case "1", "true", "yes", "on", "work", "work_related":
		return WorkRelationWork, true
	case "0", "false", "no", "off", "non_work", "non-work":
		return WorkRelationNonWork, true
	case "uncertain", "unknown", "unclassified":
		return WorkRelationUncertain, true
	default:
		return "", false
	}
}

func clamp(value, minValue, maxValue float64) float64 {
	if value < minValue {
		return minValue
	}
	if value > maxValue {
		return maxValue
	}
	return value
}
