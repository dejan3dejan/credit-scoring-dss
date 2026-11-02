using CreditScoring.Api.Contracts;
using CreditScoring.Api.Decisions;
using CreditScoring.Api.Domain;
using CreditScoring.Api.Ml;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CreditScoring.Api.Ui;

public static class Labels
{
    private static readonly Dictionary<Enum, string> All = new()
    {
        [LoanPurpose.DebtConsolidation] = "Debt consolidation",
        [LoanPurpose.CreditCard] = "Paying off credit cards",
        [LoanPurpose.HomeImprovement] = "Home improvement",
        [LoanPurpose.MajorPurchase] = "Major purchase",
        [LoanPurpose.Car] = "Car",
        [LoanPurpose.Medical] = "Medical expenses",
        [LoanPurpose.Moving] = "Moving",
        [LoanPurpose.House] = "House",
        [LoanPurpose.Vacation] = "Vacation",
        [LoanPurpose.Wedding] = "Wedding",
        [LoanPurpose.SmallBusiness] = "Small business",
        [LoanPurpose.RenewableEnergy] = "Renewable energy",
        [LoanPurpose.Other] = "Other",

        [EmploymentLength.NotStated] = "Not stated",
        [EmploymentLength.LessThanOneYear] = "Less than 1 year",
        [EmploymentLength.OneYear] = "1 year",
        [EmploymentLength.TwoYears] = "2 years",
        [EmploymentLength.ThreeYears] = "3 years",
        [EmploymentLength.FourYears] = "4 years",
        [EmploymentLength.FiveYears] = "5 years",
        [EmploymentLength.SixYears] = "6 years",
        [EmploymentLength.SevenYears] = "7 years",
        [EmploymentLength.EightYears] = "8 years",
        [EmploymentLength.NineYears] = "9 years",
        [EmploymentLength.TenOrMoreYears] = "10 years or more",

        [HomeOwnership.Rent] = "Rents",
        [HomeOwnership.Own] = "Owns outright",
        [HomeOwnership.Mortgage] = "Owns with a mortgage",
        [HomeOwnership.Other] = "Other",

        [IncomeVerification.NotVerified] = "Not verified",
        [IncomeVerification.SourceVerified] = "Source verified",
        [IncomeVerification.Verified] = "Verified",

        [DecisionStatus.Scored] = "Scored",
        [DecisionStatus.ScoringUnavailable] = "Scoring unavailable",

        [Recommendation.Approve] = "Approve",
        [Recommendation.Review] = "Review closely",
        [Recommendation.Reject] = "Reject",

        [FinalDecision.Approve] = "Approved",
        [FinalDecision.Reject] = "Rejected",

        [ReviewFilter.Pending] = "Awaiting decision",
        [ReviewFilter.Approved] = "Approved",
        [ReviewFilter.Rejected] = "Rejected",
        [ReviewFilter.Override] = "Overrides",

        [ModelRole.Active] = "Active",
        [ModelRole.Shadow] = "Shadow",
        [ModelRole.Retired] = "Retired",
    };

    private static readonly Dictionary<string, string> ScoringErrors = new()
    {
        [MlErrorCodes.Timeout] = "The scoring service did not answer in time.",
        [MlErrorCodes.Unreachable] = "The scoring service could not be reached.",
        [MlErrorCodes.UnexpectedStatus] = "The scoring service answered with an unexpected error.",
        [MlErrorCodes.InvalidResponse] = "The scoring service returned a score that could not be used.",
        [DecisionErrorCodes.ModelMismatch] = "The score came from a different model than the one asked for.",
        [DecisionErrorCodes.NoActiveModel] = "No scoring model is set as active.",
        ["model_not_found"] = "The scoring service does not have this model loaded.",
        ["validation_error"] = "The scoring service rejected the input.",
        ["model_unavailable"] = "The scoring model is not loaded.",
        ["internal_error"] = "The scoring service failed while scoring.",
    };

    private static readonly Dictionary<string, string> ModelNames = new()
    {
        ["RandomForest"] = "Random forest",
        ["LogisticRegression"] = "Logistic regression",
        ["XGBoost"] = "XGBoost",
    };

    public static string ModelName(string name) => ModelNames.GetValueOrDefault(name, name);

    public static string Of(Enum value) =>
        All.TryGetValue(value, out var label)
            ? label
            : throw new ArgumentOutOfRangeException(nameof(value), value, $"no label for {value.GetType().Name}.{value}");

    public static string? ScoringError(string code) => ScoringErrors.GetValueOrDefault(code);

    public static IEnumerable<SelectListItem> Options<T>() where T : struct, Enum =>
        Enum.GetValues<T>().Select(v => new SelectListItem(Of(v), v.ToString()));
}
