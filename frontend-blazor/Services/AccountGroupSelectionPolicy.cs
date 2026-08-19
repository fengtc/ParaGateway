using ParaGateway.Frontend.Models;

namespace ParaGateway.Frontend.Services;

public static class AccountGroupSelectionPolicy
{
    public static bool IsSelectable(GroupDto group, string? accountPlatform, bool mixedScheduling = false)
    {
        var platform = accountPlatform?.Trim() ?? string.Empty;
        var groupPlatform = group.Platform?.Trim() ?? string.Empty;

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
