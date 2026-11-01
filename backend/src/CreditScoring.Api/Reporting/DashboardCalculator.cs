using System.Text.Json.Serialization;
using CreditScoring.Api.Domain;

namespace CreditScoring.Api.Reporting;

public sealed record StatsRow(
    DecisionStatus Status,
    double? PBad,
    Recommendation? Recommendation,
    FinalDecision? Outcome,
    string? DecidedBy);

public sealed record ShadowRow(string ModelId, Recommendation? ShadowRecommendation, Recommendation? ActiveRecommendation);

public sealed record MatrixRow(int Approved, int Rejected, int Pending)
{
    [JsonIgnore]
    public int Decided => Approved + Rejected;
}

public sealed record DecisionMatrix(MatrixRow Approve, MatrixRow Reject, MatrixRow Unavailable);

public sealed record HistogramBin(double From, double To, int Count);

public sealed record OfficerStats(string Name, int Decisions, int Overrides, double? OverrideRate);

public sealed record ModelAgreement(string ModelId, int Compared, int Agreed, double? AgreementRate);

public sealed record DashboardStats(
    int Applications,
    int Scored,
    int Unavailable,
    int Decided,
    int Pending,
    DecisionMatrix Matrix,
    double? OverrideRate,
    double? HighSideOverrideRate,
    double? LowSideOverrideRate,
    double? UnavailableRate,
    IReadOnlyList<HistogramBin> PBadHistogram,
    IReadOnlyList<OfficerStats> Officers,
    IReadOnlyList<ModelAgreement> ShadowAgreement);

public static class DashboardCalculator
{
    public const int Bins = 10;

    public static DashboardStats Compute(IReadOnlyCollection<StatsRow> rows, IReadOnlyCollection<ShadowRow> shadows)
    {
        var matrix = new DecisionMatrix(
            Row(rows.Where(r => r.Recommendation == Recommendation.Approve)),
            Row(rows.Where(r => r.Recommendation == Recommendation.Reject)),
            Row(rows.Where(r => r.Recommendation is null)));

        var highSide = matrix.Approve.Rejected;
        var lowSide = matrix.Reject.Approved;
        var unavailable = rows.Count(r => r.Status == DecisionStatus.ScoringUnavailable);
        var decided = rows.Count(r => r.Outcome is not null);

        var histogram = Enumerable.Range(0, Bins)
            .Select(i => new HistogramBin(
                (double)i / Bins,
                (double)(i + 1) / Bins,
                rows.Count(r => r.PBad is { } p && BinOf(p) == i)))
            .ToList();

        var officers = rows
            .Where(r => r.Outcome is not null)
            .GroupBy(r => r.DecidedBy ?? "")
            .Select(g =>
            {
                var withRecommendation = g.Where(r => r.Recommendation is not null).ToList();
                var overrides = withRecommendation.Count(IsOverride);
                return new OfficerStats(g.Key, g.Count(), overrides, Rate(overrides, withRecommendation.Count));
            })
            .OrderByDescending(o => o.Decisions)
            .ThenBy(o => o.Name, StringComparer.Ordinal)
            .ToList();

        var agreement = shadows
            .GroupBy(s => s.ModelId)
            .Select(g =>
            {
                var compared = g.Where(s => s.ShadowRecommendation is not null && s.ActiveRecommendation is not null).ToList();
                var agreed = compared.Count(s => s.ShadowRecommendation == s.ActiveRecommendation);
                return new ModelAgreement(g.Key, compared.Count, agreed, Rate(agreed, compared.Count));
            })
            .OrderBy(a => a.ModelId, StringComparer.Ordinal)
            .ToList();

        return new DashboardStats(
            rows.Count,
            rows.Count - unavailable,
            unavailable,
            decided,
            rows.Count - decided,
            matrix,
            Rate(highSide + lowSide, matrix.Approve.Decided + matrix.Reject.Decided),
            Rate(highSide, matrix.Approve.Decided),
            Rate(lowSide, matrix.Reject.Decided),
            Rate(unavailable, rows.Count),
            histogram,
            officers,
            agreement);
    }

    private static MatrixRow Row(IEnumerable<StatsRow> rows)
    {
        var list = rows.ToList();
        return new MatrixRow(
            list.Count(r => r.Outcome == FinalDecision.Approve),
            list.Count(r => r.Outcome == FinalDecision.Reject),
            list.Count(r => r.Outcome is null));
    }

    private static bool IsOverride(StatsRow r) =>
        r.Recommendation is { } recommendation && r.Outcome is { } outcome && !OfficerDecision.Agrees(recommendation, outcome);

    private static int BinOf(double pBad) => Math.Clamp((int)Math.Floor(pBad * Bins), 0, Bins - 1);

    private static double? Rate(int part, int whole) => whole == 0 ? null : (double)part / whole;
}
