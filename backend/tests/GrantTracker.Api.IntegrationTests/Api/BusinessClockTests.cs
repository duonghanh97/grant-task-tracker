using GrantTracker.Api.Common.Time;
using GrantTracker.Api.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace GrantTracker.Api.IntegrationTests.Api;

// D6: the business day is defined by a configurable fixed UTC offset.
public class BusinessClockTests
{
    [TestCase("+1", 1, 0)]
    [TestCase("+7", 7, 0)]
    [TestCase("-8", -8, 0)]
    [TestCase("+07:00", 7, 0)]
    [TestCase("+05:30", 5, 30)]
    [TestCase("-03:30", -3, -30)]
    [TestCase("+0", 0, 0)]
    [TestCase("+00:00", 0, 0)]
    [TestCase("+14", 14, 0)]
    [TestCase("-12", -12, 0)]
    [TestCase(" +7 ", 7, 0)]
    public void ValidOffsetsParse(string value, int hours, int minutes)
    {
        var parsed = BusinessClock.TryParseOffset(value, out var offset);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(parsed, Is.True);
            Assert.That(offset, Is.EqualTo(new TimeSpan(hours, minutes, 0)));
        }
    }

    [TestCase("+15")]
    [TestCase("-13")]
    [TestCase("+14:30")]
    [TestCase("+07:60")]
    [TestCase("+7:5")]
    [TestCase("7")]
    [TestCase("abc")]
    [TestCase("UTC+7")]
    [TestCase("")]
    [TestCase(null)]
    public void InvalidOffsetsAreRejected(string? value)
    {
        Assert.That(BusinessClock.TryParseOffset(value, out _), Is.False);
    }

    [TestCase("2026-11-08T20:00:00Z", "+1", "2026-11-08")]
    [TestCase("2026-11-08T20:00:00Z", "-8", "2026-11-08")]
    [TestCase("2026-11-08T20:00:00Z", "+7", "2026-11-09")]
    [TestCase("2026-11-08T23:30:00Z", "+1", "2026-11-09")]
    [TestCase("2026-11-08T05:00:00Z", "-8", "2026-11-07")]
    [TestCase("2026-11-08T16:59:59Z", "+7", "2026-11-08")]
    [TestCase("2026-11-08T17:00:00Z", "+7", "2026-11-09")]
    public void TodayIsTheDateAtTheConfiguredOffset(string utcNow, string utcOffset, string expected)
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse(utcNow, System.Globalization.CultureInfo.InvariantCulture));
        var clock = new BusinessClock(time, Options.Create(new BusinessOptions { UtcOffset = utcOffset }));

        Assert.That(clock.Today, Is.EqualTo(DateOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Test]
    public void TodayFollowsTheClock()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 8, 23, 0, 0, TimeSpan.Zero));
        var clock = new BusinessClock(time, Options.Create(new BusinessOptions { UtcOffset = "+0" }));

        var before = clock.Today;
        time.Advance(TimeSpan.FromHours(2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(before, Is.EqualTo(new DateOnly(2026, 11, 8)));
            Assert.That(clock.Today, Is.EqualTo(new DateOnly(2026, 11, 9)));
        }
    }

    [TestCase("+15")]
    [TestCase("abc")]
    [TestCase("+7:5")]
    public async Task InvalidOffsetStopsTheAppAtStartup(string utcOffset)
    {
        await using var factory = new ApiFactory { UtcOffset = utcOffset };

        var ex = Assert.Catch(() => factory.EnsureStarted());

        Assert.That(AllMessages(ex), Does.Contain($"Business:UtcOffset '{utcOffset}' is not valid"));
    }

    private static string AllMessages(Exception? ex)
    {
        var messages = new List<string>();
        for (; ex is not null; ex = ex.InnerException)
        {
            messages.Add(ex.Message);
        }

        return string.Join(" | ", messages);
    }
}
