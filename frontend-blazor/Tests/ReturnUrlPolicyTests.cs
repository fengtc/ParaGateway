using ParaGateway.Frontend.Services;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class ReturnUrlPolicyTests
{
    private const string BaseUri = "https://mail.example.com/";

    [Theory]
    [InlineData("/")]
    [InlineData("/users")]
    [InlineData("/api-keys?userId=44d0a33d#details")]
    [InlineData("/models/%E4%B8%AD%E6%96%87")]
    [InlineData("/search?next=https%3A%2F%2Fevil.example")]
    public void TryGetSafeLocalPath_AcceptsLocalApplicationPaths(string candidate)
    {
        var accepted = ReturnUrlPolicy.TryGetSafeLocalPath(candidate, BaseUri, out var result);

        Assert.True(accepted);
        Assert.Equal(candidate, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("users")]
    [InlineData("https://evil.example/")]
    [InlineData("//evil.example/")]
    [InlineData("///evil.example/")]
    [InlineData("/\\evil.example/")]
    [InlineData("/%5cevil.example/")]
    [InlineData("/%255cevil.example/")]
    [InlineData("/%2f%2fevil.example/")]
    [InlineData("/%252f%252fevil.example/")]
    [InlineData("/users\r\nLocation:https://evil.example/")]
    [InlineData("/users%250d%250aLocation%3Ahttps%3A%2F%2Fevil.example")]
    [InlineData("/users\0")]
    public void TryGetSafeLocalPath_RejectsExternalOrAmbiguousValues(string? candidate)
    {
        var accepted = ReturnUrlPolicy.TryGetSafeLocalPath(candidate, BaseUri, out var result);

        Assert.False(accepted);
        Assert.Empty(result);
    }

    [Fact]
    public void GetSafeLocalPath_UsesFallbackForRejectedValue()
    {
        var result = ReturnUrlPolicy.GetSafeLocalPath("/\\evil.example", BaseUri);

        Assert.Equal("/", result);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hello")]
    public void TryGetSafeLocalPath_RejectsNonHttpNavigation(string candidate)
    {
        Assert.False(ReturnUrlPolicy.TryGetSafeLocalPath(candidate, BaseUri, out _));
    }
}
