using System.Text.Json.Serialization;

namespace ParaGateway.Frontend.Models;

public sealed class AccountUsageStatsDto
{
    [JsonPropertyName("history")] public List<AccountUsageHistoryDto> History { get; set; } = [];
    [JsonPropertyName("summary")] public AccountUsageSummaryDto Summary { get; set; } = new();
    [JsonPropertyName("models")] public List<AccountUsageModelStatDto> Models { get; set; } = [];
    [JsonPropertyName("endpoints")] public List<AccountUsageEndpointStatDto> Endpoints { get; set; } = [];
    [JsonPropertyName("upstream_endpoints")] public List<AccountUsageEndpointStatDto> UpstreamEndpoints { get; set; } = [];
}

public sealed class AccountUsageHistoryDto
{
    [JsonPropertyName("date")] public string Date { get; set; } = string.Empty;
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName("requests")] public long Requests { get; set; }
    [JsonPropertyName("tokens")] public long Tokens { get; set; }
    [JsonPropertyName("cost")] public double Cost { get; set; }
    [JsonPropertyName("actual_cost")] public double ActualCost { get; set; }
    [JsonPropertyName("user_cost")] public double UserCost { get; set; }
}

public sealed class AccountUsageSummaryDto
{
    [JsonPropertyName("days")] public int Days { get; set; }
    [JsonPropertyName("actual_days_used")] public int ActualDaysUsed { get; set; }
    [JsonPropertyName("total_cost")] public double TotalCost { get; set; }
    [JsonPropertyName("total_user_cost")] public double TotalUserCost { get; set; }
    [JsonPropertyName("total_standard_cost")] public double TotalStandardCost { get; set; }
    [JsonPropertyName("total_requests")] public long TotalRequests { get; set; }
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; set; }
    [JsonPropertyName("avg_daily_cost")] public double AvgDailyCost { get; set; }
    [JsonPropertyName("avg_daily_user_cost")] public double AvgDailyUserCost { get; set; }
    [JsonPropertyName("avg_daily_requests")] public double AvgDailyRequests { get; set; }
    [JsonPropertyName("avg_daily_tokens")] public double AvgDailyTokens { get; set; }
    [JsonPropertyName("avg_duration_ms")] public double AvgDurationMs { get; set; }
    [JsonPropertyName("today")] public AccountUsageDaySummaryDto? Today { get; set; }
    [JsonPropertyName("highest_cost_day")] public AccountUsageDaySummaryDto? HighestCostDay { get; set; }
    [JsonPropertyName("highest_request_day")] public AccountUsageDaySummaryDto? HighestRequestDay { get; set; }
}

public sealed class AccountUsageDaySummaryDto
{
    [JsonPropertyName("date")] public string Date { get; set; } = string.Empty;
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName("cost")] public double Cost { get; set; }
    [JsonPropertyName("user_cost")] public double UserCost { get; set; }
    [JsonPropertyName("requests")] public long Requests { get; set; }
    [JsonPropertyName("tokens")] public long Tokens { get; set; }
}

public sealed class AccountUsageModelStatDto
{
    [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
    [JsonPropertyName("requests")] public long Requests { get; set; }
    [JsonPropertyName("input_tokens")] public long InputTokens { get; set; }
    [JsonPropertyName("output_tokens")] public long OutputTokens { get; set; }
    [JsonPropertyName("cache_creation_tokens")] public long CacheCreationTokens { get; set; }
    [JsonPropertyName("cache_read_tokens")] public long CacheReadTokens { get; set; }
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; set; }
    [JsonPropertyName("cost")] public double Cost { get; set; }
    [JsonPropertyName("actual_cost")] public double ActualCost { get; set; }
    [JsonPropertyName("account_cost")] public double AccountCost { get; set; }
}

public sealed class AccountUsageEndpointStatDto
{
    [JsonPropertyName("endpoint")] public string Endpoint { get; set; } = string.Empty;
    [JsonPropertyName("requests")] public long Requests { get; set; }
    [JsonPropertyName("total_tokens")] public long TotalTokens { get; set; }
    [JsonPropertyName("cost")] public double Cost { get; set; }
    [JsonPropertyName("actual_cost")] public double ActualCost { get; set; }
}

public sealed class AccountStatsDistributionItemDto
{
    public string Name { get; init; } = string.Empty;
    public long Requests { get; init; }
    public long TotalTokens { get; init; }
    public double ActualCost { get; init; }
    public double Cost { get; init; }
    public double? AccountCost { get; init; }
}
