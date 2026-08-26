using ParaGateway.Frontend.Models;

namespace ParaGateway.Frontend.Services;

public static class AccountGroupSelectionPolicy
{
    public static bool IsSelectable(
        GroupDto group,
        string? accountPlatform,
        bool mixedScheduling = false,
        bool githubCopilot = false)
    {
        var platform = accountPlatform?.Trim() ?? string.Empty;
        var groupPlatform = group.Platform?.Trim() ?? string.Empty;

        if (group.GitHubCopilotOnly && !githubCopilot)
        {
            return false;
        }

        if (githubCopilot)
        {
            return platform.Equals("openai", StringComparison.OrdinalIgnoreCase)
                && (groupPlatform.Equals("openai", StringComparison.OrdinalIgnoreCase)
                    || groupPlatform.Equals("anthropic", StringComparison.OrdinalIgnoreCase));
        }

        if (string.IsNullOrWhiteSpace(platform) || string.IsNullOrWhiteSpace(groupPlatform)) return true;
        if (groupPlatform.Equals(platform, StringComparison.OrdinalIgnoreCase)) return true;
        if (groupPlatform.Equals("composite", StringComparison.OrdinalIgnoreCase)
            || groupPlatform.Equals("all", StringComparison.OrdinalIgnoreCase)) return true;

        return mixedScheduling
            && platform.Equals("antigravity", StringComparison.OrdinalIgnoreCase)
            && (groupPlatform.Equals("anthropic", StringComparison.OrdinalIgnoreCase)
                || groupPlatform.Equals("gemini", StringComparison.OrdinalIgnoreCase));
    }
}
