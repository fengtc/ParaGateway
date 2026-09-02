using ParaGateway.Frontend.Models;

namespace ParaGateway.Frontend.Services;

/// <summary>
/// Resolves an admin usage email to a stable user row when the database contains
/// both an active and a soft-deleted account with the same address.
/// </summary>
public static class AdminUsageUserSelectionPolicy
{
    public static string NormalizeEmail(string? email) => email?.Trim() ?? string.Empty;

    public static bool EmailEquals(string? left, string? right) =>
        string.Equals(NormalizeEmail(left), NormalizeEmail(right), StringComparison.OrdinalIgnoreCase);

    public static AdminUsageUserOptionDto? FindExact(
        IEnumerable<AdminUsageUserOptionDto>? options,
        string? email)
    {
        var normalized = NormalizeEmail(email);
        if (normalized.Length == 0 || options is null) return null;

        return options
            .Where(option => EmailEquals(option.Email, normalized))
            .OrderBy(option => option.Deleted)
            .ThenBy(option => option.Id)
            .FirstOrDefault();
    }

    public static IEnumerable<AdminUsageUserOptionDto> OrderOptions(
        IEnumerable<AdminUsageUserOptionDto>? options)
    {
        if (options is null) return Enumerable.Empty<AdminUsageUserOptionDto>();

        return options
            .Where(option => !string.IsNullOrWhiteSpace(option.Email))
            .Select(option => new AdminUsageUserOptionDto
            {
                Id = option.Id,
                Email = NormalizeEmail(option.Email),
                Deleted = option.Deleted
            })
            .GroupBy(option => option.Email, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(option => option.Deleted)
                .ThenBy(option => option.Id)
                .First())
            .OrderBy(option => option.Email, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Id)
            .ToList();
    }
}
