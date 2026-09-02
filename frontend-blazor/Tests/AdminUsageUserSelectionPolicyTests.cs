using ParaGateway.Frontend.Models;
using ParaGateway.Frontend.Services;
using Xunit;

namespace ParaGateway.Frontend.Tests;

public sealed class AdminUsageUserSelectionPolicyTests
{
    [Fact]
    public void ExactEmailPrefersActiveUserWhenDeletedRowSortsFirst()
    {
        var selected = AdminUsageUserSelectionPolicy.FindExact(
            [
                new AdminUsageUserOptionDto { Id = 4, Email = "xiegy@paratera.com", Deleted = true },
                new AdminUsageUserOptionDto { Id = 7, Email = "XIEGY@PARATERA.COM", Deleted = false }
            ],
            "  xiegy@paratera.com  ");

        Assert.NotNull(selected);
        Assert.Equal(7, selected.Id);
    }

    [Fact]
    public void ExactEmailFallsBackToDeletedUserWhenNoActiveRowExists()
    {
        var selected = AdminUsageUserSelectionPolicy.FindExact(
            [new AdminUsageUserOptionDto { Id = 4, Email = " xiegy@paratera.com ", Deleted = true }],
            "XIEGY@PARATERA.COM");

        Assert.NotNull(selected);
        Assert.Equal(4, selected.Id);
    }

    [Fact]
    public void OrderOptionsNormalizesWhitespaceAndDeduplicatesToActiveRow()
    {
        var options = AdminUsageUserSelectionPolicy.OrderOptions(
            [
                new AdminUsageUserOptionDto { Id = 4, Email = " xiegy@paratera.com ", Deleted = true },
                new AdminUsageUserOptionDto { Id = 7, Email = "xiegy@paratera.com", Deleted = false }
            ]).ToList();

        var selected = Assert.Single(options);
        Assert.Equal(7, selected.Id);
        Assert.Equal("xiegy@paratera.com", selected.Email);
        Assert.False(selected.Deleted);
    }
}
