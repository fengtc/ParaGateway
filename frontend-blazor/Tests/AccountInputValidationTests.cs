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
