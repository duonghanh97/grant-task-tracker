using GrantTracker.Domain.Grants;

namespace GrantTracker.Domain.Tests;

internal static class TestData
{
    /// <summary>A fixed "today" so no test depends on the machine's clock.</summary>
    public static readonly DateOnly Today = new(2026, 11, 8);

    public static Grant OpenGrant(int applicationDaysAgo = 30) =>
        new("GR-100001", "Riverside Community Garden Inc.", Today.AddDays(-applicationDaysAgo));
}
