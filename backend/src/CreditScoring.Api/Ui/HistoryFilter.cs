using CreditScoring.Api.Contracts;
using CreditScoring.Api.Domain;

namespace CreditScoring.Api.Ui;

public enum HistoryOutcome
{
    Approve = 1,
    Review = 2,
    Reject = 3,
    ScoringUnavailable = 4,
}

public sealed record HistoryFilter
{
    public string? Name { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public HistoryOutcome? Outcome { get; init; }
    public ReviewFilter? Review { get; init; }
    public int Page { get; init; } = 1;

    public HistoryQuery ToQuery(TimeZoneInfo zone, int pageSize) => new()
    {
        ApplicantName = string.IsNullOrWhiteSpace(Name) ? null : Name.Trim(),
        From = From is { } from ? StartOfDay(from, zone) : null,
        To = To is { } to ? StartOfDay(to.AddDays(1), zone) : null,
        Status = Outcome == HistoryOutcome.ScoringUnavailable ? DecisionStatus.ScoringUnavailable : null,
        Recommendation = Outcome switch
        {
            HistoryOutcome.Approve => Domain.Recommendation.Approve,
            HistoryOutcome.Review => Domain.Recommendation.Review,
            HistoryOutcome.Reject => Domain.Recommendation.Reject,
            _ => null,
        },
        Review = Review,
        Page = Math.Max(Page, 1),
        PageSize = pageSize,
    };

    public static DateTimeOffset StartOfDay(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
}
