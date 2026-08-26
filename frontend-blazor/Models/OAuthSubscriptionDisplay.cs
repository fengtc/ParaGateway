using System.Globalization;
using System.Text.Json;

namespace ParaGateway.Frontend.Models;

public sealed record OAuthSubscriptionDisplay(
    string PlanLabel,
    string PlanTone,
    string? ExpiryLabel,
    string? ExpiryTitle)
{
    public static OAuthSubscriptionDisplay? From(AccountDto account)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (!string.Equals(account.Type, "oauth", StringComparison.OrdinalIgnoreCase)
            || !IsSubscriptionPlatform(account.Platform)
            || IsGitHubCopilot(account))
        {
            return null;
        }

        var rawPlan = FirstNonBlank(
            ReadString(account.Credentials, "plan_type"),
            account.ParentPlanType);
        if (rawPlan is null)
        {
            return null;
        }

        var normalizedPlan = Normalize(rawPlan);
        var planLabel = normalizedPlan switch
        {
            "plus" => "Plus",
            "team" => "Team",
            "chatgptpro" or "pro" => "Pro",
            "free" or "basic" or "xbasic" => "Free",
            "business" or "selfservebusiness" or "selfservebusinessusagebased" => "Business",
            "enterprise" => "Enterprise",
            "edu" or "education" => "Edu",
            "claudepro" => "Claude Pro",
            "claudemax" or "max" => "Claude Max",
            "claudemax5x" or "max5x" => "Claude Max 5x",
            "claudemax20x" or "max20x" => "Claude Max 20x",
            "personal" => "Personal",
            _ => rawPlan
        };
        var planTone = normalizedPlan switch
        {
            "free" or "basic" or "xbasic" => "free",
            "plus" => "plus",
            "team" => "team",
            "chatgptpro" or "pro" or "claudepro" => "pro",
            "claudemax" or "max" or "claudemax5x" or "max5x" or "claudemax20x" or "max20x" => "max",
            "business" or "selfservebusiness" or "selfservebusinessusagebased" or "enterprise" => "business",
            _ => "default"
        };

        var rawExpiry = FirstNonBlank(
            ReadString(account.Credentials, "subscription_expires_at"),
            account.ParentSubscriptionExpiresAt);
        var expiryLabel = IsFreePlan(normalizedPlan) || !TryParseExpiry(rawExpiry, out var expiry)
            ? null
            : $"到期 {expiry.ToLocalTime():yyyy-MM-dd}";

        return new OAuthSubscriptionDisplay(planLabel, planTone, expiryLabel, rawExpiry);
    }

    private static bool IsGitHubCopilot(AccountDto account) =>
        string.Equals(account.Platform, "copilot", StringComparison.OrdinalIgnoreCase)
        || string.Equals(ReadString(account.Credentials, "oauth_profile"), "github_copilot", StringComparison.OrdinalIgnoreCase)
        || string.Equals(ReadString(account.Extra, "oauth_profile"), "github_copilot", StringComparison.OrdinalIgnoreCase);

    private static bool IsSubscriptionPlatform(string? platform) =>
        string.Equals(platform, "openai", StringComparison.OrdinalIgnoreCase)
        || string.Equals(platform, "anthropic", StringComparison.OrdinalIgnoreCase);

    private static bool IsFreePlan(string normalizedPlan) => normalizedPlan is "free" or "basic" or "xbasic";

    private static string Normalize(string value) => new(value
        .Trim()
        .ToLowerInvariant()
        .Where(char.IsLetterOrDigit)
        .ToArray());

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string? ReadString(Dictionary<string, JsonElement>? source, string key) =>
        source is not null
        && source.TryGetValue(key, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool TryParseExpiry(string? value, out DateTimeOffset expiry)
    {
        expiry = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                out expiry))
        {
            return true;
        }

        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixValue))
        {
            return false;
        }

        try
        {
            expiry = unixValue > 10_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(unixValue)
                : DateTimeOffset.FromUnixTimeSeconds(unixValue);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
