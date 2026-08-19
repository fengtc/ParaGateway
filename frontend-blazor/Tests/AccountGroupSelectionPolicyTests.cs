using ParaGateway.Frontend.Models;
using ParaGateway.Frontend.Services;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class AccountGroupSelectionPolicyTests
{
    [Theory]
    [InlineData("openai", "openai", false, true)]
    [InlineData("anthropic", "openai", false, false)]
    [InlineData("composite", "openai", false, true)]
    [InlineData("anthropic", "antigravity", false, false)]
    [InlineData("anthropic", "antigravity", true, true)]
    [InlineData("gemini", "antigravity", true, true)]
    public void SelectionMatchesTheOfficialPlatformRules(
        string groupPlatform,
        string accountPlatform,
        bool mixedScheduling,
        bool expected)
    {
        var group = new GroupDto { Id = "1", Name = "test", Platform = groupPlatform };

        Assert.Equal(expected, AccountGroupSelectionPolicy.IsSelectable(group, accountPlatform, mixedScheduling));
    }
}
