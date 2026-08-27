using System.Text.Json.Serialization;

namespace ParaGateway.Frontend.Models;

public sealed class WorkDistributionSummaryQueryDto
{
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public string Timezone { get; set; } = "Asia/Shanghai";
    public long? UserId { get; set; }
    public string Department { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Metric { get; set; } = "requests";
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
    [JsonPropertyName("coverage")] public WorkDistributionCoverageDto Coverage { get; set; } = new();
    [JsonPropertyName("work_related")] public List<WorkDistributionRelationDto> WorkRelated { get; set; } = [];
    [JsonPropertyName("categories")] public List<WorkDistributionCategoryDto> Categories { get; set; } = [];
    [JsonPropertyName("departments")] public List<WorkDistributionDepartmentDto> Departments { get; set; } = [];
    [JsonPropertyName("roles")] public List<WorkDistributionRoleDto> Roles { get; set; } = [];
    [JsonPropertyName("users")] public List<WorkDistributionUserDto> Users { get; set; } = [];
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
}

public sealed class WorkDistributionRelationDto
{
    [JsonPropertyName("work_related")] public string WorkRelated { get; set; } = string.Empty;
    [JsonPropertyName("value")] public long Value { get; set; }
    [JsonPropertyName("percent")] public double Percent { get; set; }
}

public sealed class WorkDistributionDepartmentDto
{
    [JsonPropertyName("department")] public string Department { get; set; } = string.Empty;
    [JsonPropertyName("requests")] public long Requests { get; set; }
    [JsonPropertyName("value")] public long Value { get; set; }
    [JsonPropertyName("average_confidence")] public double? AverageConfidence { get; set; }
    [JsonPropertyName("categories")] public List<WorkDistributionCategoryDto> Categories { get; set; } = [];
}

public sealed class WorkDistributionRoleDto
{
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
    [JsonPropertyName("user_count")] public long UserCount { get; set; }
}

public sealed class WorkDistributionUserDto
{
    [JsonPropertyName("user_id")] public long UserId { get; set; }
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("department")] public string Department { get; set; } = string.Empty;
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
    [JsonPropertyName("requests")] public long Requests { get; set; }
    [JsonPropertyName("value")] public long Value { get; set; }
    [JsonPropertyName("average_confidence")] public double? AverageConfidence { get; set; }
    [JsonPropertyName("categories")] public List<WorkDistributionCategoryDto> Categories { get; set; } = [];
}
