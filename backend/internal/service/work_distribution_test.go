package service

import (
	"context"
	"testing"
	"time"

	"github.com/stretchr/testify/require"
)

type workDistributionRepoStub struct {
	aggregates   []WorkDistributionAggregate
	recordFilter WorkDistributionRecordFilter
	createInput  CreateWorkReviewInput
	resolveInput ResolveWorkReviewInput
}

func (s *workDistributionRepoStub) GetAggregates(context.Context, WorkDistributionFilter) ([]WorkDistributionAggregate, error) {
	return s.aggregates, nil
}
func (s *workDistributionRepoStub) ListRecords(_ context.Context, filter WorkDistributionRecordFilter) ([]WorkDistributionRecord, int64, error) {
	s.recordFilter = filter
	return []WorkDistributionRecord{}, 0, nil
}
func (s *workDistributionRepoStub) ListUserClassifications(context.Context, int64, int, int) ([]WorkDistributionRecord, int64, error) {
	return []WorkDistributionRecord{}, 0, nil
}
func (s *workDistributionRepoStub) CreateReview(_ context.Context, input CreateWorkReviewInput) (*WorkDistributionReview, error) {
	s.createInput = input
	return &WorkDistributionReview{UsageLogID: input.UsageLogID}, nil
}
func (s *workDistributionRepoStub) ListReviews(context.Context, WorkDistributionReviewFilter) ([]WorkDistributionReview, int64, error) {
	return []WorkDistributionReview{}, 0, nil
}

func (s *workDistributionRepoStub) ResolveReview(_ context.Context, input ResolveWorkReviewInput) (*WorkDistributionReview, error) {
	s.resolveInput = input
	return &WorkDistributionReview{}, nil
}

func workSummaryRange() (time.Time, time.Time) {
	start := time.Date(2026, 8, 1, 0, 0, 0, 0, time.UTC)
	return start, start.Add(24 * time.Hour)
}

func TestWorkDistributionSummarySuppressesLargeSingleUserCohort(t *testing.T) {
	repo := &workDistributionRepoStub{aggregates: []WorkDistributionAggregate{{
		UserID: 7, Email: "private@example.com", Department: "研发", Role: "user",
		WorkRelated: WorkRelatedWork, Category: WorkCategoryCoding, Classified: true,
		Requests: 100, TotalTokens: 8000, DepartmentCohortSize: 1,
	}}}
	start, end := workSummaryRange()
	result, err := NewWorkDistributionService(repo).GetSummary(context.Background(), WorkDistributionSummaryFilter{
		WorkDistributionFilter: WorkDistributionFilter{StartTime: start, EndTime: end, UserID: 7},
		MinSampleSize:          1, MinCohortSize: 1,
	})
	require.NoError(t, err)
	require.Empty(t, result.Users, "one person with many requests must still be suppressed")
	require.Equal(t, 1, result.Privacy.SuppressedUsers)
	require.Equal(t, int64(5), result.Privacy.MinSampleSize)
	require.Equal(t, int64(5), result.Privacy.MinCohortSize)
	require.Empty(t, result.Departments, "a filtered low-cohort user must not leak through department totals")
	require.Empty(t, result.Categories)
	require.Empty(t, result.WorkRelated)
	require.Zero(t, result.Coverage.TotalRequests)
}

func TestWorkDistributionSummaryConfidenceAndCoverageExcludeUnclassified(t *testing.T) {
	repo := &workDistributionRepoStub{aggregates: []WorkDistributionAggregate{
		{UserID: 1, Department: "研发", WorkRelated: WorkRelatedWork, Category: WorkCategoryCoding, Classified: true, Requests: 5, TotalTokens: 50, ConfidenceSum: 4.5, ConfidenceSampleCount: 5, DepartmentCohortSize: 5},
		{UserID: 2, Department: "研发", WorkRelated: WorkRelatedUncertain, Category: WorkCategoryUnclassified, Classified: false, Requests: 5, TotalTokens: 20, DepartmentCohortSize: 5},
	}}
	start, end := workSummaryRange()
	result, err := NewWorkDistributionService(repo).GetSummary(context.Background(), WorkDistributionSummaryFilter{
		WorkDistributionFilter: WorkDistributionFilter{StartTime: start, EndTime: end}, Metric: WorkMetricTokens,
	})
	require.NoError(t, err)
	require.Equal(t, "active", result.CollectionStatus)
	require.Equal(t, int64(10), result.Coverage.TotalRequests)
	require.Equal(t, int64(5), result.Coverage.ClassifiedRequests)
	require.Equal(t, int64(5), result.Coverage.UnclassifiedRequests)
	require.NotNil(t, result.AverageConfidence)
	require.InDelta(t, 0.9, *result.AverageConfidence, 0.0001)
	require.Equal(t, int64(5), result.ConfidenceSampleCount)
	require.NotEmpty(t, result.WorkRelated)
}

func TestWorkDistributionRecordsClampBothPrivacyThresholds(t *testing.T) {
	repo := &workDistributionRepoStub{}
	start, end := workSummaryRange()
	_, _, err := NewWorkDistributionService(repo).ListRecords(context.Background(), WorkDistributionRecordFilter{
		WorkDistributionFilter: WorkDistributionFilter{StartTime: start, EndTime: end},
		MinSampleSize:          1, MinCohortSize: 1,
	})
	require.NoError(t, err)
	require.Equal(t, int64(5), repo.recordFilter.MinSampleSize)
	require.Equal(t, int64(5), repo.recordFilter.MinCohortSize)
}

func TestCreateWorkAppealPinsOwnerAndUsesStructuredReason(t *testing.T) {
	repo := &workDistributionRepoStub{}
	_, err := NewWorkDistributionService(repo).CreateAppeal(context.Background(), 9, CreateWorkReviewInput{
		UsageLogID: 42, WorkRelated: WorkRelatedWork, Category: WorkCategoryDocumentation,
		ReasonCode: "incorrect_category",
	})
	require.NoError(t, err)
	require.Equal(t, int64(9), repo.createInput.OwnerUserID)
	require.Equal(t, int64(9), repo.createInput.RequestedBy)

	_, err = NewWorkDistributionService(repo).CreateAppeal(context.Background(), 9, CreateWorkReviewInput{
		UsageLogID: 42, WorkRelated: WorkRelatedWork, Category: WorkCategoryDocumentation,
		ReasonCode: "free form prompt text",
	})
	require.Error(t, err)
}

func TestWorkDistributionSummarySuppressesFilteredScopeBelowCohort(t *testing.T) {
	repo := &workDistributionRepoStub{aggregates: []WorkDistributionAggregate{{
		UserID: 1, Department: "研发", Role: "contractor", WorkRelated: WorkRelatedWork,
		Category: WorkCategoryCoding, Classified: true, Requests: 20, TotalTokens: 200,
		DepartmentCohortSize: 4, ScopeCohortSize: 4,
	}}}
	start, end := workSummaryRange()
	result, err := NewWorkDistributionService(repo).GetSummary(context.Background(), WorkDistributionSummaryFilter{
		WorkDistributionFilter: WorkDistributionFilter{StartTime: start, EndTime: end, Role: "contractor"},
	})
	require.NoError(t, err)
	require.Zero(t, result.Coverage.TotalRequests)
	require.Empty(t, result.Categories)
	require.Empty(t, result.Departments)
	require.Empty(t, result.Users)
}

func TestWorkDistributionSummaryExcludesUnpoolableSmallDepartmentsFromTotals(t *testing.T) {
	rows := make([]WorkDistributionAggregate, 0, 4)
	for userID := int64(1); userID <= 4; userID++ {
		department := "甲"
		if userID > 2 {
			department = "乙"
		}
		rows = append(rows, WorkDistributionAggregate{
			UserID: userID, Department: department, WorkRelated: WorkRelatedWork,
			Category: WorkCategoryCoding, Classified: true, Requests: 5, TotalTokens: 50,
			DepartmentCohortSize: 2, ScopeCohortSize: 4,
		})
	}
	start, end := workSummaryRange()
	result, err := NewWorkDistributionService(&workDistributionRepoStub{aggregates: rows}).GetSummary(context.Background(), WorkDistributionSummaryFilter{
		WorkDistributionFilter: WorkDistributionFilter{StartTime: start, EndTime: end},
	})
	require.NoError(t, err)
	require.Zero(t, result.Coverage.TotalRequests)
	require.Empty(t, result.Departments)
	require.Equal(t, 4, result.Privacy.SuppressedUsers)
}

func TestWorkDistributionSummaryPoolsSmallDepartmentsOnlyAsAnonymousCohort(t *testing.T) {
	rows := make([]WorkDistributionAggregate, 0, 6)
	for userID := int64(1); userID <= 6; userID++ {
		department := "甲"
		if userID > 3 {
			department = "乙"
		}
		rows = append(rows, WorkDistributionAggregate{
			UserID: userID, Department: department, WorkRelated: WorkRelatedWork,
			Category: WorkCategoryDocumentation, Classified: true, Requests: 5, TotalTokens: 50,
			DepartmentCohortSize: 3, ScopeCohortSize: 6,
		})
	}
	start, end := workSummaryRange()
	result, err := NewWorkDistributionService(&workDistributionRepoStub{aggregates: rows}).GetSummary(context.Background(), WorkDistributionSummaryFilter{
		WorkDistributionFilter: WorkDistributionFilter{StartTime: start, EndTime: end},
	})
	require.NoError(t, err)
	require.Equal(t, int64(30), result.Coverage.TotalRequests)
	require.Len(t, result.Departments, 1)
	require.Equal(t, "other_departments", result.Departments[0].Department)
	require.Empty(t, result.Users)
	require.Equal(t, 6, result.Privacy.SuppressedUsers)
}

func TestWorkDistributionSummaryRolesAreKAnonymousAndIndependentOfUserLimit(t *testing.T) {
	rows := make([]WorkDistributionAggregate, 0, 10)
	for userID := int64(1); userID <= 10; userID++ {
		role := "研发"
		if userID > 6 {
			role = "产品"
		}
		rows = append(rows, WorkDistributionAggregate{
			UserID: userID, Department: "技术中心", Role: role,
			WorkRelated: WorkRelatedWork, Category: WorkCategoryCoding, Classified: true,
			Requests: 1, TotalTokens: 50, DepartmentCohortSize: 10, ScopeCohortSize: 10,
		})
	}
	start, end := workSummaryRange()
	result, err := NewWorkDistributionService(&workDistributionRepoStub{aggregates: rows}).GetSummary(context.Background(), WorkDistributionSummaryFilter{
		WorkDistributionFilter: WorkDistributionFilter{StartTime: start, EndTime: end},
		MinSampleSize:          5, MinCohortSize: 5, UserLimit: 1,
	})
	require.NoError(t, err)
	require.Empty(t, result.Users, "role options must not depend on visible per-user rows")
	require.Equal(t, []WorkDistributionRole{{Role: "研发", UserCount: 6}}, result.Roles)
}

func TestWorkClassificationPairsAndResolutionNotesAreDecisionSpecific(t *testing.T) {
	repo := &workDistributionRepoStub{}
	svc := NewWorkDistributionService(repo)
	_, err := svc.CreateCorrection(context.Background(), CreateWorkReviewInput{
		UsageLogID: 1, RequestedBy: 9, WorkRelated: WorkRelatedWork,
		Category: WorkCategoryNonWork, ReasonCode: "incorrect_category",
	})
	require.Error(t, err)
	_, err = svc.CreateCorrection(context.Background(), CreateWorkReviewInput{
		UsageLogID: 1, RequestedBy: 9, WorkRelated: WorkRelatedNonWork,
		Category: WorkCategoryNonWork, ReasonCode: "incorrect_category",
	})
	require.NoError(t, err)

	_, err = svc.ResolveReview(context.Background(), ResolveWorkReviewInput{
		ReviewID: 1, ResolvedBy: 9, Decision: WorkReviewApproved, ResolutionNote: "insufficient_evidence",
	})
	require.Error(t, err)
	_, err = svc.ResolveReview(context.Background(), ResolveWorkReviewInput{
		ReviewID: 1, ResolvedBy: 9, Decision: WorkReviewRejected, ResolutionNote: "confirmed_correction",
	})
	require.Error(t, err)
	_, err = svc.ResolveReview(context.Background(), ResolveWorkReviewInput{
		ReviewID: 1, ResolvedBy: 9, Decision: WorkReviewApproved, ResolutionNote: "confirmed_correction",
	})
	require.NoError(t, err)
	require.Equal(t, "confirmed_correction", repo.resolveInput.ResolutionNote)
}
