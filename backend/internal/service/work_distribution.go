package service

import (
	"context"
	"sort"
	"strings"
	"time"

	infraerrors "github.com/Wei-Shaw/sub2api/internal/pkg/errors"
)

const (
	WorkMetricRequests = "requests"
	WorkMetricTokens   = "tokens"

	WorkRelatedWork      = "work"
	WorkRelatedNonWork   = "non_work"
	WorkRelatedUncertain = "uncertain"

	WorkCategoryCoding        = "coding"
	WorkCategoryDocumentation = "documentation"
	WorkCategoryDataAnalysis  = "data_analysis"
	WorkCategoryOperations    = "operations"
	WorkCategoryCommunication = "communication"
	WorkCategoryLearning      = "learning"
	WorkCategoryOther         = "other"
	WorkCategoryUnclassified  = "unclassified"
	WorkCategoryNonWork       = "non_work"

	WorkReviewPending  = "pending"
	WorkReviewApproved = "approved"
	WorkReviewRejected = "rejected"
)

var (
	ErrWorkUsageNotFound  = infraerrors.NotFound("WORK_USAGE_NOT_FOUND", "usage record not found")
	ErrWorkReviewNotFound = infraerrors.NotFound("WORK_REVIEW_NOT_FOUND", "work classification review not found")
	ErrWorkReviewConflict = infraerrors.Conflict("WORK_REVIEW_CONFLICT", "a pending review already exists for this usage record")
	ErrWorkReviewResolved = infraerrors.Conflict("WORK_REVIEW_RESOLVED", "work classification review is already resolved")
)

var validWorkCategories = map[string]struct{}{
	WorkCategoryCoding: {}, WorkCategoryDocumentation: {}, WorkCategoryDataAnalysis: {},
	WorkCategoryOperations: {}, WorkCategoryCommunication: {}, WorkCategoryLearning: {},
	WorkCategoryOther: {}, WorkCategoryUnclassified: {}, WorkCategoryNonWork: {},
}

var validWorkRelated = map[string]struct{}{
	WorkRelatedWork: {}, WorkRelatedNonWork: {}, WorkRelatedUncertain: {},
}

var validWorkReviewReasons = map[string]struct{}{
	"incorrect_category": {}, "incorrect_work_relation": {}, "missing_classification": {}, "other": {},
}

type WorkDistributionFilter struct {
	StartTime  time.Time
	EndTime    time.Time
	UserID     int64
	Department string
	Role       string
}

type WorkDistributionSummaryFilter struct {
	WorkDistributionFilter
	Metric        string
	MinSampleSize int64
	MinCohortSize int64
	UserLimit     int
}

type WorkDistributionRecordFilter struct {
	WorkDistributionFilter
	Category      string
	WorkRelated   string
	ReviewStatus  string
	MinSampleSize int64
	MinCohortSize int64
	Page          int
	PageSize      int
}

type WorkDistributionReviewFilter struct {
	Status   string
	UserID   int64
	Page     int
	PageSize int
}

type WorkDistributionMetadata struct {
	ProjectRef     string `json:"project_ref,omitempty"`
	RepositoryRef  string `json:"repository_ref,omitempty"`
	SubmissionType string `json:"submission_type,omitempty"`
	Department     string `json:"department,omitempty"`
	Role           string `json:"role,omitempty"`
	Source         string `json:"source"`
}

type WorkDistributionClassification struct {
	WorkRelated          string    `json:"work_related"`
	Category             string    `json:"category"`
	Weight               int64     `json:"weight"`
	Confidence           *float64  `json:"confidence,omitempty"`
	ClassificationSource string    `json:"classification_source"`
	ClassifierVersion    string    `json:"classifier_version,omitempty"`
	UpdatedAt            time.Time `json:"updated_at"`
}

type WorkDistributionRecord struct {
	UsageLogID     int64                           `json:"usage_log_id"`
	UserID         int64                           `json:"user_id"`
	Email          string                          `json:"email"`
	Department     string                          `json:"department"`
	Role           string                          `json:"role"`
	TotalTokens    int64                           `json:"total_tokens"`
	CreatedAt      time.Time                       `json:"created_at"`
	Metadata       *WorkDistributionMetadata       `json:"metadata,omitempty"`
	Classification *WorkDistributionClassification `json:"classification,omitempty"`
	ReviewStatus   string                          `json:"review_status,omitempty"`
}

type WorkDistributionAggregate struct {
	UserID                int64
	Email                 string
	Department            string
	Role                  string
	WorkRelated           string
	Category              string
	Classified            bool
	Requests              int64
	TotalTokens           int64
	ConfidenceSum         float64
	ConfidenceSampleCount int64
	DepartmentCohortSize  int64
	ScopeCohortSize       int64
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
	WorkRelated           string   `json:"work_related"`
	Requests              int64    `json:"requests"`
	TotalTokens           int64    `json:"total_tokens"`
	Value                 int64    `json:"value"`
	Percent               float64  `json:"percent"`
	AverageConfidence     *float64 `json:"average_confidence,omitempty"`
	ConfidenceSampleCount int64    `json:"confidence_sample_count"`
	confidenceSum         float64
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

type WorkDistributionCoverage struct {
	TotalRequests        int64   `json:"total_requests"`
	ClassifiedRequests   int64   `json:"classified_requests"`
	UnclassifiedRequests int64   `json:"unclassified_requests"`
	ClassifiedPercent    float64 `json:"classified_percent"`
}

type WorkDistributionPrivacy struct {
	MinSampleSize   int64 `json:"min_sample_size"`
	MinCohortSize   int64 `json:"min_cohort_size"`
	SuppressedUsers int   `json:"suppressed_users"`
}

type WorkDistributionRole struct {
	Role      string `json:"role"`
	UserCount int64  `json:"user_count"`
}

type WorkDistributionSummary struct {
	GeneratedAt           time.Time                    `json:"generated_at"`
	StartDate             string                       `json:"start_date"`
	EndDate               string                       `json:"end_date"`
	Metric                string                       `json:"metric"`
	CollectionStatus      string                       `json:"collection_status"`
	AverageConfidence     *float64                     `json:"average_confidence,omitempty"`
	ConfidenceSampleCount int64                        `json:"confidence_sample_count"`
	Privacy               WorkDistributionPrivacy      `json:"privacy"`
	Coverage              WorkDistributionCoverage     `json:"coverage"`
	WorkRelated           []WorkDistributionRelation   `json:"work_related"`
	Categories            []WorkDistributionCategory   `json:"categories"`
	Departments           []WorkDistributionDepartment `json:"departments"`
	Roles                 []WorkDistributionRole       `json:"roles"`
	Users                 []WorkDistributionUser       `json:"users"`
}

type CreateWorkReviewInput struct {
	UsageLogID  int64
	OwnerUserID int64
	WorkRelated string
	Category    string
	ReasonCode  string
	RequestedBy int64
}

type ResolveWorkReviewInput struct {
	ReviewID       int64
	Decision       string
	ResolutionNote string
	ResolvedBy     int64
}

type WorkDistributionReview struct {
	ID                  int64      `json:"id"`
	UsageLogID          int64      `json:"usage_log_id"`
	UserID              int64      `json:"user_id"`
	Email               string     `json:"email"`
	PreviousWorkRelated *string    `json:"previous_work_related,omitempty"`
	PreviousCategory    *string    `json:"previous_category,omitempty"`
	ProposedWorkRelated string     `json:"proposed_work_related"`
	ProposedCategory    string     `json:"proposed_category"`
	ReasonCode          string     `json:"reason_code"`
	Status              string     `json:"status"`
	ResolutionNote      string     `json:"resolution_note,omitempty"`
	RequestedBy         *int64     `json:"requested_by,omitempty"`
	ResolvedBy          *int64     `json:"resolved_by,omitempty"`
	CreatedAt           time.Time  `json:"created_at"`
	ResolvedAt          *time.Time `json:"resolved_at,omitempty"`
}

type WorkDistributionRepository interface {
	GetAggregates(ctx context.Context, filter WorkDistributionFilter) ([]WorkDistributionAggregate, error)
	ListRecords(ctx context.Context, filter WorkDistributionRecordFilter) ([]WorkDistributionRecord, int64, error)
	ListUserClassifications(ctx context.Context, userID int64, page, pageSize int) ([]WorkDistributionRecord, int64, error)
	CreateReview(ctx context.Context, input CreateWorkReviewInput) (*WorkDistributionReview, error)
	ListReviews(ctx context.Context, filter WorkDistributionReviewFilter) ([]WorkDistributionReview, int64, error)
	ResolveReview(ctx context.Context, input ResolveWorkReviewInput) (*WorkDistributionReview, error)
}

type WorkDistributionService struct {
	repo WorkDistributionRepository
}

func NewWorkDistributionService(repo WorkDistributionRepository) *WorkDistributionService {
	return &WorkDistributionService{repo: repo}
}

func (s *WorkDistributionService) GetSummary(ctx context.Context, filter WorkDistributionSummaryFilter) (*WorkDistributionSummary, error) {
	if err := normalizeSummaryFilter(&filter); err != nil {
		return nil, err
	}
	rows, err := s.repo.GetAggregates(ctx, filter.WorkDistributionFilter)
	if err != nil {
		return nil, err
	}

	result := &WorkDistributionSummary{
		GeneratedAt:      time.Now().UTC(),
		StartDate:        filter.StartTime.Format("2006-01-02"),
		EndDate:          filter.EndTime.Add(-time.Nanosecond).Format("2006-01-02"),
		Metric:           filter.Metric,
		CollectionStatus: "no_data",
		Privacy:          WorkDistributionPrivacy{MinSampleSize: filter.MinSampleSize, MinCohortSize: filter.MinCohortSize},
		Categories:       []WorkDistributionCategory{},
		Departments:      []WorkDistributionDepartment{},
		Roles:            []WorkDistributionRole{},
		Users:            []WorkDistributionUser{},
		WorkRelated:      []WorkDistributionRelation{},
	}
	if filter.UserID > 0 {
		var selectedRequests int64
		var selectedCohort int64
		for _, row := range rows {
			selectedRequests += row.Requests
			if selectedCohort == 0 || row.DepartmentCohortSize < selectedCohort {
				selectedCohort = row.DepartmentCohortSize
			}
		}
		if selectedRequests < filter.MinSampleSize || selectedCohort < filter.MinCohortSize {
			result.Privacy.SuppressedUsers = 1
			return result, nil
		}
	}
	if strings.TrimSpace(filter.Department) != "" || strings.TrimSpace(filter.Role) != "" {
		var scopeCohort int64
		for _, row := range rows {
			if scopeCohort == 0 || (row.ScopeCohortSize > 0 && row.ScopeCohortSize < scopeCohort) {
				scopeCohort = row.ScopeCohortSize
			}
		}
		if scopeCohort < filter.MinCohortSize {
			return result, nil
		}
	}

	// Departments below the cohort threshold are never exposed by name. They
	// may contribute only as one pooled cohort when that pool independently
	// contains enough users; otherwise their rows are excluded from top-level
	// totals as well to prevent differencing attacks.
	smallDepartmentUsers := map[int64]struct{}{}
	for _, row := range rows {
		if row.DepartmentCohortSize < filter.MinCohortSize {
			smallDepartmentUsers[row.UserID] = struct{}{}
		}
	}
	includeSmallDepartmentPool := int64(len(smallDepartmentUsers)) >= filter.MinCohortSize
	if !includeSmallDepartmentPool {
		result.Privacy.SuppressedUsers = len(smallDepartmentUsers)
	}

	categoryMap := map[string]*WorkDistributionCategory{}
	relationMap := map[string]*WorkDistributionRelation{}
	departmentMap := map[string]*WorkDistributionDepartment{}
	userMap := map[int64]*WorkDistributionUser{}
	userCohortSizes := map[int64]int64{}
	roleUsers := map[string]map[int64]struct{}{}
	roleLabels := map[string]string{}
	var resultConfidenceSum float64
	for _, row := range rows {
		isSmallDepartment := row.DepartmentCohortSize < filter.MinCohortSize
		if isSmallDepartment && !includeSmallDepartmentPool {
			continue
		}
		if current := userCohortSizes[row.UserID]; current == 0 || row.DepartmentCohortSize < current {
			userCohortSizes[row.UserID] = row.DepartmentCohortSize
		}
		role := strings.TrimSpace(row.Role)
		if role == "" {
			role = "unknown"
		}
		roleKey := strings.ToLower(role)
		if roleUsers[roleKey] == nil {
			roleUsers[roleKey] = map[int64]struct{}{}
			roleLabels[roleKey] = role
		}
		roleUsers[roleKey][row.UserID] = struct{}{}
		result.Coverage.TotalRequests += row.Requests
		if row.Classified {
			result.Coverage.ClassifiedRequests += row.Requests
		}
		result.ConfidenceSampleCount += row.ConfidenceSampleCount
		resultConfidenceSum += row.ConfidenceSum
		addWorkCategory(categoryMap, row.WorkRelated, row.Category, row.Requests, row.TotalTokens, row.ConfidenceSum, row.ConfidenceSampleCount)
		addWorkRelation(relationMap, row.WorkRelated, row.Requests, row.TotalTokens, row.ConfidenceSum, row.ConfidenceSampleCount)

		department := row.Department
		if department == "" {
			department = "unknown"
		}
		if isSmallDepartment {
			department = "other_departments"
		}
		dep := departmentMap[department]
		if dep == nil {
			dep = &WorkDistributionDepartment{Department: department, Categories: []WorkDistributionCategory{}}
			departmentMap[department] = dep
		}
		dep.Requests += row.Requests
		dep.TotalTokens += row.TotalTokens
		dep.confidenceSum += row.ConfidenceSum
		dep.ConfidenceSampleCount += row.ConfidenceSampleCount
		depCategoryMap := categorySliceToMap(dep.Categories)
		addWorkCategory(depCategoryMap, row.WorkRelated, row.Category, row.Requests, row.TotalTokens, row.ConfidenceSum, row.ConfidenceSampleCount)
		dep.Categories = categoryMapToSlice(depCategoryMap)

		user := userMap[row.UserID]
		if user == nil {
			user = &WorkDistributionUser{UserID: row.UserID, Email: row.Email, Department: department, Role: row.Role, Categories: []WorkDistributionCategory{}}
			userMap[row.UserID] = user
		}
		user.Requests += row.Requests
		user.TotalTokens += row.TotalTokens
		user.confidenceSum += row.ConfidenceSum
		user.ConfidenceSampleCount += row.ConfidenceSampleCount
		userCategoryMap := categorySliceToMap(user.Categories)
		addWorkCategory(userCategoryMap, row.WorkRelated, row.Category, row.Requests, row.TotalTokens, row.ConfidenceSum, row.ConfidenceSampleCount)
		user.Categories = categoryMapToSlice(userCategoryMap)
	}
	result.Coverage.UnclassifiedRequests = result.Coverage.TotalRequests - result.Coverage.ClassifiedRequests
	result.Coverage.ClassifiedPercent = percentage(result.Coverage.ClassifiedRequests, result.Coverage.TotalRequests)
	if result.Coverage.TotalRequests > 0 {
		result.CollectionStatus = "active"
	}
	result.AverageConfidence = averageConfidence(resultConfidenceSum, result.ConfidenceSampleCount)

	result.Categories = finalizeCategories(categoryMapToSlice(categoryMap), filter.Metric)
	result.WorkRelated = finalizeRelations(relationMap, filter.Metric)
	for key, users := range roleUsers {
		if int64(len(users)) < filter.MinCohortSize {
			continue
		}
		result.Roles = append(result.Roles, WorkDistributionRole{
			Role: roleLabels[key], UserCount: int64(len(users)),
		})
	}
	sort.Slice(result.Roles, func(i, j int) bool {
		if result.Roles[i].UserCount == result.Roles[j].UserCount {
			return strings.ToLower(result.Roles[i].Role) < strings.ToLower(result.Roles[j].Role)
		}
		return result.Roles[i].UserCount > result.Roles[j].UserCount
	})
	for _, dep := range departmentMap {
		dep.Value = workMetricValue(filter.Metric, dep.Requests, dep.TotalTokens)
		dep.AverageConfidence = averageConfidence(dep.confidenceSum, dep.ConfidenceSampleCount)
		dep.Categories = finalizeCategories(dep.Categories, filter.Metric)
		result.Departments = append(result.Departments, *dep)
	}
	sort.Slice(result.Departments, func(i, j int) bool { return result.Departments[i].Value > result.Departments[j].Value })

	for _, user := range userMap {
		if user.Requests < filter.MinSampleSize || userCohortSizes[user.UserID] < filter.MinCohortSize {
			result.Privacy.SuppressedUsers++
			continue
		}
		user.Value = workMetricValue(filter.Metric, user.Requests, user.TotalTokens)
		user.AverageConfidence = averageConfidence(user.confidenceSum, user.ConfidenceSampleCount)
		user.Categories = finalizeCategories(user.Categories, filter.Metric)
		result.Users = append(result.Users, *user)
	}
	sort.Slice(result.Users, func(i, j int) bool { return result.Users[i].Value > result.Users[j].Value })
	if len(result.Users) > filter.UserLimit {
		result.Users = result.Users[:filter.UserLimit]
	}
	return result, nil
}

func (s *WorkDistributionService) ListRecords(ctx context.Context, filter WorkDistributionRecordFilter) ([]WorkDistributionRecord, int64, error) {
	if err := normalizeRecordFilter(&filter); err != nil {
		return nil, 0, err
	}
	return s.repo.ListRecords(ctx, filter)
}

func (s *WorkDistributionService) ListOwnClassifications(ctx context.Context, userID int64, page, pageSize int) ([]WorkDistributionRecord, int64, error) {
	if userID <= 0 {
		return nil, 0, infraerrors.BadRequest("INVALID_WORK_CLASSIFICATION_USER", "invalid user")
	}
	normalizePage(&page, &pageSize)
	return s.repo.ListUserClassifications(ctx, userID, page, pageSize)
}

func (s *WorkDistributionService) CreateAppeal(ctx context.Context, userID int64, input CreateWorkReviewInput) (*WorkDistributionReview, error) {
	if userID <= 0 {
		return nil, infraerrors.BadRequest("INVALID_WORK_CLASSIFICATION_USER", "invalid user")
	}
	input.OwnerUserID = userID
	input.RequestedBy = userID
	return s.CreateCorrection(ctx, input)
}

func (s *WorkDistributionService) CreateCorrection(ctx context.Context, input CreateWorkReviewInput) (*WorkDistributionReview, error) {
	input.WorkRelated = strings.TrimSpace(input.WorkRelated)
	input.Category = strings.TrimSpace(input.Category)
	input.ReasonCode = strings.TrimSpace(input.ReasonCode)
	if input.UsageLogID <= 0 || input.RequestedBy <= 0 || !isValidWorkRelated(input.WorkRelated) || !isValidWorkCategory(input.Category) {
		return nil, infraerrors.BadRequest("INVALID_WORK_CORRECTION", "invalid work classification correction")
	}
	if _, ok := validWorkReviewReasons[input.ReasonCode]; !ok {
		return nil, infraerrors.BadRequest("INVALID_WORK_REVIEW_REASON", "invalid work classification review reason")
	}
	if !isValidWorkClassification(input.WorkRelated, input.Category) {
		return nil, infraerrors.BadRequest("INVALID_WORK_CLASSIFICATION", "invalid work relation and category combination")
	}
	return s.repo.CreateReview(ctx, input)
}

func (s *WorkDistributionService) ListReviews(ctx context.Context, filter WorkDistributionReviewFilter) ([]WorkDistributionReview, int64, error) {
	filter.Status = strings.TrimSpace(filter.Status)
	if filter.Status != "" && filter.Status != WorkReviewPending && filter.Status != WorkReviewApproved && filter.Status != WorkReviewRejected {
		return nil, 0, infraerrors.BadRequest("INVALID_WORK_REVIEW_STATUS", "invalid work classification review status")
	}
	normalizePage(&filter.Page, &filter.PageSize)
	return s.repo.ListReviews(ctx, filter)
}

func (s *WorkDistributionService) ResolveReview(ctx context.Context, input ResolveWorkReviewInput) (*WorkDistributionReview, error) {
	input.Decision = strings.TrimSpace(input.Decision)
	input.ResolutionNote = strings.TrimSpace(input.ResolutionNote)
	if input.ReviewID <= 0 || input.ResolvedBy <= 0 || (input.Decision != WorkReviewApproved && input.Decision != WorkReviewRejected) || !isValidWorkResolutionNote(input.Decision, input.ResolutionNote) {
		return nil, infraerrors.BadRequest("INVALID_WORK_REVIEW_RESOLUTION", "invalid work classification review resolution")
	}
	return s.repo.ResolveReview(ctx, input)
}

func normalizeSummaryFilter(filter *WorkDistributionSummaryFilter) error {
	if filter.EndTime.IsZero() || filter.StartTime.IsZero() || !filter.EndTime.After(filter.StartTime) {
		return infraerrors.BadRequest("INVALID_WORK_DISTRIBUTION_RANGE", "invalid work distribution date range")
	}
	filter.Metric = strings.TrimSpace(filter.Metric)
	if filter.Metric == "" {
		filter.Metric = WorkMetricRequests
	}
	if filter.Metric != WorkMetricRequests && filter.Metric != WorkMetricTokens {
		return infraerrors.BadRequest("INVALID_WORK_DISTRIBUTION_METRIC", "metric must be requests or tokens")
	}
	if filter.MinSampleSize < 5 {
		filter.MinSampleSize = 5
	}
	if filter.MinSampleSize > 1000 {
		filter.MinSampleSize = 1000
	}
	if filter.MinCohortSize < 5 {
		filter.MinCohortSize = 5
	}
	if filter.MinCohortSize > 1000 {
		filter.MinCohortSize = 1000
	}
	if filter.UserLimit <= 0 {
		filter.UserLimit = 100
	}
	if filter.UserLimit > 500 {
		filter.UserLimit = 500
	}
	return nil
}

func normalizeRecordFilter(filter *WorkDistributionRecordFilter) error {
	if filter.EndTime.IsZero() || filter.StartTime.IsZero() || !filter.EndTime.After(filter.StartTime) {
		return infraerrors.BadRequest("INVALID_WORK_DISTRIBUTION_RANGE", "invalid work distribution date range")
	}
	filter.Category = strings.TrimSpace(filter.Category)
	filter.WorkRelated = strings.TrimSpace(filter.WorkRelated)
	filter.ReviewStatus = strings.TrimSpace(filter.ReviewStatus)
	if filter.MinSampleSize < 5 {
		filter.MinSampleSize = 5
	}
	if filter.MinSampleSize > 1000 {
		filter.MinSampleSize = 1000
	}
	if filter.MinCohortSize < 5 {
		filter.MinCohortSize = 5
	}
	if filter.MinCohortSize > 1000 {
		filter.MinCohortSize = 1000
	}
	if filter.Category != "" && !isValidWorkCategory(filter.Category) {
		return infraerrors.BadRequest("INVALID_WORK_CATEGORY", "invalid work category")
	}
	if filter.WorkRelated != "" && !isValidWorkRelated(filter.WorkRelated) {
		return infraerrors.BadRequest("INVALID_WORK_RELATION", "invalid work relation")
	}
	if filter.ReviewStatus != "" && filter.ReviewStatus != WorkReviewPending && filter.ReviewStatus != WorkReviewApproved && filter.ReviewStatus != WorkReviewRejected {
		return infraerrors.BadRequest("INVALID_WORK_REVIEW_STATUS", "invalid work classification review status")
	}
	normalizePage(&filter.Page, &filter.PageSize)
	return nil
}

func normalizePage(page, pageSize *int) {
	if *page <= 0 {
		*page = 1
	}
	if *pageSize <= 0 {
		*pageSize = 20
	}
	if *pageSize > 200 {
		*pageSize = 200
	}
}

func isValidWorkCategory(value string) bool { _, ok := validWorkCategories[value]; return ok }
func isValidWorkRelated(value string) bool  { _, ok := validWorkRelated[value]; return ok }

func isValidWorkClassification(workRelated, category string) bool {
	switch workRelated {
	case WorkRelatedWork:
		return category != WorkCategoryNonWork && category != WorkCategoryUnclassified && isValidWorkCategory(category)
	case WorkRelatedNonWork:
		return category == WorkCategoryNonWork
	case WorkRelatedUncertain:
		return category == WorkCategoryUnclassified
	default:
		return false
	}
}

func isValidWorkResolutionNote(decision, value string) bool {
	if value == "other" {
		return true
	}
	if decision == WorkReviewApproved {
		return value == "confirmed_correction"
	}
	if decision == WorkReviewRejected {
		switch value {
		case "insufficient_evidence", "duplicate", "invalid_request":
			return true
		}
	}
	return false
}

func workCategoryKey(workRelated, category string) string { return workRelated + "\x00" + category }

func addWorkCategory(items map[string]*WorkDistributionCategory, related, category string, requests, tokens int64, confidenceSum float64, confidenceSamples int64) {
	key := workCategoryKey(related, category)
	item := items[key]
	if item == nil {
		item = &WorkDistributionCategory{WorkRelated: related, Category: category}
		items[key] = item
	}
	item.Requests += requests
	item.TotalTokens += tokens
	item.confidenceSum += confidenceSum
	item.ConfidenceSampleCount += confidenceSamples
}

func addWorkRelation(items map[string]*WorkDistributionRelation, related string, requests, tokens int64, confidenceSum float64, confidenceSamples int64) {
	item := items[related]
	if item == nil {
		item = &WorkDistributionRelation{WorkRelated: related}
		items[related] = item
	}
	item.Requests += requests
	item.TotalTokens += tokens
	item.confidenceSum += confidenceSum
	item.ConfidenceSampleCount += confidenceSamples
}

func categorySliceToMap(items []WorkDistributionCategory) map[string]*WorkDistributionCategory {
	out := make(map[string]*WorkDistributionCategory, len(items))
	for i := range items {
		item := items[i]
		out[workCategoryKey(item.WorkRelated, item.Category)] = &item
	}
	return out
}

func categoryMapToSlice(items map[string]*WorkDistributionCategory) []WorkDistributionCategory {
	out := make([]WorkDistributionCategory, 0, len(items))
	for _, item := range items {
		out = append(out, *item)
	}
	return out
}

func finalizeCategories(items []WorkDistributionCategory, metric string) []WorkDistributionCategory {
	var total int64
	for i := range items {
		items[i].Value = workMetricValue(metric, items[i].Requests, items[i].TotalTokens)
		items[i].AverageConfidence = averageConfidence(items[i].confidenceSum, items[i].ConfidenceSampleCount)
		total += items[i].Value
	}
	for i := range items {
		items[i].Percent = percentage(items[i].Value, total)
	}
	sort.Slice(items, func(i, j int) bool { return items[i].Value > items[j].Value })
	return items
}

func finalizeRelations(items map[string]*WorkDistributionRelation, metric string) []WorkDistributionRelation {
	out := make([]WorkDistributionRelation, 0, len(items))
	var total int64
	for _, item := range items {
		item.Value = workMetricValue(metric, item.Requests, item.TotalTokens)
		item.AverageConfidence = averageConfidence(item.confidenceSum, item.ConfidenceSampleCount)
		total += item.Value
		out = append(out, *item)
	}
	for i := range out {
		out[i].Percent = percentage(out[i].Value, total)
	}
	sort.Slice(out, func(i, j int) bool { return out[i].Value > out[j].Value })
	return out
}

func averageConfidence(sum float64, count int64) *float64 {
	if count <= 0 {
		return nil
	}
	value := sum / float64(count)
	return &value
}

func workMetricValue(metric string, requests, tokens int64) int64 {
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
