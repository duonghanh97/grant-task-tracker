using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace GrantTracker.Api.Common.Time;

public sealed class BusinessOptions
{
    public const string SectionName = "Business";

    /// <summary>
    /// Fixed UTC offset that defines the business day, e.g. <c>+7</c>,
    /// <c>-8</c> or <c>+05:30</c>. Daylight saving is not applied.
    /// </summary>
    public string UtcOffset { get; set; } = "+00:00";
}

/// <summary>
/// The only place that turns the current instant into a business date.
/// Business logic receives <see cref="Today"/> and never reads a clock (BR-6).
/// </summary>
public sealed partial class BusinessClock
{
    private static readonly TimeSpan MinOffset = TimeSpan.FromHours(-12);
    private static readonly TimeSpan MaxOffset = TimeSpan.FromHours(14);

    private readonly TimeProvider _timeProvider;

    public BusinessClock(TimeProvider timeProvider, IOptions<BusinessOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _timeProvider = timeProvider;

        // options.Value runs BusinessOptionsValidator, so an invalid value fails here.
        Offset = TryParseOffset(options.Value.UtcOffset, out var offset)
            ? offset
            : throw new InvalidOperationException("Business:UtcOffset is not valid.");
    }

    public TimeSpan Offset { get; }

    public DateOnly Today => DateOnly.FromDateTime(_timeProvider.GetUtcNow().ToOffset(Offset).DateTime);

    /// <summary>Parses <c>±H</c>, <c>±HH</c> or <c>±HH:mm</c> between -12:00 and +14:00.</summary>
    public static bool TryParseOffset(string? value, out TimeSpan offset)
    {
        offset = TimeSpan.Zero;
        var match = OffsetPattern().Match(value?.Trim() ?? string.Empty);
        if (!match.Success)
        {
            return false;
        }

        var hours = int.Parse(match.Groups["hours"].Value, CultureInfo.InvariantCulture);
        var minutes = match.Groups["minutes"].Success
            ? int.Parse(match.Groups["minutes"].Value, CultureInfo.InvariantCulture)
            : 0;
        if (minutes > 59)
        {
            return false;
        }

        var magnitude = new TimeSpan(hours, minutes, 0);
        var parsed = match.Groups["sign"].Value == "-" ? -magnitude : magnitude;
        if (parsed < MinOffset || parsed > MaxOffset)
        {
            return false;
        }

        offset = parsed;
        return true;
    }

    [GeneratedRegex(@"^(?<sign>[+-])(?<hours>\d{1,2})(?::(?<minutes>\d{2}))?$")]
    private static partial Regex OffsetPattern();
}

internal sealed class BusinessOptionsValidator : IValidateOptions<BusinessOptions>
{
    public ValidateOptionsResult Validate(string? name, BusinessOptions options) =>
        BusinessClock.TryParseOffset(options.UtcOffset, out _)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"Business:UtcOffset '{options.UtcOffset}' is not valid. "
                + "Use a UTC offset such as +7, -8 or +05:30, between -12:00 and +14:00.");
}
