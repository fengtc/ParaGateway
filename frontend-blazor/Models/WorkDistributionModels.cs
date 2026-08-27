using System.Text.Json.Serialization;

namespace ParaGateway.Frontend.Models;

public sealed class WorkDistributionSummaryQueryDto
{
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public long? UserId { get; set; }
    public string Department { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Metric { get; set; } = "requests";
    public int MinSampleSize { get; set; } = 5;
    public int MinCohortSize { get; set; } = 5;
    public int UserLimit { get; set; } = 100;
}

public sealed class WorkDistributionSummaryDto
{
    [JsonPropertyName("collection_status")] public string CollectionStatus { get; set; } = string.Empty;
    [JsonPropertyName("generated_at")] public DateTimeOffset GeneratedAt { get; set; }
    [JsonPropertyName("start_date")] public string StartDate { get; set; } = string.Empty;
    [JsonPropertyName("end_date")] public string EndDate { get; set; } = string.Empty;
    [JsonPropertyName("metric")] public string Metric { get; set; } = "requests";
    [JsonPropertyName("average_confidence")] public double? AverageConfidence { get; set; }
    [JsonPropertyName("confidence_sample_count")] public long ConfidenceSampleCount { get; set; }
    [JsonPropertyName("privacy")] public WorkDistributionPrivacyDto Privacy { get; set; } = new();
    [JsonPropertyName("coverage")] public WorkDistributionCoverageDto Coverage { get; set; } = new();
    [JsonPropertyName("work_related")] public List<WorkDistributionRelationDto> WorkRelated { get; set; } = [];
    [JsonPropertyName("categories")] public List<WorkDistributionCategoryDto> Categories { get; set; } = [];
    [JsonPropertyName("departments")] public List<WorkDistributionDepartmentDto> Departments { get; set; } = [];
    [JsonPropertyName("roles")] public List<WorkDistributionRoleDto> Roles { get; set; } = [];
    [JsonPropertyName("users")] public List<WorkDistributionUserDto> Users { get; set; } = [];
}

public sealed class WorkDistributionRoleDto
{
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
    [JsonPropertyName("user_count")] public long UserCount { get; set; }
}

public sealed class WorkDistributionPrivacyDto
{
    [JsonPropertyName("min_sample_size")] public long MinSampleSize { get; set; }
    [JsonPropertyName("min_cohort_size")] public long MinCohortSize { get; set; }
    [JsonPropertyName("suppressed_users")] public int SuppressedUsers { get; set; }
}

public sealed class WorkDistributionCoverageDto
{
    [JsonPropertyName("total_requests")] public long TotalRequests { get; set; }
    [JsonPropertyName("classified_requests")] public long ClassifiedRequests { get; set; }
    [JsonPropertyName("unclassified_requests")] public long UnclassifiedRequests { get; set; }
    [JsonPropertyName("classified_percent")] public double ClassifiedPercent { get; set; }
}

public sealed class WorkDistributionCategoryDto
{
    [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;
    [JsonPropertyName("work_related")] public string WorkRelated { get; set; } = string.Empty;
    [JsonPropertyName("requests")] public long Requests { get; set; }
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; set; }
    [JsonPropertyName("value")] public long Value { get; set; }
    [JsonPropertyName("percent")] public double Percent { get; set; }
    [JsonPropertyName("average_confidence")] public double? AverageConfidence { get; set; }
    [JsonPropertyName("confidence_sample_count")] public long ConfidenceSampleCount { get; set; }
}

public sealed class WorkDistributionRelationDto
{
    [JsonPropertyName("work_related")] public string WorkRelated { get; set; } = string.Empty;
    [JsonPropertyName("requests")] public long Requests { get; set; }
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; set; }
    [JsonPropertyName("value")] public long Value { get; set; }
    [JsonPropertyName("percent")] public double Percent { get; set; }
    [JsonPropertyName("average_confidence")] public double? AverageConfidence { get; set; }
    [JsonPropertyName("confidence_sample_count")] public long ConfidenceSampleCount { get; set; }
}

public sealed class WorkDistributionDepartmentDto
{
    [JsonPropertyName("department")] public string Department { get; set; } = string.Empty;
    [JsonPropertyName("requests")] public long Requests { get; set; }
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; set; }
    [JsonPropertyName("value")] public long Value { get; set; }
    [JsonPropertyName("average_confidence")] public double? AverageConfidence { get; set; }
    [JsonPropertyName("confidence_sample_count")] public long ConfidenceSampleCount { get; set; }
    [JsonPropertyName("categories")] public List<WorkDistributionCategoryDto> Categories { get; set; } = [];
}

public sealed class WorkDistributionUserDto
{
    [JsonPropertyName("user_id")] public long UserId { get; set; }
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("department")] public string Department { get; set; } = string.Empty;
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
    [JsonPropertyName("requests")] public long Requests { get; set; }
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; set; }
    [JsonPropertyName("value")] public long Value { get; set; }
    [JsonPropertyName("average_confidence")] public double? AverageConfidence { get; set; }
    [JsonPropertyName("confidence_sample_count")] public long ConfidenceSampleCount { get; set; }
    [JsonPropertyName("categories")] public List<WorkDistributionCategoryDto> Categories { get; set; } = [];
}

public sealed class WorkDistributionRecordQueryDto
{
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public long? UserId { get; set; }
    public string Department { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string WorkRelated { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = string.Empty;
    public int MinSampleSize { get; set; } = 5;
    public int MinCohortSize { get; set; } = 5;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class WorkDistributionRecordDto
{
    [JsonPropertyName("usage_log_id")] public long UsageLogId { get; set; }
    [JsonPropertyName("user_id")] public long UserId { get; set; }
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("department")] public string Department { get; set; } = string.Empty;
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [JsonPropertyName("metadata")] public WorkDistributionMetadataDto? Metadata { get; set; }
    [JsonPropertyName("classification")] public WorkDistributionClassificationDto? Classification { get; set; }
    [JsonPropertyName("review_status")] public string ReviewStatus { get; set; } = string.Empty;
}

public sealed class WorkDistributionMetadataDto
{
    [JsonPropertyName("project_ref")] public string ProjectRef { get; set; } = string.Empty;
    [JsonPropertyName("repository_ref")] public string RepositoryRef { get; set; } = string.Empty;
    [JsonPropertyName("submission_type")] public string SubmissionType { get; set; } = string.Empty;
    [JsonPropertyName("department")] public string Department { get; set; } = string.Empty;
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
    [JsonPropertyName("source")] public string Source { get; set; } = string.Empty;
}

public sealed class WorkDistributionClassificationDto
{
    [JsonPropertyName("work_related")] public string WorkRelated { get; set; } = string.Empty;
    [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;
    [JsonPropertyName("weight")] public long Weight { get; set; }
    [JsonPropertyName("confidence")] public double? Confidence { get; set; }
    [JsonPropertyName("classification_source")] public string ClassificationSource { get; set; } = string.Empty;
    [JsonPropertyName("classifier_version")] public string ClassifierVersion { get; set; } = string.Empty;
    [JsonPropertyName("updated_at")] public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class WorkDistributionReviewQueryDto
{
    public string Status { get; set; } = "pending";
    public long? UserId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class WorkDistributionReviewDto
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("usage_log_id")] public long UsageLogId { get; set; }
    [JsonPropertyName("user_id")] public long UserId { get; set; }
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("previous_work_related")] public string? PreviousWorkRelated { get; set; }
    [JsonPropertyName("previous_category")] public string? PreviousCategory { get; set; }
    [JsonPropertyName("proposed_work_related")] public string ProposedWorkRelated { get; set; } = string.Empty;
    [JsonPropertyName("proposed_category")] public string ProposedCategory { get; set; } = string.Empty;
    [JsonPropertyName("reason_code")] public string ReasonCode { get; set; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("resolution_note")] public string ResolutionNote { get; set; } = string.Empty;
    [JsonPropertyName("requested_by")] public long? RequestedBy { get; set; }
    [JsonPropertyName("resolved_by")] public long? ResolvedBy { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [JsonPropertyName("resolved_at")] public DateTimeOffset? ResolvedAt { get; set; }
}

// Personal work-classification DTOs intentionally contain structured metadata only.
// Prompt text, request bodies, source code, and credentials are not part of this contract.
public sealed class UserWorkClassificationDto
{
    [JsonPropertyName("usage_log_id")] public long UsageLogId { get; set; }
    [JsonPropertyName("user_id")] public long UserId { get; set; }
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("department")] public string Department { get; set; } = string.Empty;
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [JsonPropertyName("metadata")] public WorkDistributionMetadataDto? Metadata { get; set; }
    [JsonPropertyName("classification")] public WorkDistributionClassificationDto? Classification { get; set; }
    [JsonPropertyName("review_status")] public string ReviewStatus { get; set; } = string.Empty;
}

public sealed class UserWorkClassificationAppealRequestDto
{
    [JsonPropertyName("work_related")] public string WorkRelated { get; set; } = string.Empty;
    [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;
    [JsonPropertyName("reason_code")] public string ReasonCode { get; set; } = string.Empty;
}

public sealed class UserWorkClassificationAppealDto
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("usage_log_id")] public long UsageLogId { get; set; }
    [JsonPropertyName("user_id")] public long UserId { get; set; }
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("previous_work_related")] public string? PreviousWorkRelated { get; set; }
    [JsonPropertyName("previous_category")] public string? PreviousCategory { get; set; }
    [JsonPropertyName("proposed_work_related")] public string ProposedWorkRelated { get; set; } = string.Empty;
    [JsonPropertyName("proposed_category")] public string ProposedCategory { get; set; } = string.Empty;
    [JsonPropertyName("reason_code")] public string ReasonCode { get; set; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("resolution_note")] public string ResolutionNote { get; set; } = string.Empty;
    [JsonPropertyName("requested_by")] public long? RequestedBy { get; set; }
    [JsonPropertyName("resolved_by")] public long? ResolvedBy { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [JsonPropertyName("resolved_at")] public DateTimeOffset? ResolvedAt { get; set; }
}

public sealed class WorkDistributionPagedDto<T>
{
    [JsonPropertyName("items")] public List<T> Items { get; set; } = [];
    [JsonPropertyName("total")] public long Total { get; set; }
    [JsonPropertyName("page")] public int Page { get; set; } = 1;
    [JsonPropertyName("page_size")] public int PageSize { get; set; } = 50;
    [JsonPropertyName("pages")] public int Pages { get; set; }
}

public static class WorkDistributionLabels
{
    public static string Category(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "coding" => "编码",
        "documentation" => "文档",
        "data_analysis" => "数据分析",
        "operations" => "运维",
        "communication" => "沟通协作",
        "learning" => "学习研究",
        "other" => "其他工作",
        "non_work" => "非工作",
        "unclassified" or null or "" => "未分类",
        _ => value!.Trim()
    };

    public static string WorkRelated(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "work" => "工作相关",
        "non_work" => "非工作",
        "uncertain" or null or "" => "不确定",
        _ => value!.Trim()
    };
}
