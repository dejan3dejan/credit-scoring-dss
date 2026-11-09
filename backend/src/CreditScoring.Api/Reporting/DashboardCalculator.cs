using System.Text.Json.Serialization;
using CreditScoring.Api.Domain;

namespace CreditScoring.Api.Reporting;

public sealed record StatsRow(
    DecisionStatus Status,
    double? PBad,
    Recommendation? Recommendation,
    FinalDecision? Outcome,
    string? DecidedBy,
    DateTimeOffset SubmittedAt = default,
    Guid BranchId = default,
    LoanOutcomeKind? LoanOutcome = null);

public sealed record BranchRef(Guid Id, string Code, string Name);

public sealed record StatsRange(DateTimeOffset From, DateTimeOffset To)
{
    public StatsRange Previous => new(From - (To - From), From);
}

public sealed record WeekStats(
    DateOnly WeekStart,
    int Applications,
    int Approve,
    int Review,
    int Reject,
    int Unavailable,
    int Decided,
    int Overrides,
    double? OverrideRate,
    double? ReviewShare);

public sealed record BranchStats(
    Guid BranchId,
    string Code,
    string Name,
    int Applications,
    int Decided,
    int Overrides,
    double? OverrideRate,
    double? ReviewShare);

public sealed record PeriodStats(
    DateTimeOffset From,
    DateTimeOffset To,
    int Applications,
    int Decided,
    double? ApprovalRate,
    double? OverrideRate,
    double? ReviewShare,
    double? UnavailableRate);

public sealed record PeriodComparison(PeriodStats Current, PeriodStats Previous);

public enum DecisionPath
{
    AsRecommended = 1,
    AfterReview = 2,
    Override = 3,
    WithoutScore = 4,
}

public sealed record OutcomeGroup(DecisionPath Path, int Approved, int WithOutcome, int Defaulted, double? DefaultRate);

public sealed record CalibrationBand(double From, double To, int Loans, double? MeanPredicted, double? ObservedDefaultRate);

public sealed record ShadowRow(string ModelId, Recommendation? ShadowRecommendation, Recommendation? ActiveRecommendation);

public sealed record MatrixRow(int Approved, int Rejected, int Pending)
{
    [JsonIgnore]
    public int Decided => Approved + Rejected;
}

public sealed record DecisionMatrix(MatrixRow Approve, MatrixRow Review, MatrixRow Reject, MatrixRow Unavailable);

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
    IReadOnlyList<ModelAgreement> ShadowAgreement,
    IReadOnlyList<WeekStats> Weekly,
    IReadOnlyList<BranchStats> ByBranch,
    PeriodComparison? Comparison,
    IReadOnlyList<OutcomeGroup> OutcomeByDecision,
    IReadOnlyList<CalibrationBand> Calibration);

public static class DashboardCalculator
{
    public const int Bins = 10;

    public static DashboardStats Compute(IReadOnlyCollection<StatsRow> rows, IReadOnlyCollection<ShadowRow> shadows) =>
        Compute(rows, shadows, null, null, null);

    public static DashboardStats Compute(
        IReadOnlyCollection<StatsRow> rows,
        IReadOnlyCollection<ShadowRow> shadows,
        IReadOnlyCollection<BranchRef>? branches,
        StatsRange? range,
        IReadOnlyCollection<StatsRow>? previous)
    {
        var matrix = new DecisionMatrix(
            Row(rows.Where(r => r.Recommendation == Recommendation.Approve)),
            Row(rows.Where(r => r.Recommendation == Recommendation.Review)),
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
                var overridable = g.Where(r => r.Recommendation is Recommendation.Approve or Recommendation.Reject).ToList();
                var overrides = overridable.Count(IsOverride);
                return new OfficerStats(g.Key, g.Count(), overrides, Rate(overrides, overridable.Count));
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
            agreement,
            Weekly(rows, range),
            branches is null ? [] : ByBranch(rows, branches),
            range is not null && previous is not null
                ? new PeriodComparison(Period(rows, range), Period(previous, range.Previous))
                : null,
            OutcomeByDecision(rows),
            Calibration(rows));
    }

    private static DateOnly WeekOf(DateTimeOffset at)
    {
        var day = DateOnly.FromDateTime(at.UtcDateTime);
        return day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
    }

    private static IReadOnlyList<WeekStats> Weekly(IReadOnlyCollection<StatsRow> rows, StatsRange? range)
    {
        DateOnly first, last;
        if (range is not null)
        {
            first = WeekOf(range.From);
            last = WeekOf(range.To.AddTicks(-1));
        }
        else if (rows.Count > 0)
        {
            first = rows.Min(r => WeekOf(r.SubmittedAt));
            last = rows.Max(r => WeekOf(r.SubmittedAt));
        }
        else
        {
            return [];
        }

        var byWeek = rows.ToLookup(r => WeekOf(r.SubmittedAt));
        var weeks = new List<WeekStats>();
        for (var week = first; week <= last; week = week.AddDays(7))
        {
            var inWeek = byWeek[week].ToList();
            var (overrides, overridable) = Overrides(inWeek);
            weeks.Add(new WeekStats(
                week,
                inWeek.Count,
                inWeek.Count(r => r.Recommendation == Recommendation.Approve),
                inWeek.Count(r => r.Recommendation == Recommendation.Review),
                inWeek.Count(r => r.Recommendation == Recommendation.Reject),
                inWeek.Count(r => r.Status == DecisionStatus.ScoringUnavailable),
                inWeek.Count(r => r.Outcome is not null),
                overrides,
                Rate(overrides, overridable),
                ReviewShare(inWeek)));
        }
        return weeks;
    }

    private static IReadOnlyList<BranchStats> ByBranch(IReadOnlyCollection<StatsRow> rows, IReadOnlyCollection<BranchRef> branches)
    {
        var names = branches.ToDictionary(b => b.Id);
        return rows
            .GroupBy(r => r.BranchId)
            .Select(g =>
            {
                var branch = names[g.Key];
                var list = g.ToList();
                var (overrides, overridable) = Overrides(list);
                return new BranchStats(
                    branch.Id, branch.Code, branch.Name, list.Count, list.Count(r => r.Outcome is not null),
                    overrides, Rate(overrides, overridable), ReviewShare(list));
            })
            .OrderBy(b => b.Code, StringComparer.Ordinal)
            .ToList();
    }

    private static PeriodStats Period(IReadOnlyCollection<StatsRow> rows, StatsRange range)
    {
        var decided = rows.Count(r => r.Outcome is not null);
        var (overrides, overridable) = Overrides(rows);
        return new PeriodStats(
            range.From,
            range.To,
            rows.Count,
            decided,
            Rate(rows.Count(r => r.Outcome == FinalDecision.Approve), decided),
            Rate(overrides, overridable),
            ReviewShare(rows),
            Rate(rows.Count(r => r.Status == DecisionStatus.ScoringUnavailable), rows.Count));
    }

    private static IReadOnlyList<OutcomeGroup> OutcomeByDecision(IReadOnlyCollection<StatsRow> rows)
    {
        var approved = rows.Where(r => r.Outcome == FinalDecision.Approve).ToList();
        return Enum.GetValues<DecisionPath>()
            .Select(path =>
            {
                var loans = approved.Where(r => PathOf(r) == path).ToList();
                var withOutcome = loans.Count(r => r.LoanOutcome is not null);
                var defaulted = loans.Count(r => r.LoanOutcome == LoanOutcomeKind.Defaulted);
                return new OutcomeGroup(path, loans.Count, withOutcome, defaulted, Rate(defaulted, withOutcome));
            })
            .ToList();
    }

    private static DecisionPath PathOf(StatsRow row) => row.Recommendation switch
    {
        Recommendation.Approve => DecisionPath.AsRecommended,
        Recommendation.Review => DecisionPath.AfterReview,
        Recommendation.Reject => DecisionPath.Override,
        null => DecisionPath.WithoutScore,
        var other => throw new InvalidOperationException($"no decision path for {other}"),
    };

    private static IReadOnlyList<CalibrationBand> Calibration(IReadOnlyCollection<StatsRow> rows)
    {
        var known = rows.Where(r => r.Outcome == FinalDecision.Approve && r.LoanOutcome is not null && r.PBad is not null).ToList();
        return Enumerable.Range(0, Bins)
            .Select(i =>
            {
                var band = known.Where(r => BinOf(r.PBad!.Value) == i).ToList();
                return new CalibrationBand(
                    (double)i / Bins,
                    (double)(i + 1) / Bins,
                    band.Count,
                    band.Count == 0 ? null : band.Average(r => r.PBad!.Value),
                    Rate(band.Count(r => r.LoanOutcome == LoanOutcomeKind.Defaulted), band.Count));
            })
            .ToList();
    }

    private static (int Overrides, int Overridable) Overrides(IReadOnlyCollection<StatsRow> rows)
    {
        var overridable = rows.Where(r => r.Outcome is not null && r.Recommendation is Recommendation.Approve or Recommendation.Reject).ToList();
        return (overridable.Count(IsOverride), overridable.Count);
    }

    private static double? ReviewShare(IReadOnlyCollection<StatsRow> rows) =>
        Rate(rows.Count(r => r.Recommendation == Recommendation.Review), rows.Count(r => r.Status == DecisionStatus.Scored));

    private static MatrixRow Row(IEnumerable<StatsRow> rows)
    {
        var list = rows.ToList();
        return new MatrixRow(
            list.Count(r => r.Outcome == FinalDecision.Approve),
            list.Count(r => r.Outcome == FinalDecision.Reject),
            list.Count(r => r.Outcome is null));
    }

    private static bool IsOverride(StatsRow r) =>
        r.Recommendation is { } recommendation && r.Outcome is { } outcome && OfficerDecision.IsOverrideOf(recommendation, outcome);

    private static int BinOf(double pBad) => Math.Clamp((int)Math.Floor(pBad * Bins), 0, Bins - 1);

    private static double? Rate(int part, int whole) => whole == 0 ? null : (double)part / whole;
}
