using System.Linq.Expressions;
using CreditScoring.Api.Decisions;
using CreditScoring.Api.Domain;

namespace CreditScoring.Api.Contracts;

public sealed record DecisionResponse(
    DecisionStatus Status,
    DateTimeOffset EvaluatedAt,
    double? PBad,
    string? ModelId,
    double? ThresholdUsed,
    double? ReviewFromUsed,
    Recommendation? Recommendation,
    string? ErrorCode,
    IReadOnlyList<ScoreFactorResponse> Factors)
{
    public static DecisionResponse From(CreditDecision decision) => new(
        decision.Status,
        decision.EvaluatedAt,
        decision.PBad,
        decision.ModelId,
        decision.ThresholdUsed,
        decision.ReviewFromUsed,
        decision.Recommendation,
        decision.ErrorCode,
        decision.Factors.OrderBy(f => f.Rank).Select(f => new ScoreFactorResponse(f.Input, f.Contribution)).ToList());
}

public sealed record ScoreFactorResponse(string Input, double Contribution);

public sealed record OfficerDecisionResponse(
    FinalDecision Outcome,
    string? Reason,
    string DecidedBy,
    DateTimeOffset DecidedAt,
    bool IsOverride)
{
    public static OfficerDecisionResponse From(OfficerDecision decision) => new(
        decision.Outcome,
        decision.Reason,
        decision.DecidedBy,
        decision.DecidedAt,
        decision.IsOverride);
}

public sealed record ScoringFailureResponse(string ErrorCode, DateTimeOffset AttemptedAt)
{
    public static ScoringFailureResponse From(ScoringFailure failure) => new(failure.ErrorCode, failure.AttemptedAt);
}

public sealed record ApplicationResponse
{
    public required Guid Id { get; init; }
    public required Guid ClientId { get; init; }
    public required Guid BranchId { get; init; }
    public required string ClientNumber { get; init; }
    public required string ApplicantName { get; init; }
    public required string SubmittedBy { get; init; }
    public required DateTimeOffset SubmittedAt { get; init; }

    public required int TermMonths { get; init; }
    public required int LoanAmount { get; init; }
    public required LoanPurpose Purpose { get; init; }
    public required EmploymentLength EmploymentLength { get; init; }
    public required HomeOwnership HomeOwnership { get; init; }
    public required decimal AnnualIncome { get; init; }
    public required IncomeVerification IncomeVerification { get; init; }
    public required decimal DebtToIncomePercent { get; init; }
    public required int CreditScore { get; init; }
    public required DateOnly EarliestCreditLine { get; init; }
    public required double CreditHistoryYears { get; init; }
    public required int InquiriesLast6Months { get; init; }
    public required int DelinquenciesLast2Years { get; init; }
    public required int PublicRecords { get; init; }
    public required int Bankruptcies { get; init; }
    public required decimal RevolvingBalance { get; init; }
    public required decimal RevolvingUtilizationPercent { get; init; }
    public required int OpenAccounts { get; init; }
    public required int TotalAccounts { get; init; }
    public required int MortgageAccounts { get; init; }

    public required DecisionResponse? Decision { get; init; }
    public required OfficerDecisionResponse? OfficerDecision { get; init; }
    public required IReadOnlyList<ScoringFailureResponse> ScoringFailures { get; init; }

    public static ApplicationResponse From(CreditApplication application) => new()
    {
        Id = application.Id,
        ClientId = application.ClientId,
        BranchId = application.BranchId,
        ClientNumber = application.Client?.ClientNumber
            ?? throw new InvalidOperationException("the application's client must be loaded to build a response"),
        ApplicantName = application.ApplicantName,
        SubmittedBy = application.SubmittedBy,
        SubmittedAt = application.SubmittedAt,
        TermMonths = BusinessLimits.TermMonths,
        LoanAmount = application.LoanAmount,
        Purpose = application.Purpose,
        EmploymentLength = application.EmploymentLength,
        HomeOwnership = application.HomeOwnership,
        AnnualIncome = application.AnnualIncome,
        IncomeVerification = application.IncomeVerification,
        DebtToIncomePercent = application.DebtToIncomePercent,
        CreditScore = application.CreditScore,
        EarliestCreditLine = application.EarliestCreditLine,
        CreditHistoryYears = LendingClubMapper.CreditHistoryYears(application.EarliestCreditLine, application.SubmittedAt),
        InquiriesLast6Months = application.InquiriesLast6Months,
        DelinquenciesLast2Years = application.DelinquenciesLast2Years,
        PublicRecords = application.PublicRecords,
        Bankruptcies = application.Bankruptcies,
        RevolvingBalance = application.RevolvingBalance,
        RevolvingUtilizationPercent = application.RevolvingUtilizationPercent,
        OpenAccounts = application.OpenAccounts,
        TotalAccounts = application.TotalAccounts,
        MortgageAccounts = application.MortgageAccounts,
        Decision = application.Decision is null ? null : DecisionResponse.From(application.Decision),
        OfficerDecision = application.OfficerDecision is null ? null : OfficerDecisionResponse.From(application.OfficerDecision),
        ScoringFailures = application.ScoringFailures.OrderBy(f => f.AttemptedAt).Select(ScoringFailureResponse.From).ToList(),
    };
}

public sealed record ApplicationSummary(
    Guid Id,
    Guid ClientId,
    string ApplicantName,
    string SubmittedBy,
    DateTimeOffset SubmittedAt,
    int LoanAmount,
    LoanPurpose Purpose,
    DecisionStatus? Status,
    Recommendation? Recommendation,
    double? PBad,
    FinalDecision? FinalDecision,
    bool? IsOverride,
    Guid BranchId)
{
    public static readonly Expression<Func<CreditApplication, ApplicationSummary>> FromApplication = a => new ApplicationSummary(
        a.Id,
        a.ClientId,
        a.ApplicantName,
        a.SubmittedBy,
        a.SubmittedAt,
        a.LoanAmount,
        a.Purpose,
        a.Decision == null ? null : a.Decision.Status,
        a.Decision == null ? null : a.Decision.Recommendation,
        a.Decision == null ? null : a.Decision.PBad,
        a.OfficerDecision == null ? null : a.OfficerDecision.Outcome,
        a.OfficerDecision == null ? null : a.OfficerDecision.IsOverride,
        a.BranchId);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
