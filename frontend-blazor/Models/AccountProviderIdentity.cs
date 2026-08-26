using System.Text.Json;

namespace ParaGateway.Frontend.Models;

public static class AccountProviderIdentity
{
    public const string GitHubCopilotProfile = "github_copilot";

    public static string OAuthProfile(AccountDto? account) =>
        ReadString(account?.Credentials, "oauth_profile")?.Trim() ?? string.Empty;

    public static bool IsCanonicalGitHubCopilot(AccountDto? account) =>
        account is not null
        && string.Equals(account.Platform?.Trim(), "openai", StringComparison.OrdinalIgnoreCase)
        && string.Equals(account.Type?.Trim(), "oauth", StringComparison.OrdinalIgnoreCase)
        && string.Equals(OAuthProfile(account), GitHubCopilotProfile, StringComparison.OrdinalIgnoreCase);

    public static bool IsLegacyGitHubCopilot(AccountDto? account) =>
        account is not null
        && !IsCanonicalGitHubCopilot(account)
        && (string.Equals(account.Platform?.Trim(), "copilot", StringComparison.OrdinalIgnoreCase)
            || string.Equals(OAuthProfile(account), GitHubCopilotProfile, StringComparison.OrdinalIgnoreCase));

    private static string? ReadString(IReadOnlyDictionary<string, JsonElement>? source, string key) =>
        source is not null
        && source.TryGetValue(key, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
