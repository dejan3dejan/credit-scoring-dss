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
        [CheckingAccountStatus.NoAccount] = "No checking account",
        [CheckingAccountStatus.Negative] = "Overdrawn (below 0 DM)",
        [CheckingAccountStatus.ZeroTo200] = "0 to 200 DM",
        [CheckingAccountStatus.AtLeast200OrSalaryAssignment] = "200 DM or more, or salary paid in for at least a year",

        [CreditHistory.PastPaymentDelays] = "Delays in paying off past credits",
        [CreditHistory.CriticalAccount] = "Critical account, or credits at other banks",
        [CreditHistory.NoCreditsOrAllPaidDuly] = "No credits taken, or all paid back on time",
        [CreditHistory.ExistingPaidDulySoFar] = "Existing credits paid back on time so far",
        [CreditHistory.AllAtThisBankPaidDuly] = "All credits at this bank paid back on time",

        [LoanPurpose.Other] = "Other",
        [LoanPurpose.NewCar] = "New car",
        [LoanPurpose.UsedCar] = "Used car",
        [LoanPurpose.FurnitureOrEquipment] = "Furniture or equipment",
        [LoanPurpose.RadioOrTelevision] = "Radio or television",
        [LoanPurpose.DomesticAppliances] = "Domestic appliances",
        [LoanPurpose.Repairs] = "Repairs",
        [LoanPurpose.Vacation] = "Vacation",
        [LoanPurpose.Retraining] = "Retraining",
        [LoanPurpose.Business] = "Business",

        [SavingsAccount.UnknownOrNone] = "Unknown, or no savings account",
        [SavingsAccount.Below100] = "Below 100 DM",
        [SavingsAccount.From100To500] = "100 to 500 DM",
        [SavingsAccount.From500To1000] = "500 to 1,000 DM",
        [SavingsAccount.AtLeast1000] = "1,000 DM or more",

        [EmploymentDuration.Unemployed] = "Unemployed",
        [EmploymentDuration.LessThan1Year] = "Less than 1 year",
        [EmploymentDuration.From1To4Years] = "1 to 4 years",
        [EmploymentDuration.From4To7Years] = "4 to 7 years",
        [EmploymentDuration.AtLeast7Years] = "7 years or more",

        [PersonalStatusSex.MaleDivorcedOrSeparated] = "Male, divorced or separated",
        [PersonalStatusSex.FemaleNonSingleOrMaleSingle] = "Female non-single, or male single",
        [PersonalStatusSex.MaleMarriedOrWidowed] = "Male, married or widowed",
        [PersonalStatusSex.FemaleSingle] = "Female, single",

        [OtherDebtors.None] = "None",
        [OtherDebtors.CoApplicant] = "Co-applicant",
        [OtherDebtors.Guarantor] = "Guarantor",

        [PropertyType.UnknownOrNone] = "Unknown, or no property",
        [PropertyType.CarOrOther] = "Car or other",
        [PropertyType.BuildingSocietySavingsOrLifeInsurance] = "Building society savings or life insurance",
        [PropertyType.RealEstate] = "Real estate",

        [OtherInstallmentPlans.Bank] = "At a bank",
        [OtherInstallmentPlans.Stores] = "At stores",
        [OtherInstallmentPlans.None] = "None",

        [Housing.ForFree] = "Lives for free",
        [Housing.Rent] = "Rents",
        [Housing.Own] = "Owns",

        [JobType.UnemployedOrUnskilledNonResident] = "Unemployed, or unskilled non-resident",
        [JobType.UnskilledResident] = "Unskilled, resident",
        [JobType.SkilledEmployeeOrOfficial] = "Skilled employee or official",
        [JobType.ManagerSelfEmployedOrHighlyQualified] = "Manager, self-employed or highly qualified",

        [DecisionStatus.Scored] = "Scored",
        [DecisionStatus.ScoringUnavailable] = "Scoring unavailable",

        [Recommendation.Approve] = "Approve",
        [Recommendation.Reject] = "Reject",

        [FinalDecision.Approve] = "Approved",
        [FinalDecision.Reject] = "Rejected",

        [ReviewFilter.Pending] = "Awaiting decision",
        [ReviewFilter.Approved] = "Approved",
        [ReviewFilter.Rejected] = "Rejected",
        [ReviewFilter.Override] = "Overrides",
    };

    private static readonly Dictionary<string, string> ScoringErrors = new()
    {
        [MlErrorCodes.Timeout] = "The scoring service did not answer in time.",
        [MlErrorCodes.Unreachable] = "The scoring service could not be reached.",
        [MlErrorCodes.UnexpectedStatus] = "The scoring service answered with an unexpected error.",
        [MlErrorCodes.InvalidResponse] = "The scoring service returned a score that could not be used.",
        [DecisionErrorCodes.ModelMismatch] = "The score came from a model this system is not configured for.",
        ["validation_error"] = "The scoring service rejected the input.",
        ["model_unavailable"] = "The scoring model is not loaded.",
        ["internal_error"] = "The scoring service failed while scoring.",
    };

    public static string Of(Enum value) =>
        All.TryGetValue(value, out var label)
            ? label
            : throw new ArgumentOutOfRangeException(nameof(value), value, $"no label for {value.GetType().Name}.{value}");

    public static string? ScoringError(string code) => ScoringErrors.GetValueOrDefault(code);

    public static IEnumerable<SelectListItem> Options<T>() where T : struct, Enum =>
        Enum.GetValues<T>().Select(v => new SelectListItem(Of(v), v.ToString()));
}
