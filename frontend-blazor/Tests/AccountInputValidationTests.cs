using System.ComponentModel.DataAnnotations;
using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class AccountInputValidationTests
{
    [Fact]
    public void EditingAccountAllowsBlankOptionalBaseUrl()
    {
        var input = CreateEditingAccount();

        Assert.Empty(Validate(input));
    }

    [Fact]
    public void EditingAccountRejectsMalformedNonEmptyBaseUrl()
    {
        var input = CreateEditingAccount();
        input.BaseUrl = "not-a-url";

        var results = Validate(input);

        Assert.Contains(results, result =>
            result.ErrorMessage == "Base URL 格式不正确"
            && result.MemberNames.Contains(nameof(AccountInput.BaseUrl)));
    }

    [Fact]
    public void AdaptiveDeepSeekRequiresAllNativeProtocolBaseUrls()
    {
        var input = CreateAdaptiveAccount("deepseek");

        var missing = Validate(input);

        Assert.Contains(missing, result => result.MemberNames.Contains(nameof(AccountInput.AdaptiveChatCompletionsBaseUrl)));
        Assert.Contains(missing, result => result.MemberNames.Contains(nameof(AccountInput.AdaptiveAnthropicBaseUrl)));
        Assert.Contains(missing, result => result.MemberNames.Contains(nameof(AccountInput.AdaptiveResponsesBaseUrl)));

        input.AdaptiveChatCompletionsBaseUrl = "https://api.deepseek.com";
        input.AdaptiveAnthropicBaseUrl = "https://api.deepseek.com/anthropic";
        input.AdaptiveResponsesBaseUrl = "https://api.deepseek.com/responses";

        Assert.Empty(Validate(input));
    }

    [Fact]
    public void AdaptiveKimiDoesNotRequireResponsesBaseUrl()
    {
        var input = CreateAdaptiveAccount("kimi");
        input.AdaptiveChatCompletionsBaseUrl = "https://api.moonshot.cn/v1";
        input.AdaptiveAnthropicBaseUrl = "https://api.moonshot.cn/anthropic";

        Assert.Empty(Validate(input));
    }

    private static AccountInput CreateAdaptiveAccount(string platform) => new()
    {
        Name = "adaptive-cn",
        Platform = platform,
        Type = "apikey",
        ApiKey = "sk-test",
        ApiProtocol = "adaptive"
    };

    private static AccountInput CreateEditingAccount() => new()
    {
        IsEditing = true,
        Name = "OpenAI OAuth",
        Platform = "openai",
        Type = "oauth",
        BaseUrl = string.Empty,
        Concurrency = 8,
        Priority = 100,
        RateMultiplier = 1
    };

    private static List<ValidationResult> Validate(AccountInput input)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true);
        return results;
    }
}
