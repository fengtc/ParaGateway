using ParaGateway.Frontend.Models;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class ApiKeyDtoTests
{
    [Theory]
    [InlineData("active", true)]
    [InlineData("inactive", false)]
    [InlineData("quota_exhausted", false)]
    [InlineData("expired", false)]
    public void FromPreservesBackendStatusForGridAndKeepsActiveFlag(string status, bool isActive)
    {
        var dto = ApiKeyDto.From(new GoApiKey
        {
            Id = 1,
            UserId = 2,
            Key = "sk-test-key",
            Name = "test",
            Status = status
        });

        Assert.Equal(status, dto.Status);
        Assert.Equal(isActive, dto.IsActive);
    }
}
