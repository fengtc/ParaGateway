package service

import (
	"context"
	"sort"
	"strings"
	"time"

	infraerrors "github.com/Wei-Shaw/sub2api/internal/pkg/errors"
)

const (
	WorkMetricRequests        = "requests"
	WorkMetricTokens          = "tokens"
	WorkRelatedWork           = "work"
	WorkRelatedNonWork        = "non_work"
	WorkRelatedUncertain      = "uncertain"
	WorkCategoryCoding        = "coding"
	WorkCategoryDocumentation = "documentation"
	WorkCategoryDataAnalysis  = "data_analysis"
	WorkCategoryOperations    = "operations"
	WorkCategoryCommunication = "communication"
	WorkCategoryLearning      = "learning"
	WorkCategoryOther         = "other"
	WorkCategoryUnclassified  = "unclassified"
	WorkCategoryNonWork       = "non_work"
)

var validWorkCategories = map[string]struct{}{
	WorkCategoryCoding: {}, WorkCategoryDocumentation: {}, WorkCategoryDataAnalysis: {}, WorkCategoryOperations: {},
	WorkCategoryCommunication: {}, WorkCategoryLearning: {}, WorkCategoryOther: {}, WorkCategoryUnclassified: {}, WorkCategoryNonWork: {},
}

type WorkDistributionFilter struct {
	StartTime, EndTime time.Time
	UserID             int64
	Department, Role   string
}
type WorkDistributionSummaryFilter struct {
	WorkDistributionFilter
	Metric    string
	UserLimit int
}

type WorkDistributionAggregate struct {
	UserID                                         int64
	Email, Department, Role, WorkRelated, Category string
	Requests, TotalTokens                          int64
	ConfidenceSum                                  float64
	ConfidenceSampleCount                          int64
}

type WorkDistributionCategory struct {
	Category              string   `json:"category"`
	WorkRelated           string   `json:"work_related"`
	Requests              int64    `json:"requests"`
	TotalTokens           int64    `json:"total_tokens"`
	Value                 int64    `json:"value"`
	Percent               float64  `json:"percent"`
	AverageConfidence     *float64 `json:"average_confidence,omitempty"`
	ConfidenceSampleCount int64    `json:"confidence_sample_count"`
	confidenceSum         float64
}
type WorkDistributionRelation struct {
	WorkRelated string  `json:"work_related"`
	Requests    int64   `json:"requests"`
	TotalTokens int64   `json:"total_tokens"`
	Value       int64   `json:"value"`
	Percent     float64 `json:"percent"`
}
type WorkDistributionDepartment struct {
	Department            string                     `json:"department"`
	Requests              int64                      `json:"requests"`
	TotalTokens           int64                      `json:"total_tokens"`
	Value                 int64                      `json:"value"`
	AverageConfidence     *float64                   `json:"average_confidence,omitempty"`
	ConfidenceSampleCount int64                      `json:"confidence_sample_count"`
	Categories            []WorkDistributionCategory `json:"categories"`
	confidenceSum         float64
}
type WorkDistributionUser struct {
	UserID                int64                      `json:"user_id"`
	Email                 string                     `json:"email"`
	Department            string                     `json:"department"`
	Role                  string                     `json:"role"`
	Requests              int64                      `json:"requests"`
	TotalTokens           int64                      `json:"total_tokens"`
	Value                 int64                      `json:"value"`
	AverageConfidence     *float64                   `json:"average_confidence,omitempty"`
	ConfidenceSampleCount int64                      `json:"confidence_sample_count"`
	Categories            []WorkDistributionCategory `json:"categories"`
	confidenceSum         float64
}
type WorkDistributionRole struct {
	Role      string `json:"role"`
	UserCount int64  `json:"user_count"`
}
type WorkDistributionCoverage struct {
	TotalRequests        int64   `json:"total_requests"`
	ClassifiedRequests   int64   `json:"classified_requests"`
	UnclassifiedRequests int64   `json:"unclassified_requests"`
	ClassifiedPercent    float64 `json:"classified_percent"`
}
type WorkDistributionSummary struct {
	GeneratedAt           time.Time                    `json:"generated_at"`
	StartDate             string                       `json:"start_date"`
	EndDate               string                       `json:"end_date"`
	Metric                string                       `json:"metric"`
	CollectionStatus      string                       `json:"collection_status"`
	AverageConfidence     *float64                     `json:"average_confidence,omitempty"`
	ConfidenceSampleCount int64                        `json:"confidence_sample_count"`
	Coverage              WorkDistributionCoverage     `json:"coverage"`
	WorkRelated           []WorkDistributionRelation   `json:"work_related"`
	Categories            []WorkDistributionCategory   `json:"categories"`
	Departments           []WorkDistributionDepartment `json:"departments"`
	Roles                 []WorkDistributionRole       `json:"roles"`
	Users                 []WorkDistributionUser       `json:"users"`
}

type WorkDistributionRepository interface {
	GetAggregates(context.Context, WorkDistributionFilter) ([]WorkDistributionAggregate, error)
}
type WorkDistributionService struct{ repo WorkDistributionRepository }

func NewWorkDistributionService(repo WorkDistributionRepository) *WorkDistributionService {
	return &WorkDistributionService{repo: repo}
}

func (s *WorkDistributionService) GetSummary(ctx context.Context, filter WorkDistributionSummaryFilter) (*WorkDistributionSummary, error) {
	if filter.StartTime.IsZero() || filter.EndTime.IsZero() || !filter.EndTime.After(filter.StartTime) {
		return nil, infraerrors.BadRequest("INVALID_DATE_RANGE", "invalid date range")
	}
	filter.Metric = strings.ToLower(strings.TrimSpace(filter.Metric))
	if filter.Metric == "" {
		filter.Metric = WorkMetricRequests
	}
	if filter.Metric != WorkMetricRequests && filter.Metric != WorkMetricTokens {
		return nil, infraerrors.BadRequest("INVALID_METRIC", "metric must be requests or tokens")
	}
	if filter.UserLimit <= 0 || filter.UserLimit > 500 {
		filter.UserLimit = 100
	}
	rows, err := s.repo.GetAggregates(ctx, filter.WorkDistributionFilter)
	if err != nil {
		return nil, err
	}
	result := &WorkDistributionSummary{GeneratedAt: time.Now().UTC(), StartDate: filter.StartTime.Format("2006-01-02"), EndDate: filter.EndTime.Add(-time.Nanosecond).Format("2006-01-02"), Metric: filter.Metric, CollectionStatus: "no_data", Categories: []WorkDistributionCategory{}, WorkRelated: []WorkDistributionRelation{}, Departments: []WorkDistributionDepartment{}, Roles: []WorkDistributionRole{}, Users: []WorkDistributionUser{}}
	categories := map[string]*WorkDistributionCategory{}
	relations := map[string]*WorkDistributionRelation{}
	departments := map[string]*WorkDistributionDepartment{}
	users := map[int64]*WorkDistributionUser{}
	roleUsers := map[string]map[int64]struct{}{}
	roleLabels := map[string]string{}
	var confidenceSum float64
	for _, row := range rows {
		result.Coverage.TotalRequests += row.Requests
		if row.Category != WorkCategoryUnclassified {
			result.Coverage.ClassifiedRequests += row.Requests
		}
		result.ConfidenceSampleCount += row.ConfidenceSampleCount
		confidenceSum += row.ConfidenceSum
		addCategory(categories, row)
		addRelation(relations, row)
		department := strings.TrimSpace(row.Department)
		if department == "" {
			department = "unknown"
		}
		dep := departments[department]
		if dep == nil {
			dep = &WorkDistributionDepartment{Department: department, Categories: []WorkDistributionCategory{}}
			departments[department] = dep
		}
		dep.Requests += row.Requests
		dep.TotalTokens += row.TotalTokens
		dep.ConfidenceSampleCount += row.ConfidenceSampleCount
		dep.confidenceSum += row.ConfidenceSum
		depMap := categorySliceMap(dep.Categories)
		addCategory(depMap, row)
		dep.Categories = categoryMapSlice(depMap)
		user := users[row.UserID]
		if user == nil {
			user = &WorkDistributionUser{UserID: row.UserID, Email: row.Email, Department: department, Role: row.Role, Categories: []WorkDistributionCategory{}}
			users[row.UserID] = user
		}
		user.Requests += row.Requests
		user.TotalTokens += row.TotalTokens
		user.ConfidenceSampleCount += row.ConfidenceSampleCount
		user.confidenceSum += row.ConfidenceSum
		userMap := categorySliceMap(user.Categories)
		addCategory(userMap, row)
		user.Categories = categoryMapSlice(userMap)
		role := strings.TrimSpace(row.Role)
		if role == "" {
			role = "unknown"
		}
		key := strings.ToLower(role)
		if roleUsers[key] == nil {
			roleUsers[key] = map[int64]struct{}{}
			roleLabels[key] = role
		}
		roleUsers[key][row.UserID] = struct{}{}
	}
	result.Coverage.UnclassifiedRequests = result.Coverage.TotalRequests - result.Coverage.ClassifiedRequests
	result.Coverage.ClassifiedPercent = percentage(result.Coverage.ClassifiedRequests, result.Coverage.TotalRequests)
	if result.Coverage.TotalRequests > 0 {
		result.CollectionStatus = "active"
	}
	result.AverageConfidence = averageConfidence(confidenceSum, result.ConfidenceSampleCount)
	result.Categories = finalizeCategories(categoryMapSlice(categories), filter.Metric)
	for _, item := range relations {
		item.Value = metricValue(filter.Metric, item.Requests, item.TotalTokens)
		item.Percent = percentage(item.Value, totalRelationValue(relations, filter.Metric))
		result.WorkRelated = append(result.WorkRelated, *item)
	}
	sort.Slice(result.WorkRelated, func(i, j int) bool { return result.WorkRelated[i].Value > result.WorkRelated[j].Value })
	for _, item := range departments {
		item.Value = metricValue(filter.Metric, item.Requests, item.TotalTokens)
		item.AverageConfidence = averageConfidence(item.confidenceSum, item.ConfidenceSampleCount)
		item.Categories = finalizeCategories(item.Categories, filter.Metric)
		result.Departments = append(result.Departments, *item)
	}
	sort.Slice(result.Departments, func(i, j int) bool { return result.Departments[i].Value > result.Departments[j].Value })
	for _, item := range users {
		item.Value = metricValue(filter.Metric, item.Requests, item.TotalTokens)
		item.AverageConfidence = averageConfidence(item.confidenceSum, item.ConfidenceSampleCount)
		item.Categories = finalizeCategories(item.Categories, filter.Metric)
		result.Users = append(result.Users, *item)
	}
	sort.Slice(result.Users, func(i, j int) bool { return result.Users[i].Value > result.Users[j].Value })
	if len(result.Users) > filter.UserLimit {
		result.Users = result.Users[:filter.UserLimit]
	}
	for key, set := range roleUsers {
		result.Roles = append(result.Roles, WorkDistributionRole{Role: roleLabels[key], UserCount: int64(len(set))})
	}
	sort.Slice(result.Roles, func(i, j int) bool { return result.Roles[i].Role < result.Roles[j].Role })
	return result, nil
}

func addCategory(items map[string]*WorkDistributionCategory, row WorkDistributionAggregate) {
	item := items[row.Category]
	if item == nil {
		item = &WorkDistributionCategory{Category: row.Category, WorkRelated: row.WorkRelated}
		items[row.Category] = item
	}
	item.Requests += row.Requests
	item.TotalTokens += row.TotalTokens
	item.ConfidenceSampleCount += row.ConfidenceSampleCount
	item.confidenceSum += row.ConfidenceSum
}
func addRelation(items map[string]*WorkDistributionRelation, row WorkDistributionAggregate) {
	item := items[row.WorkRelated]
	if item == nil {
		item = &WorkDistributionRelation{WorkRelated: row.WorkRelated}
		items[row.WorkRelated] = item
	}
	item.Requests += row.Requests
	item.TotalTokens += row.TotalTokens
}
func categorySliceMap(items []WorkDistributionCategory) map[string]*WorkDistributionCategory {
	result := map[string]*WorkDistributionCategory{}
	for i := range items {
		item := items[i]
		result[item.Category] = &item
	}
	return result
}
func categoryMapSlice(items map[string]*WorkDistributionCategory) []WorkDistributionCategory {
	result := make([]WorkDistributionCategory, 0, len(items))
	for _, item := range items {
		result = append(result, *item)
	}
	return result
}
func finalizeCategories(items []WorkDistributionCategory, metric string) []WorkDistributionCategory {
	var total int64
	for i := range items {
		items[i].Value = metricValue(metric, items[i].Requests, items[i].TotalTokens)
		total += items[i].Value
		items[i].AverageConfidence = averageConfidence(items[i].confidenceSum, items[i].ConfidenceSampleCount)
	}
	for i := range items {
		items[i].Percent = percentage(items[i].Value, total)
	}
	sort.Slice(items, func(i, j int) bool { return items[i].Value > items[j].Value })
	return items
}
func totalRelationValue(items map[string]*WorkDistributionRelation, metric string) int64 {
	var total int64
	for _, item := range items {
		total += metricValue(metric, item.Requests, item.TotalTokens)
	}
	return total
}
func metricValue(metric string, requests, tokens int64) int64 {
	if metric == WorkMetricTokens {
		return tokens
	}
	return requests
}
func percentage(value, total int64) float64 {
	if total <= 0 {
		return 0
	}
	return float64(value) * 100 / float64(total)
}
func averageConfidence(sum float64, count int64) *float64 {
	if count <= 0 {
		return nil
	}
	value := sum / float64(count)
	return &value
}
