using System.Linq.Expressions;
using CreditScoring.Api.Domain;

namespace CreditScoring.Api.Contracts;

public sealed record DecisionResponse(
    DecisionStatus Status,
    DateTimeOffset EvaluatedAt,
    double? PBad,
    string? ModelId,
    double? ThresholdUsed,
    Recommendation? Recommendation,
    string? ErrorCode)
{
    public static DecisionResponse From(CreditDecision decision) => new(
        decision.Status,
        decision.EvaluatedAt,
        decision.PBad,
        decision.ModelId,
        decision.ThresholdUsed,
        decision.Recommendation,
        decision.ErrorCode);
}

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
    public required string ClientNumber { get; init; }
    public required string ApplicantName { get; init; }
    public required string SubmittedBy { get; init; }
    public required DateTimeOffset SubmittedAt { get; init; }

    public required int DurationMonths { get; init; }
    public required int CreditAmount { get; init; }
    public required int Age { get; init; }
    public required decimal InstallmentSharePercent { get; init; }
    public required int YearsAtResidence { get; init; }
    public required int CreditsAtBank { get; init; }
    public required int Dependants { get; init; }

    public required CheckingAccountStatus CheckingAccountStatus { get; init; }
    public required CreditHistory CreditHistory { get; init; }
    public required LoanPurpose Purpose { get; init; }
    public required SavingsAccount SavingsAccount { get; init; }
    public required EmploymentDuration EmploymentDuration { get; init; }
    public required PersonalStatusSex PersonalStatusSex { get; init; }
    public required OtherDebtors OtherDebtors { get; init; }
    public required PropertyType Property { get; init; }
    public required OtherInstallmentPlans OtherInstallmentPlans { get; init; }
    public required Housing Housing { get; init; }
    public required JobType Job { get; init; }
    public required bool HasRegisteredTelephone { get; init; }
    public required bool IsForeignWorker { get; init; }

    public required DecisionResponse? Decision { get; init; }
    public required OfficerDecisionResponse? OfficerDecision { get; init; }
    public required IReadOnlyList<ScoringFailureResponse> ScoringFailures { get; init; }

    public static ApplicationResponse From(CreditApplication application) => new()
    {
        Id = application.Id,
        ClientId = application.ClientId,
        ClientNumber = application.Client?.ClientNumber
            ?? throw new InvalidOperationException("the application's client must be loaded to build a response"),
        ApplicantName = application.ApplicantName,
        SubmittedBy = application.SubmittedBy,
        SubmittedAt = application.SubmittedAt,
        DurationMonths = application.DurationMonths,
        CreditAmount = application.CreditAmount,
        Age = application.Age,
        InstallmentSharePercent = application.InstallmentSharePercent,
        YearsAtResidence = application.YearsAtResidence,
        CreditsAtBank = application.CreditsAtBank,
        Dependants = application.Dependants,
        CheckingAccountStatus = application.CheckingAccountStatus,
        CreditHistory = application.CreditHistory,
        Purpose = application.Purpose,
        SavingsAccount = application.SavingsAccount,
        EmploymentDuration = application.EmploymentDuration,
        PersonalStatusSex = application.PersonalStatusSex,
        OtherDebtors = application.OtherDebtors,
        Property = application.Property,
        OtherInstallmentPlans = application.OtherInstallmentPlans,
        Housing = application.Housing,
        Job = application.Job,
        HasRegisteredTelephone = application.HasRegisteredTelephone,
        IsForeignWorker = application.IsForeignWorker,
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
    int CreditAmount,
    int DurationMonths,
    DecisionStatus? Status,
    Recommendation? Recommendation,
    double? PBad,
    FinalDecision? FinalDecision,
    bool? IsOverride)
{
    public static readonly Expression<Func<CreditApplication, ApplicationSummary>> FromApplication = a => new ApplicationSummary(
        a.Id,
        a.ClientId,
        a.ApplicantName,
        a.SubmittedBy,
        a.SubmittedAt,
        a.CreditAmount,
        a.DurationMonths,
        a.Decision == null ? null : a.Decision.Status,
        a.Decision == null ? null : a.Decision.Recommendation,
        a.Decision == null ? null : a.Decision.PBad,
        a.OfficerDecision == null ? null : a.OfficerDecision.Outcome,
        a.OfficerDecision == null ? null : a.OfficerDecision.IsOverride);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
